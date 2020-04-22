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
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class EncryptionInfo {
		public bool Encrypt { get; set; }

		public IEncryptorParameters EncryptionParameters { get; set; }

		public Func<SafeArrayHandle> Secret { get; set; }
	}

	public interface IWalletFileInfo {
		string                  Filename              { get; }
		SafeArrayHandle         Filebytes             { get; }
		WalletPassphraseDetails WalletSecurityDetails { get; }
		int?                    FileCacheTimeout      { get; }
		void                    SetFileCacheTimeout(int? value, LockContext lockContext);
		bool                    IsLoaded   { get; }
		bool                    FileExists { get; }
		void                    RefreshFile();
		Task                    CreateEmptyFile(LockContext lockContext, object data = null);
		Task                    Load(LockContext lockContext, object data = null);
		Task                    Reset(LockContext lockContext);
		Task                    ReloadFileBytes(LockContext lockContext, object data = null);

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

		protected readonly RecursiveAsyncLock      locker = new RecursiveAsyncLock();
		protected readonly IWalletSerialisationFal serialisationFal;

		protected readonly BlockchainServiceSet serviceSet;
		private            Timer                fileBytesTimer;

		private            int?                fileCacheTimeout;
		private            int                 lastFileHash;
		protected readonly ChainConfigurations chainConfiguration;

		public WalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) {
			this.serialisationFal      = serialisationFal;
			this.Filename              = filename;
			this.WalletSecurityDetails = walletSecurityDetails;
			this.SetFileCacheTimeout(fileCacheTimeout, null);
			this.serviceSet         = serviceSet;
			this.chainConfiguration = chainConfiguration;
		}

		protected EncryptionInfo EncryptionInfo { get; set; }

		public string                  Filename              { get; protected set; }
		public SafeArrayHandle         Filebytes             { get; } = SafeArrayHandle.Create();
		public WalletPassphraseDetails WalletSecurityDetails { get; }

		public int? FileCacheTimeout {
			get => this.fileCacheTimeout;
		}

		public void SetFileCacheTimeout(int? value, LockContext lockContext) {

			this.fileCacheTimeout = value;
			this.ResetFileBytesTimer(lockContext);

		}

		public bool IsLoaded => this.Filebytes.HasData;

		public bool FileExists => this.serialisationFal.TransactionalFileSystem.FileExists(this.Filename);

		public Task<bool> CollectionExists<T>(LockContext lockContext) {
			return this.RunQueryDbOperation((litedbDal, lc) => Task.FromResult(litedbDal.CollectionExists<T>()), lockContext);
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

			await CreateEmptyDb(lockContext).ConfigureAwait(false);

			// force a creation
			await this.CreateSecurityDetails(lockContext).ConfigureAwait(false);

			await this.SaveFile(lockContext, true, data).ConfigureAwait(false);
		}

		public void DeleteFile() {

			if(!this.FileExists) {
				return;
			}

			this.serialisationFal.TransactionalFileSystem.FileDelete(this.Filename);
		}

		public virtual Task Reset(LockContext lockContext) {
			this.ClearFileBytes();

			this.ClearFileBytesTimer();

			return Task.CompletedTask;
		}

		/// <summary>
		///     if data was previously loaded, we for ce a refresh
		/// </summary>
		public virtual Task ReloadFileBytes(LockContext lockContext, object data = null) {

			if(this.IsLoaded) {
				return this.LoadFileBytes(lockContext, data);
			}

			return Task.CompletedTask;
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
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.ClearEncryptionInfo();

				// get the new settingsBase
				await CreateSecurityDetails(handle).ConfigureAwait(false);

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

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				await this.LazyLoad(handle, data).ConfigureAwait(false);

				await this.UpdateDbEntry(handle).ConfigureAwait(false);

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

			return this.RunQueryDbOperation((litedbDal, lc) => {

				return litedbDal.OpenAsync<T[]>(async db => litedbDal.CollectionExists<K>(db) ? operation(litedbDal.All<K>(db)).ToArray() : new T[0]);

			}, lockContext, data);
		}

		private Task CreateEmptyDb(LockContext lockContext, object data = null) {
			return this.RunNoLoadDbOperation(this.CreateDbFile, lockContext, data);
		}

		protected virtual Task LoadFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					this.ClearFileBytes();

					try {
						this.Filebytes.Entry = (await serialisationFal.LoadFile(Filename, EncryptionInfo, false).ConfigureAwait(false)).Entry;
					} catch(FileNotFoundException fnex) {
						//TODO: anything? it could be normal
					}

					this.ResetFileBytesTimer(handle);
				}
			}, data);

		}

		private void ClearFileBytes() {
			if(this.Filebytes.HasData) {
				this.Filebytes.Entry.Disposed = (entry) => entry.Clear();
				this.Filebytes.Entry          = null;
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
						using(var handle = this.locker.Lock(lockContext)) {
							// clear it all from memory
							this.ClearFileBytes();

							this.ResetFileBytesTimer(handle);
						}
					} catch(Exception ex) {
						//TODO: do something?
						Log.Error(ex, "Timer exception");
					}

				}, this, TimeSpan.FromSeconds(this.FileCacheTimeout.Value), new TimeSpan(-1));
			}
		}

		protected abstract Task PrepareEncryptionInfo(LockContext lockContext);

		protected abstract Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext);

		protected abstract Task CreateSecurityDetails(LockContext lockContext);

		protected abstract Task UpdateDbEntry(LockContext lockContext);

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
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.Filebytes != null && this.Filebytes.HasData) {
					int hash = hasher.Hash(this.Filebytes);

					if(hash != this.lastFileHash || force) {
						// file has changed, lets save it
						await this.SaveFileBytes(handle).ConfigureAwait(false);
						this.lastFileHash = hash;
					}
				}
			}
		}

		protected virtual Task SaveFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await this.serialisationFal.SaveFile(this.Filename, this.Filebytes, this.EncryptionInfo, false).ConfigureAwait(false);
				}
			}, data);
		}

		private Task RunNoLoadDbOperation(Func<LiteDBDAL, LockContext, Task> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					using SafeArrayHandle newBytes = await serialisationFal.RunDbOperation(operation, Filebytes, handle).ConfigureAwait(false);

					// clear previous memory since we replaced it
					this.ClearFileBytes();
					this.Filebytes.Entry = newBytes.Entry;
				}
			}, data);
		}

		private Task<T> RunNoLoadDbOperation<T>(Func<LiteDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					(SafeArrayHandle newBytes, T result) = await serialisationFal.RunDbOperation(operation, Filebytes, handle).ConfigureAwait(false);

					using(newBytes) {
						// clear previous memory since we replaced it
						this.ClearFileBytes();
						this.Filebytes.Entry = newBytes.Entry;
					}

					return result;
				}
			}, data);
		}

		protected async Task RunDbOperation(Func<LiteDBDAL, LockContext, Task> operation, LockContext lockContext, object data = null) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await LazyLoad(handle, data).ConfigureAwait(false);

				await RunNoLoadDbOperation(operation, handle, data).ConfigureAwait(false);
			}
		}

		protected async Task<T> RunDbOperation<T>(Func<LiteDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				await LazyLoad(handle, data).ConfigureAwait(false);

				return await RunNoLoadDbOperation(operation, handle, data).ConfigureAwait(false);
			}
		}

		protected Task<T> RunQueryDbOperation<T>(Func<LiteDBDAL, LockContext, Task<T>> operation, LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await LazyLoad(handle).ConfigureAwait(false);

					return await serialisationFal.RunQueryDbOperation(operation, Filebytes, handle).ConfigureAwait(false);
				}
			}, data);
		}

		private Task LazyLoad(LockContext lockContext, object data = null) {
			if(!this.IsLoaded) {
				return this.Load(lockContext, data);
			}

			return Task.CompletedTask;
		}
	}
}