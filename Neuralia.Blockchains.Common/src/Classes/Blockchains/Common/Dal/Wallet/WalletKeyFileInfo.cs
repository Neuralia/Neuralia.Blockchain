using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
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
		protected const string NEXT_KEY_SUFFIX = "NEXT";

		public const string DB_DAL_KEY = "KEYS";

		private readonly IWalletAccount account;
		protected readonly AccountPassphraseDetails accountPassphraseDetails;
		private readonly KeyInfo keyInfo;
		private readonly Type keyType;

		private bool hasChanged;

		private IWalletKey key;

		public WalletKeyFileInfo(IWalletAccount account, string keyName, byte ordinalId, Type keyType, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, AccountPassphraseDetails accountPassphraseDetails, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {

			this.account = account;
			this.KeyName = keyName;
			this.OrdinalId = ordinalId;
			this.keyType = keyType;
			this.accountPassphraseDetails = accountPassphraseDetails;
		}

		protected string KeyTypeName => this.keyType.Name;
		protected string KeyTypeNextName => $"{this.KeyTypeName}{NEXT_KEY_SUFFIX}";

		public string KeyName { get; }

		public byte OrdinalId { get; }

		public async Task<IWalletKey> Key(LockContext lockContext) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.key == null) {
					//KeyData keyData = new KeyData(this.account.AccountCode, this.KeyName);

					this.key = await this.LoadKey<IWalletKey>(this.account.AccountCode, this.KeyName, handle).ConfigureAwait(false);

					//this.key = this.RunQueryDbOperation(((dbdal, lc) => dbdal.Any<IWalletKey>() ? dbdal.GetSingle<IWalletKey>() : null, keyData);
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

		public async Task CreateEmptyFile(IWalletKey entry, IWalletKey nextKey, LockContext lockContext) {
			// ensure the key is of the expected type
			if(!this.keyType.IsInstanceOfType(entry)) {
				throw new ApplicationException($"Invalid key type. Not of expected type {this.keyType.FullName}");
			}

			await base.CreateEmptyFile(entry, lockContext).ConfigureAwait(false);

			if(nextKey != null) {
				await this.SetNextKey(nextKey, lockContext).ConfigureAwait(false);
			}

			this.hasChanged = true;
		}

		protected override IWalletKey CreateEntryType() {
			// we will never call this directly
			throw new NotImplementedException();
		}

		protected override string DBDalKey => DB_DAL_KEY;

		public override Task CreateEmptyFile(LockContext lockContext, object data = null) {
			// with keys, we do nothing here. the explicit overload will be called explicitely
			return Task.CompletedTask;
		}

		public override async Task CreateEmptyFile(IWalletKey entry, LockContext lockContext) {
			await base.CreateEmptyFile(entry, lockContext).ConfigureAwait(false);

			this.hasChanged = true;
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override async Task InsertNewDbData(IWalletKey key, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.key = key;

				KeyData keyData = new KeyData(key.AccountCode, key.Name);

				await this.RunDbOperation((dbdal, lc) => {
					dbdal.Insert(key, this.KeyTypeName, c => c.Id);

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<IWalletKey, Guid>(i => i.Id);

			this.hasChanged = true;

			return Task.CompletedTask;
		}

		protected override async Task PrepareEncryptionInfo(LockContext lockContext) {
			await this.CreateSecurityDetails(lockContext).ConfigureAwait(false);

			this.hasChanged = true;
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.accountPassphraseDetails.EncryptWalletKeys;
				}

				if(this.EncryptionInfo.Encrypt) {
					if(!this.accountPassphraseDetails.KeyPassphraseValid(this.account.AccountCode, this.KeyName)) {
						throw new ApplicationException("Encrypted wallet key does not have a valid passphrase");
					}

					this.EncryptionInfo.SecretHandler = () => this.accountPassphraseDetails.KeyPassphrase(this.account.AccountCode, this.KeyName).ConvertToUnsecureBytes();

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
					data = new KeyData(this.account.AccountCode, this.KeyName);
				}

				if(data is KeyData keyData) {
					throw new KeyDecryptionException(keyData.AccountCode, keyData.Name, dex);
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
					data = new KeyData(this.account.AccountCode, this.KeyName);
				}

				if(data is KeyData keyData) {
					throw new KeyDecryptionException(keyData.AccountCode, keyData.Name, dex);
				}

				throw new WalletDecryptionException(dex);
			}
		}

		/// <summary>
		///     Load a key with a custom selector
		/// </summary>
		/// <param name="selector">select the key subset to selcet. make sure to make a copy, because the key will be disposed</param>
		/// <param name="accountCode"></param>
		/// <param name="name"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, string name, LockContext lockContext)
			where T : class
			where K : IWalletKey {

			return this.LoadKey(selector, accountCode, name, false, lockContext);
		}

		public Task<T> LoadKey<T>(string accountCode, string name, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountCode, name, false, lockContext);
		}

		public Task<T> LoadNextKey<T>(string accountCode, string name, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountCode, name, true, lockContext);
		}

		protected Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, string name, bool nextKey, LockContext lockContext)
			where T : class
			where K : IWalletKey {

			KeyData keyData = new KeyData(accountCode, name);

			return this.RunQueryDbOperation(async (dbdal, lc) => {

				string keyName = nextKey ? this.KeyTypeNextName : this.KeyTypeName;

				if(!dbdal.CollectionExists<K>(keyName)) {
					// ok, we did not find it. lets see if it has another name, and the type is assignable
					List<string> collectionNames = dbdal.GetCollectionNames();

					if(!collectionNames.Any()) {
						return null;
					}

					Type basicType = typeof(K);
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
						if((resultType != null) && basicType.IsAssignableFrom(resultType)) {
							keyName = collection;
							foundMatch = true;

							break;
						}
					}

					if(!foundMatch) {
						return null;
					}
				}

				K loadedKey = dbdal.GetOne<K>(k => (k.AccountCode == accountCode) && (k.Name == name), keyName);

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
			KeyData keyData = new KeyData(key.AccountCode, key.Name);

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						dbdal.Update(key, this.KeyTypeName);
					}

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		public async Task<bool> IsNextKeySet(LockContext lockContext) {
			IWalletKey key = await this.Key(lockContext).ConfigureAwait(false);

			return await this.LoadNextKey<IWalletKey>(key.AccountCode, key.Name, lockContext).ConfigureAwait(false) != null;
		}

		public Task SetNextKey(KeyInfo keyInfo, IWalletKey nextKey, LockContext lockContext) {

			return SetNextKey(nextKey.AccountCode, keyInfo.Name, nextKey, lockContext);
		}

		public Task SetNextKey(IWalletKey nextKey, LockContext lockContext) {
			
			return SetNextKey(nextKey.AccountCode, nextKey.Name, nextKey, lockContext);
		}

		public async Task SetNextKey(string accountCode, string keyName, IWalletKey nextKey, LockContext lockContext) {

			if(string.IsNullOrWhiteSpace(accountCode)) {
				throw new ArgumentException("Key account code is not set", nameof(accountCode));
			}
			if(string.IsNullOrWhiteSpace(keyName)) {
				throw new ArgumentException("Key name is not set", nameof(keyName));
			}
			if(nextKey.KeyAddress == null || nextKey.KeyAddress.OrdinalId == 0) {
				throw new ArgumentException("Key ordinal is not set", nameof(nextKey.KeyAddress.OrdinalId));
			}
			KeyData keyData = new KeyData(accountCode, keyName);

			Expression<Func<IWalletKey, bool>> predicate = k => k.AccountCode == nextKey.AccountCode && k.KeyAddress.OrdinalId == nextKey.KeyAddress.OrdinalId;
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {

					if(dbdal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						if(!dbdal.Exists(predicate, this.KeyTypeName)){
							throw new ArgumentException("key does not exist");
						}
						using IWalletKey currentKey = dbdal.GetOne(predicate, this.KeyTypeName);
						
						if(nextKey.KeyAddress.KeyUseIndex.KeyUseSequenceId != currentKey.Index.KeyUseSequenceId+1 || nextKey.KeyAddress.KeyUseIndex.KeyUseIndex != 0) {
							throw new ArgumentException("key next key does not have the next sequence Id set");
						}

					} else {
						throw new ArgumentException("key does not exist");
					}

					bool insert = false;

					if(dbdal.CollectionExists<IWalletKey>(this.KeyTypeNextName)) {
						if(dbdal.Exists(predicate, this.KeyTypeNextName)) {

							using IWalletKey currentNextKey = dbdal.GetOne(predicate, this.KeyTypeNextName);
							nextKey.Id = currentNextKey.Id;
							
							dbdal.Update(nextKey, this.KeyTypeNextName);
						} else {
							insert = true;
						}
					} else {
						insert = true;
					}

					if(insert) {
						dbdal.Insert(nextKey, this.KeyTypeNextName, k => k.Id);
					}

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}

		public async Task SwapNextKey(KeyInfo keyInfo, string accountCode, LockContext lockContext) {
			KeyData keyData = new KeyData(accountCode, keyInfo.Name);

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {

					if(dbdal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						dbdal.Remove<IWalletKey>(k => (k.AccountCode == accountCode) && (k.Name == keyInfo.Name), this.KeyTypeName);
					}

					using(IWalletKey nextKey = dbdal.GetOne<IWalletKey>(k => (k.AccountCode == accountCode) && (k.KeyAddress.OrdinalId == keyInfo.Ordinal), this.KeyTypeNextName)) {

						dbdal.Insert(nextKey, this.KeyTypeName, k => k.Id);
					}

					dbdal.Remove<IWalletKey>(k => (k.AccountCode == accountCode) && (k.Name == keyInfo.Name), this.KeyTypeNextName);

					return Task.CompletedTask;
				}, handle, keyData).ConfigureAwait(false);

				this.hasChanged = true;
			}
		}
		
		public override async Task Reset(LockContext lockContext) {
			await base.Reset(lockContext).ConfigureAwait(false);

			this.ClearCached(lockContext);
		}
		
		public override void ClearCached(LockContext lockContext) {
			base.ClearCached(lockContext);
			
			this.key = null;
		}

		protected class KeyData {
			public readonly string Name;
			public string AccountCode;

			public KeyData(string accountCode, string name) {
				this.AccountCode = accountCode;
				this.Name = name;
			}
		}
	}
}