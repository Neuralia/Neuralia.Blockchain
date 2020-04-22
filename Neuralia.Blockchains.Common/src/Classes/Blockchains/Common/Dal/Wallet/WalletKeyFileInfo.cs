using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyFileInfo : SingleEntryWalletFileInfo<IWalletKey> {

		private readonly   IWalletAccount           account;
		protected readonly AccountPassphraseDetails accountPassphraseDetails;
		private readonly   KeyInfo                  keyInfo;
		private readonly   Type                     keyType;

		private IWalletKey key;

		public WalletKeyFileInfo(IWalletAccount account, string keyName, byte ordinalId, Type keyType, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, AccountPassphraseDetails accountPassphraseDetails, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {

			this.account                  = account;
			this.KeyName                  = keyName;
			this.OrdinalId                = ordinalId;
			this.keyType                  = keyType;
			this.accountPassphraseDetails = accountPassphraseDetails;
		}

		public async Task<IWalletKey> Key(LockContext lockContext) {

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.key == null) {
					//KeyData keyData = new KeyData(this.account.AccountUuid, this.KeyName);

					this.key = await LoadKey<IWalletKey>(account.AccountUuid, KeyName, handle).ConfigureAwait(false);

					//this.key = this.RunQueryDbOperation(((litedbDal, lc) => litedbDal.Any<IWalletKey>() ? litedbDal.GetSingle<IWalletKey>() : null, keyData);
				}

				return this.key;
			}
		}

		protected override async Task SaveFileBytes(LockContext lockContext, object data = null) {
			if(this.IsLoaded && this.hasChanged) {
				await base.SaveFileBytes(lockContext, data).ConfigureAwait(false);
			}

			this.hasChanged = false;
		}

		private         bool   hasChanged      = false;
		protected const string NEXT_KEY_SUFFIX = "NEXT";
		protected       string KeyTypeName     => this.keyType.Name;
		protected       string KeyTypeNextName => $"{this.KeyTypeName}{NEXT_KEY_SUFFIX}";

		public string KeyName { get; }

		public byte OrdinalId { get; }

		public async Task CreateEmptyFile(IWalletKey entry, IWalletKey nextKey, LockContext lockContext) {
			// ensure the key is of the expected type
			if(!this.keyType.IsInstanceOfType(entry)) {
				throw new ApplicationException($"Invalid key type. Not of expected type {this.keyType.FullName}");
			}

			await base.CreateEmptyFile(entry, lockContext).ConfigureAwait(false);

			if(nextKey != null) {
				await SetNextKey(nextKey, lockContext).ConfigureAwait(false);
			}

			this.hasChanged = true;
		}

		public override async Task CreateEmptyFile(IWalletKey entry, LockContext lockContext) {
			await CreateEmptyFile(entry, lockContext).ConfigureAwait(false);

			this.hasChanged = true;
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override async Task InsertNewDbData(IWalletKey key, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.key = key;

				KeyData keyData = new KeyData(key.AccountUuid, key.Name);

				await RunDbOperation((litedbDal, lc) => {
					litedbDal.Insert(key, KeyTypeName, c => c.Id);

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<IWalletKey, Guid>(i => i.Id);

			this.hasChanged = true;

			return Task.CompletedTask;
		}

		protected override async Task PrepareEncryptionInfo(LockContext lockContext) {
			await CreateSecurityDetails(lockContext).ConfigureAwait(false);

			this.hasChanged = true;
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.accountPassphraseDetails.EncryptWalletKeys;
				}

				if(this.EncryptionInfo.Encrypt) {
					if(!this.accountPassphraseDetails.KeyPassphraseValid(this.account.AccountUuid, this.KeyName)) {
						throw new ApplicationException("Encrypted wallet key does not have a valid passphrase");
					}

					this.EncryptionInfo.Secret = () => this.accountPassphraseDetails.KeyPassphrase(this.account.AccountUuid, this.KeyName).ConvertToUnsecureBytes();

					// get the parameters from the account
					this.EncryptionInfo.EncryptionParameters = this.account.Keys.Single(ki => ki.Name == this.KeyName).EncryptionParameters;

				}
			}

		}

		protected override Task UpdateDbEntry(LockContext lockContext) {
			// here we do nothing, since we dont have a single entry

			return Task.CompletedTask;
		}

		/// <summary>
		///     Convert any encryption error into key encryption error so we can remediate it.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="data"></param>
		/// <exception cref="KeyDecryptionException"></exception>
		/// <exception cref="WalletDecryptionException"></exception>
		protected override async Task RunCryptoOperation(Func<Task> action, object data = null) {
			// override the exception behavior to change the exception type
			try {
				await action().ConfigureAwait(false);
			} catch(DataEncryptionException dex) {
				if(data == null) {
					data = new KeyData(this.account.AccountUuid, this.KeyName);
				}

				if(data is KeyData keyData) {
					throw new KeyDecryptionException(keyData.AccountUuid, keyData.Name, dex);
				}

				throw new WalletDecryptionException(dex);
			}
		}

		/// <summary>
		///     Convert any encryption error into key encryption error so we can remediate it.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="data"></param>
		/// <exception cref="KeyDecryptionException"></exception>
		/// <exception cref="WalletDecryptionException"></exception>
		protected override async Task<U> RunCryptoOperation<U>(Func<Task<U>> action, object data = null) {
			// override the exception behavior to change the exception type

			try {
				return await action().ConfigureAwait(false);
			} catch(DataEncryptionException dex) {
				if(data == null) {
					data = new KeyData(this.account.AccountUuid, this.KeyName);
				}

				if(data is KeyData keyData) {
					throw new KeyDecryptionException(keyData.AccountUuid, keyData.Name, dex);
				}

				throw new WalletDecryptionException(dex);
			}
		}

		/// <summary>
		///     Load a key with a custom selector
		/// </summary>
		/// <param name="selector">select the key subset to selcet. make sure to make a copy, because the key will be disposed</param>
		/// <param name="accountUuid"></param>
		/// <param name="name"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name, LockContext lockContext)
			where T : class
			where K : IWalletKey {

			return this.LoadKey<K, T>(selector, accountUuid, name, false, lockContext);
		}

		public Task<T> LoadKey<T>(Guid accountUuid, string name, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountUuid, name, false, lockContext);
		}

		public Task<T> LoadNextKey<T>(Guid accountUuid, string name, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountUuid, name, true, lockContext);
		}

		protected Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name, bool nextKey, LockContext lockContext)
			where T : class
			where K : IWalletKey {

			KeyData keyData = new KeyData(accountUuid, name);

			return this.RunQueryDbOperation(async (litedbDal, lc) => {

				string keyName = nextKey ? this.KeyTypeNextName : this.KeyTypeName;

				if(!litedbDal.CollectionExists<K>(keyName)) {
					// ok, we did not find it. lets see if it has another name, and the type is assignable
					var collectionNames = litedbDal.GetCollectionNames();

					if(!collectionNames.Any()) {
						return null;
					}

					Type basicType  = typeof(K);
					bool foundMatch = false;

					if(nextKey) {
						collectionNames = collectionNames.Where(n => n.EndsWith(NEXT_KEY_SUFFIX)).ToList();
					} else {
						collectionNames = collectionNames.Where(n => !n.EndsWith(NEXT_KEY_SUFFIX)).ToList();
					}

					foreach(string collection in collectionNames) {

						// see if we can find the key matching type
						Type resultType = Assembly.GetAssembly(basicType).GetType($"{basicType.Namespace}.{collection}");

						// if the two are assignable, than we have a super class and we can assign it
						if(resultType != null && basicType.IsAssignableFrom(resultType)) {
							keyName    = collection;
							foundMatch = true;

							break;
						}
					}

					if(!foundMatch) {
						return null;
					}
				}

				K loadedKey = litedbDal.GetOne<K>(k => k.AccountUuid == accountUuid && k.Name == name, keyName);

				if(loadedKey == null) {
					throw new ApplicationException("Failed to load wallet key from file");
				}

				if(!nextKey) {
					this.key = loadedKey;
				}

				T result = selector(loadedKey);

				// if we selected a subset of the key, we dispose of the key
				if(!result.Equals(loadedKey)) {
					loadedKey.Dispose();
				}

				return result;
			}, lockContext, keyData);
		}

		public async Task UpdateKey(IWalletKey key, LockContext lockContext) {
			KeyData keyData = new KeyData(key.AccountUuid, key.Name);

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeName)) {
						litedbDal.Update(key, KeyTypeName);
					}

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		public async Task<bool> IsNextKeySet(LockContext lockContext) {
			var key = await Key(lockContext).ConfigureAwait(false);

			return (await this.LoadNextKey<IWalletKey>(key.AccountUuid, key.Name, lockContext).ConfigureAwait(false)) != null;
		}

		public async Task SetNextKey(IWalletKey nextKey, LockContext lockContext) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, nextKey.Name);

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {

					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeName)) {
						using IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => k.AccountUuid == nextKey.AccountUuid && k.KeyAddress.OrdinalId == nextKey.KeyAddress.OrdinalId, KeyTypeName);

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;

					} else {
						throw new ArgumentException("key does not exist");
					}

					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeNextName)) {
						if(litedbDal.Exists<IWalletKey>(k => k.AccountUuid == nextKey.AccountUuid && k.KeyAddress.OrdinalId == nextKey.KeyAddress.OrdinalId, KeyTypeNextName)) {
							throw new ApplicationException("A key is already set to be our next key. Since it may already be promised, we can not overwrite it.");
						}
					}

					litedbDal.Insert(nextKey, KeyTypeNextName, k => k.Id);

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		public async Task UpdateNextKey(KeyInfo keyInfo, IWalletKey nextKey, LockContext lockContext) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, keyInfo.Name);

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {

					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeName)) {
						using IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => k.AccountUuid == nextKey.AccountUuid && k.KeyAddress.OrdinalId == keyInfo.Ordinal, KeyTypeName);

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;

					} else {
						throw new ArgumentException("key does not exist");
					}

					bool insert = false;

					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeNextName)) {
						if(litedbDal.Exists<IWalletKey>(k => k.AccountUuid == nextKey.AccountUuid && k.KeyAddress.OrdinalId == keyInfo.Ordinal, KeyTypeNextName)) {

							litedbDal.Update(nextKey, KeyTypeNextName);
						} else {
							insert = true;
						}
					} else {
						insert = true;
					}

					if(insert) {
						litedbDal.Insert(nextKey, KeyTypeNextName, k => k.Id);
					}

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		public async Task SwapNextKey(KeyInfo keyInfo, Guid accountUuid, LockContext lockContext) {
			KeyData keyData = new KeyData(accountUuid, keyInfo.Name);

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {

					if(litedbDal.CollectionExists<IWalletKey>(KeyTypeName)) {
						litedbDal.Remove<IWalletKey>(k => k.AccountUuid == accountUuid && k.Name == keyInfo.Name, KeyTypeName);
					}

					using(IWalletKey nextKey = litedbDal.GetOne<IWalletKey>(k => k.AccountUuid == accountUuid && k.KeyAddress.OrdinalId == keyInfo.Ordinal, KeyTypeNextName)) {

						litedbDal.Insert(nextKey, KeyTypeName, k => k.Id);
					}

					litedbDal.Remove<IWalletKey>(k => k.AccountUuid == accountUuid && k.Name == keyInfo.Name, KeyTypeNextName);

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		protected class KeyData {
			public          Guid   AccountUuid;
			public readonly string Name;

			public KeyData(Guid accountUuid, string name) {
				this.AccountUuid = accountUuid;
				this.Name        = name;
			}
		}
	}
}