using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class EncryptionInfo : IDisposableExtended {
		public bool Encrypt { get; set; }

		public IEncryptorParameters EncryptionParameters { get; set; }

		public Func<SafeArrayHandle> SecretHandler { private get; set; }

		public SafeArrayHandle Secret() {
			//TODO: we should review all this. is it ok to make a copy in memory? if not, we have to avoid disposing, is this safe?
			return this.SecretHandler().Clone();
		}
		
		private FileEncryptor.FileEncryptorContextHandler contextHandler;
		public FileEncryptor.FileEncryptorContextHandler ContextHandler {
			get {
				if(this.contextHandler == null) {
					this.contextHandler = new FileEncryptor.FileEncryptorContextHandler();

					this.contextHandler.PasswordBytes = this.Secret();
				}

				return this.contextHandler;
			}
		}
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.contextHandler?.Dispose();
			}

			this.IsDisposed = true;
		}

		~EncryptionInfo() {
			this.Dispose(false);
		}

	#endregion
	}

	public interface IWalletFileInfo : IDisposableExtended {
		string Filename { get; }
		SafeArrayHandle Filebytes { get; }
		WalletPassphraseDetails WalletSecurityDetails { get; }
		int? FileCacheTimeout { get; }
		bool IsLoaded { get; }
		bool FileExists { get; }
		void SetFileCacheTimeout(int? value, LockContext lockContext);
		void RefreshFile();
		Task CreateEmptyFile(LockContext lockContext, object data = null);
		Task Load(LockContext lockContext, object data = null);
		Task Reset(LockContext lockContext);
		Task ReloadFileBytes(LockContext lockContext, object data = null);
		void ClearCached(LockContext lockContext);

		/// <summary>
		///     cause a changing of the encryption
		/// </summary>
		Task ChangeEncryption(LockContext lockContext, object data = null);

		void ClearEncryptionInfo();
		Task Save(LockContext lockContext, object data = null);

		/// <summary>
		///     run a filtering query on all items
		/// </summary>
		/// <param name="operation"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		Task<T[]> RunQuery<T, K>(Func<IEnumerable<K>, IEnumerable<T>> operation, LockContext lockContext, object data = null)
			where T : new();

		public Task<bool> CollectionExists<T>(LockContext lockContext);
	}

	public abstract class WalletFileInfo : IWalletFileInfo {
		private static readonly xxHasher32 hasher = new xxHasher32();
		protected readonly ChainConfigurations chainConfiguration;

		protected readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();
		protected readonly IWalletSerialisationFal serialisationFal;

		protected readonly BlockchainServiceSet serviceSet;
		private Timer fileBytesTimer;

		private int lastFileHash;

		protected virtual string DBDalKey => ""; //the default one

		public WalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) {
			this.serialisationFal = serialisationFal;
			this.Filename = filename;
			this.WalletSecurityDetails = walletSecurityDetails;
			this.SetFileCacheTimeout(fileCacheTimeout, null);
			this.serviceSet = serviceSet;
			this.chainConfiguration = chainConfiguration;
		}

		protected EncryptionInfo EncryptionInfo { get; set; }

		public string Filename { get; protected set; }
		public SafeArrayHandle Filebytes { get; } = SafeArrayHandle.Create();
		public WalletPassphraseDetails WalletSecurityDetails { get; }

		public int? FileCacheTimeout { get; private set; }

		public void SetFileCacheTimeout(int? value, LockContext lockContext) {

			this.FileCacheTimeout = value;
			this.ResetFileBytesTimer(lockContext);

		}

		public bool IsLoaded => this.Filebytes.HasData;

		public bool FileExists => this.serialisationFal.TransactionalFileSystem.FileExists(this.Filename);

		public Task<bool> CollectionExists<T>(LockContext lockContext) {
			return this.RunQueryDbOperation((IWalletDBDAL, lc) => Task.FromResult(IWalletDBDAL.CollectionExists<T>()), lockContext);
		}

		public void RefreshFile() {
			this.serialisationFal.TransactionalFileSystem.RefreshFile(this.Filename);
		}

		public virtual async Task CreateEmptyFile(LockContext lockContext, object data = null) {
			if(this.IsLoaded) {
				throw new ApplicationException("File is already created");
			}

			if(this.FileExists) {
				throw new ApplicationException("A file already exists. we can not overwrite an existing file. delete it and try again");
			}

			await this.CreateEmptyDb(lockContext).ConfigureAwait(false);

			// force a creation
			await this.CreateSecurityDetails(lockContext).ConfigureAwait(false);

			await this.SaveFile(lockContext, true, data).ConfigureAwait(false);
		}

		public virtual Task Reset(LockContext lockContext) {
			this.ClearFileBytes();

			this.ClearFileBytesTimer();

			return Task.CompletedTask;
		}

		/// <summary>
		///     if data was previously loaded, we for ce a refresh
		/// </summary>
		public virtual async Task ReloadFileBytes(LockContext lockContext, object data = null) {

			if(this.IsLoaded) {
				this.ClearCached(lockContext);
				await this.LoadFileBytes(lockContext, data).ConfigureAwait(false);
			}
		}

		public virtual void ClearCached(LockContext lockContext) {
		}

		public virtual async Task Load(LockContext lockContext, object data = null) {

			if(!this.FileExists) {
				throw new ApplicationException($"Attempted to load a wallet structure file ({this.Filename}) that does not exist. ");
			}

			await this.PrepareEncryptionInfo(lockContext).ConfigureAwait(false);

			await this.LoadFileBytes(lockContext, data).ConfigureAwait(false);
		}

		/// <summary>
		///     cause a changing of the encryption
		/// </summary>
		public virtual async Task ChangeEncryption(LockContext lockContext, object data = null) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.ClearEncryptionInfo();

				// get the new settingsBase
				await this.CreateSecurityDetails(handle).ConfigureAwait(false);

				string originalName = this.Filename;
				string tempFileName = this.Filename + ".tmp";

				this.Filename = tempFileName;
				await this.SaveFile(handle, true, data).ConfigureAwait(false);
				this.Filename = originalName;

				// swap the files
				this.serialisationFal.TransactionalFileSystem.FileDelete(originalName);
				this.serialisationFal.TransactionalFileSystem.FileMove(tempFileName, originalName);
			}
		}

		public void ClearEncryptionInfo() {
			this.EncryptionInfo = null;
		}

		public virtual async Task Save(LockContext lockContext, object data = null) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				await this.LazyLoad(handle, data).ConfigureAwait(false);
				
				await this.SaveFile(handle, false, data).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     run a filtering query on all items
		/// </summary>
		/// <param name="operation"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public Task<T[]> RunQuery<T, K>(Func<IEnumerable<K>, IEnumerable<T>> operation, LockContext lockContext, object data = null)
			where T : new() {

			return this.RunQueryDbOperation((IWalletDBDAL, lc) => {

				return IWalletDBDAL.OpenAsync(async db => IWalletDBDAL.CollectionExists<K>(db) ? operation(IWalletDBDAL.All<K>(db)).ToArray() : new T[0]);

			}, lockContext, data);
		}

		public void DeleteFile() {

			if(!this.FileExists) {
				return;
			}

			this.serialisationFal.TransactionalFileSystem.FileDelete(this.Filename);
		}

		private Task CreateEmptyDb(LockContext lockContext, object data = null) {
			return this.RunNoLoadDbOperation(this.CreateDbFile, lockContext, data);
		}

		protected virtual Task LoadFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					this.ClearFileBytes();

					try {
						this.Filebytes.Entry = (await this.serialisationFal.LoadFile(this.Filename, this.EncryptionInfo, false).ConfigureAwait(false)).Entry;
					} catch(FileNotFoundException fnex) {
						//TODO: anything? it could be normal
					}

					this.ResetFileBytesTimer(handle);
				}
			}, data);

		}

		private void ClearFileBytes() {
			if(this.Filebytes.HasData) {
				this.Filebytes.Entry.Disposed = entry => entry.Clear();
				this.Filebytes.Entry = null;
			}
		}

		protected void ClearFileBytesTimer() {
			if(this.fileBytesTimer != null) {
				this.fileBytesTimer.Dispose();
				this.fileBytesTimer = null;
			}
		}

		protected void ResetFileBytesTimer(LockContext lockContext) {
			this.ClearFileBytesTimer();

			if(this.FileCacheTimeout.HasValue) {
				this.fileBytesTimer = new Timer(state => {

					try {
						using(LockHandle handle = this.locker.Lock(lockContext)) {
							// clear it all from memory
							this.ClearFileBytes();

							this.ResetFileBytesTimer(handle);
						}
					} catch(Exception ex) {
						//TODO: do something?
						this.serialisationFal.CentralCoordinator.Log.Error(ex, "Timer exception");
					}

				}, this, TimeSpan.FromSeconds(this.FileCacheTimeout.Value), new TimeSpan(-1));
			}
		}

		protected abstract Task PrepareEncryptionInfo(LockContext lockContext);

		protected abstract Task CreateDbFile(IWalletDBDAL IWalletDBDAL, LockContext lockContext);

		protected abstract Task CreateSecurityDetails(LockContext lockContext);


		protected virtual async Task RunCryptoOperation(Func<Task> action, object data = null) {
			try {
				await action().ConfigureAwait(false);
			} catch(DataEncryptionException dex) {
				throw new WalletDecryptionException(dex);
			}
		}

		protected virtual async Task<U> RunCryptoOperation<U>(Func<Task<U>> action, object data = null) {
			try {
				return await action().ConfigureAwait(false);
			} catch(DataEncryptionException dex) {
				throw new WalletDecryptionException(dex);
			}
		}

		protected async Task SaveFile(LockContext lockContext, bool force = false, object data = null) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if((this.Filebytes != null) && this.Filebytes.HasData) {
					int hash = hasher.Hash(this.Filebytes);

					if((hash != this.lastFileHash) || force) {
						// file has changed, lets save it
						await this.SaveFileBytes(handle).ConfigureAwait(false);
						this.lastFileHash = hash;
					}
				}
			}
		}

		protected virtual Task SaveFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await this.serialisationFal.SaveFile(this.Filename, this.Filebytes, this.EncryptionInfo, false).ConfigureAwait(false);
				}
			}, data);
		}

		private Task RunNoLoadDbOperation(Func<IWalletDBDAL, LockContext, Task> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					using SafeArrayHandle newBytes = await this.serialisationFal.RunDbOperation(this.DBDalKey, operation, this.Filebytes, handle).ConfigureAwait(false);

					// clear previous memory since we replaced it
					this.ClearFileBytes();
					this.Filebytes.Entry = newBytes.Entry;
				}
			}, data);
		}

		private Task<T> RunNoLoadDbOperation<T>(Func<IWalletDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					(SafeArrayHandle newBytes, T result) = await this.serialisationFal.RunDbOperation(this.DBDalKey, operation, this.Filebytes, handle).ConfigureAwait(false);

					using(newBytes) {
						// clear previous memory since we replaced it
						this.ClearFileBytes();
						this.Filebytes.Entry = newBytes.Entry;
					}

					return result;
				}
			}, data);
		}

		protected async Task RunDbOperation(Func<IWalletDBDAL, LockContext, Task> operation, LockContext lockContext, object data = null) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.LazyLoad(handle, data).ConfigureAwait(false);

				await this.RunNoLoadDbOperation(operation, handle, data).ConfigureAwait(false);
			}
		}

		protected async Task<T> RunDbOperation<T>(Func<IWalletDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				await this.LazyLoad(handle, data).ConfigureAwait(false);

				return await this.RunNoLoadDbOperation(operation, handle, data).ConfigureAwait(false);
			}
		}

		protected Task<T> RunQueryDbOperation<T>(Func<IWalletDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await this.LazyLoad(handle).ConfigureAwait(false);

					return await this.serialisationFal.RunQueryDbOperation(this.DBDalKey, operation, this.Filebytes, handle).ConfigureAwait(false);
				}
			}, data);
		}

		protected Task LazyLoad(LockContext lockContext, object data = null) {
			if(!this.IsLoaded) {
				return this.Load(lockContext, data);
			}

			return Task.CompletedTask;
		}
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		~WalletFileInfo() {
			this.Dispose(false);
		}

		protected virtual void DisposeAll() {

			this.WalletSecurityDetails?.Dispose();
			this.ClearEncryptionInfo();
			this.ClearCached(null);
			this.Reset(null).WaitAndUnwrapException();
		}

	#endregion
	}
}