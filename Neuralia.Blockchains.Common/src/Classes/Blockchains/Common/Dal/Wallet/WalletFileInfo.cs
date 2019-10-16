using System;
using System.Collections.Generic;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class EncryptionInfo {
		public bool encrypt;

		public IEncryptorParameters encryptionParameters { get; set; }

		public Func<SafeArrayHandle> Secret { get; set; }
	}

	public interface IWalletFileInfo {
		string Filename { get; }
		SafeArrayHandle Filebytes { get; }
		WalletPassphraseDetails WalletSecurityDetails { get; }
		int? FileCacheTimeout { get; set; }
		bool IsLoaded { get; }
		bool FileExists { get; }
		void CreateEmptyFile(object data = null);
		void Load(object data = null);
		void Reset();
		void ReloadFileBytes(object data = null);

		/// <summary>
		///     cause a changing of the encryption
		/// </summary>
		void ChangeEncryption(object data = null);

		void ClearEncryptionInfo();
		void Save(object data = null);

		/// <summary>
		///     run a filtering query on all items
		/// </summary>
		/// <param name="operation"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		IEnumerable<T> RunQuery<T, K>(Func<IEnumerable<K>, IEnumerable<T>> operation, object data = null)
			where T : new();
	}

	public abstract class WalletFileInfo : IWalletFileInfo {
		private static readonly xxHasher32 hasher = new xxHasher32();

		protected readonly object locker = new object();
		protected readonly IWalletSerialisationFal serialisationFal;

		protected readonly BlockchainServiceSet serviceSet;
		private Timer fileBytesTimer;

		private int? fileCacheTimeout;
		private int lastFileHash;

		public WalletFileInfo(string filename, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) {
			this.serialisationFal = serialisationFal;
			this.Filename = filename;
			this.WalletSecurityDetails = walletSecurityDetails;
			this.FileCacheTimeout = fileCacheTimeout;
			this.serviceSet = serviceSet;
		}

		protected EncryptionInfo EncryptionInfo { get; set; }

		public string Filename { get; protected set; }
		public SafeArrayHandle Filebytes { get;  } = SafeArrayHandle.Create();
		public WalletPassphraseDetails WalletSecurityDetails { get; }

		public int? FileCacheTimeout {
			get => this.fileCacheTimeout;
			set {
				this.fileCacheTimeout = value;
				this.ResetFileBytesTimer();
			}
		}

		public bool IsLoaded => this.Filebytes.HasData;

		public bool FileExists => this.serialisationFal.TransactionalFileSystem.FileExists(this.Filename);

		public virtual void CreateEmptyFile(object data = null) {
			if(this.IsLoaded) {
				throw new ApplicationException("File is already created");
			}

			if(this.FileExists) {
				throw new ApplicationException("A file already exists. we can not overwrite an existing file. delete it and try again");
			}

			this.CreateEmptyDb();

			// force a creation
			this.CreateSecurityDetails();

			this.SaveFile(true, data);
		}
		
		public void DeleteFile() {

			if(!this.FileExists) {
				return;
			}

			this.serialisationFal.TransactionalFileSystem.FileDelete(this.Filename);
		}

		public virtual void Reset() {
			this.ClearFileBytes();

			this.ClearFileBytesTimer();
		}

		/// <summary>
		///     if data was previously loaded, we for ce a refresh
		/// </summary>
		public virtual void ReloadFileBytes(object data = null) {

			if(this.IsLoaded) {
				this.LoadFileBytes(data);
			}
		}

		public virtual void Load(object data = null) {

			if(!this.FileExists) {
				throw new ApplicationException($"Attempted to load a wallet structure file ({this.Filename}) that does not exist. ");
			}

			this.PrepareEncryptionInfo();

			this.LoadFileBytes(data);
		}

		/// <summary>
		///     cause a changing of the encryption
		/// </summary>
		public virtual void ChangeEncryption(object data = null) {
			lock(this.locker) {
				this.ClearEncryptionInfo();

				// get the new settingsBase
				this.CreateSecurityDetails();

				string originalName = this.Filename;
				string tempFileName = this.Filename + ".tmp";

				this.Filename = tempFileName;
				this.SaveFile(true, data);
				this.Filename = originalName;

				// swap the files
				this.serialisationFal.TransactionalFileSystem.FileDelete(originalName);
				this.serialisationFal.TransactionalFileSystem.FileMove(tempFileName, originalName);
			}
		}

		public void ClearEncryptionInfo() {
			this.EncryptionInfo = null;
		}

		public virtual void Save(object data = null) {

			lock(this.locker) {
				this.LazyLoad(data);

				this.UpdateDbEntry();

				this.SaveFile(false, data);
			}
		}

		/// <summary>
		///     run a filtering query on all items
		/// </summary>
		/// <param name="operation"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public IEnumerable<T> RunQuery<T, K>(Func<IEnumerable<K>, IEnumerable<T>> operation, object data = null)
			where T : new() {

			return this.RunQueryDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<K>()) {
					return operation(litedbDal.All<K>());
				}

				return new T[0];
			}, data);
		}

		private void CreateEmptyDb(object data = null) {
			this.RunNoLoadDbOperation(this.CreateDbFile, data);
		}

		protected virtual void LoadFileBytes(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					this.ClearFileBytes();
					this.Filebytes.Entry = this.serialisationFal.LoadFile(this.Filename, this.EncryptionInfo, false).Entry;
					this.ResetFileBytesTimer();
				}
			}, data);
		}

		private void ClearFileBytes() {
			if(this.Filebytes.HasData) {
				this.Filebytes.Entry.Disposed = (entry) => entry.Clear();
				this.Filebytes.Entry = null;
			}
		}

		protected void ClearFileBytesTimer() {
			if(this.fileBytesTimer != null) {
				this.fileBytesTimer.Dispose();
				this.fileBytesTimer = null;
			}
		}

		protected void ResetFileBytesTimer() {
			this.ClearFileBytesTimer();

			if(this.FileCacheTimeout.HasValue) {
				this.fileBytesTimer = new Timer(state => {

					lock(this.locker) {
						// clear it all from memory
						this.ClearFileBytes();

						this.ResetFileBytesTimer();
					}

				}, this, TimeSpan.FromSeconds(this.FileCacheTimeout.Value), new TimeSpan(-1));
			}
		}

		protected abstract void PrepareEncryptionInfo();

		protected abstract void CreateDbFile(LiteDBDAL litedbDal);

		protected abstract void CreateSecurityDetails();

		protected abstract void UpdateDbEntry();

		/// <summary>
		///     converet decryption errors into wallet error
		/// </summary>
		/// <param name="action"></param>
		/// <exception cref="WalletDecryptionException"></exception>
		protected virtual void RunCryptoOperation(Action action, object data = null) {
			try {
				action();
			} catch(DataEncryptionException dex) {
				throw new WalletDecryptionException(dex);
			}
		}

		/// <summary>
		///     converet decryption errors into wallet error
		/// </summary>
		/// <param name="action"></param>
		/// <exception cref="WalletDecryptionException"></exception>
		protected virtual U RunCryptoOperation<U>(Func<U> action, object data = null) {
			try {
				return action();
			} catch(DataEncryptionException dex) {
				throw new WalletDecryptionException(dex);
			}
		}

		protected void SaveFile(bool force = false, object data = null) {
			lock(this.locker) {
				if((this.Filebytes != null) && this.Filebytes.HasData) {
					int hash = hasher.Hash(this.Filebytes);

					if((hash != this.lastFileHash) || force) {
						// file has changed, lets save it
						this.SaveFileBytes();
						this.lastFileHash = hash;
					}
				}
			}
		}

		protected virtual void SaveFileBytes(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					this.serialisationFal.SaveFile(this.Filename, this.Filebytes, this.EncryptionInfo, false);
				}
			}, data);
		}

		private void RunNoLoadDbOperation(Action<LiteDBDAL> operation, object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					using(SafeArrayHandle newBytes = this.serialisationFal.RunDbOperation(operation, this.Filebytes)) {

						// clear previous memory since we replaced it
						this.ClearFileBytes();
						this.Filebytes.Entry = newBytes.Entry;
					}
				}
			}, data);
		}

		private T RunNoLoadDbOperation<T>(Func<LiteDBDAL, T> operation, object data = null) {
			return this.RunCryptoOperation(() => {
				lock(this.locker) {
					(SafeArrayHandle newBytes, T result) = this.serialisationFal.RunDbOperation(operation, this.Filebytes);

					using(newBytes) {
						// clear previous memory since we replaced it
						this.ClearFileBytes();
						this.Filebytes.Entry = newBytes.Entry;
					}

					return result;
				}
			}, data);
		}

		protected void RunDbOperation(Action<LiteDBDAL> operation, object data = null) {
			lock(this.locker) {
				this.LazyLoad(data);

				this.RunNoLoadDbOperation(operation, data);
			}
		}

		protected T RunDbOperation<T>(Func<LiteDBDAL, T> operation, object data = null) {
			lock(this.locker) {
				this.LazyLoad(data);

				return this.RunNoLoadDbOperation(operation, data);
			}
		}

		protected T RunQueryDbOperation<T>(Func<LiteDBDAL, T> operation, object data = null) {
			return this.RunCryptoOperation(() => {
				lock(this.locker) {
					this.LazyLoad();

					return this.serialisationFal.RunQueryDbOperation(operation, this.Filebytes);
				}
			}, data);
		}

		private void LazyLoad(object data = null) {
			if(!this.IsLoaded) {
				this.Load(data);
			}
		}
	}
}