using System;
using System.Linq;
using System.Reflection;
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

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyFileInfo : SingleEntryWalletFileInfo<IWalletKey> {

		private readonly IWalletAccount account;
		protected readonly AccountPassphraseDetails accountPassphraseDetails;
		private readonly KeyInfo keyInfo;
		private readonly Type keyType;

		private IWalletKey key;

		public WalletKeyFileInfo(IWalletAccount account, string keyName, byte ordinalId, Type keyType, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, AccountPassphraseDetails accountPassphraseDetails, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {

			this.account = account;
			this.KeyName = keyName;
			this.OrdinalId = ordinalId;
			this.keyType = keyType;
			this.accountPassphraseDetails = accountPassphraseDetails;
		}

		public IWalletKey Key {
			get {
				lock(this.locker) {
					if(this.key == null) {
						//KeyData keyData = new KeyData(this.account.AccountUuid, this.KeyName);

						this.key = this.LoadKey<IWalletKey>(this.account.AccountUuid, this.KeyName);
						//this.key = this.RunQueryDbOperation(litedbDal => litedbDal.Any<IWalletKey>() ? litedbDal.GetSingle<IWalletKey>() : null, keyData);
					}
				}

				return this.key;
			}
		}

		protected const string NEXT_KEY_SUFFIX = "-NEXT";
		protected string KeyTypeName => this.keyType.Name;
		protected string KeyTypeNextName => $"{this.KeyTypeName}{NEXT_KEY_SUFFIX}";
		

		public string KeyName { get; }

		public byte OrdinalId { get; }

		public void CreateEmptyFile(IWalletKey entry, IWalletKey nextKey) {
			// ensure the key is of the expected type
			if(!this.keyType.IsInstanceOfType(entry)) {
				throw new ApplicationException($"Invalid key type. Not of expected type {this.keyType.FullName}");
			}

			base.CreateEmptyFile(entry);

			if(nextKey != null) {
				this.SetNextKey(nextKey);
			}
		}

		public override void CreateEmptyFile(IWalletKey entry) {
			this.CreateEmptyFile(entry, null);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(IWalletKey key) {
			lock(this.locker) {
				this.key = key;

				KeyData keyData = new KeyData(key.AccountUuid, key.Name);

				this.RunDbOperation(litedbDal => {
					litedbDal.Insert(key, this.KeyTypeName, c => c.Id);
				}, keyData);
			}
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<IWalletKey, Guid>(i => i.Id);
		}

		protected override void PrepareEncryptionInfo() {
			this.CreateSecurityDetails();
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
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

		protected override void UpdateDbEntry() {
			// here we do nothing, since we dont have a single entry
		}

		/// <summary>
		///     Convert any encryption error into key encryption error so we can remediate it.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="data"></param>
		/// <exception cref="KeyDecryptionException"></exception>
		/// <exception cref="WalletDecryptionException"></exception>
		protected override void RunCryptoOperation(Action action, object data = null) {
			// override the exception behavior to change the exception type
			try {
				action();
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
		protected override U RunCryptoOperation<U>(Func<U> action, object data = null) {
			// override the exception behavior to change the exception type

			try {
				return action();
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
		public T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name)
			where T : class
			where K : IWalletKey {

			return this.LoadKey<K, T>(selector, accountUuid, name, false);
		}
		
		public T LoadKey<T>(Guid accountUuid, string name)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountUuid, name, false);
		}

		public T LoadNextKey<T>(Guid accountUuid, string name)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(e => e, accountUuid, name, true);
		}
		
		protected T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name, bool nextKey)
			where T : class
			where K : IWalletKey {

			KeyData keyData = new KeyData(accountUuid, name);

			return this.RunQueryDbOperation(litedbDal => {

				string keyName = nextKey?this.KeyTypeNextName:this.KeyTypeName;

				if(!litedbDal.CollectionExists<K>(keyName)) {
					// ok, we did not find it. lets see if it has another name, and the type is assignable
					var collectionNames = litedbDal.GetCollectionNames();

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

				K loadedKey = litedbDal.GetOne<K>(k => (k.AccountUuid == accountUuid) && (k.Name == name), keyName);

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
			}, keyData);
		}
		
		
		public void UpdateKey(IWalletKey key) {
			KeyData keyData = new KeyData(key.AccountUuid, key.Name);

			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						litedbDal.Update(key, this.KeyTypeName);
					}
				}, keyData);

				this.SaveFile(false, keyData);
			}
		}

		public bool IsNextKeySet => this.LoadNextKey<IWalletKey>(this.Key.AccountUuid, this.Key.Name) != null;

		public void SetNextKey(IWalletKey nextKey) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, nextKey.Name);

			this.RunDbOperation(litedbDal => {

				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
					using(IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => (k.AccountUuid == nextKey.AccountUuid) && k.KeyAddress.OrdinalId == nextKey.KeyAddress.OrdinalId, this.KeyTypeName)) {

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;
					}
				} else {
					throw new ArgumentException("key does not exist");
				}
				
				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeNextName)) {
					if(litedbDal.Exists<IWalletKey>(k => (k.AccountUuid == nextKey.AccountUuid) && k.KeyAddress.OrdinalId == nextKey.KeyAddress.OrdinalId, this.KeyTypeNextName)) {
						throw new ApplicationException("A key is already set to be our next key. Since it may already be promised, we can not overwrite it.");
					}
				}

				litedbDal.Insert(nextKey, this.KeyTypeNextName, k => k.Id);
			}, keyData);

			this.SaveFile(false, keyData);
		}

		public void UpdateNextKey(KeyInfo keyInfo, IWalletKey nextKey) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, keyInfo.Name);

			this.RunDbOperation(litedbDal => {

				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
					using(IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => (k.AccountUuid == nextKey.AccountUuid) && k.KeyAddress.OrdinalId == keyInfo.Ordinal, this.KeyTypeName)) {

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;
					}
				} else {
					throw new ArgumentException("key does not exist");
				}

				bool insert = false;
				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeNextName)) {
					if(litedbDal.Exists<IWalletKey>(k => (k.AccountUuid == nextKey.AccountUuid) && k.KeyAddress.OrdinalId == keyInfo.Ordinal, this.KeyTypeNextName)) {
						
						litedbDal.Update(nextKey, this.KeyTypeNextName);
					} else {
						insert = true;
					}
				} else {
					insert = true;
				}
				
				if(insert) {
					litedbDal.Insert(nextKey, this.KeyTypeNextName, k => k.Id);
				}

			}, keyData);

			this.SaveFile(false, keyData);
		}

		public void SwapNextKey( KeyInfo keyInfo,  Guid accountUuid) {
			KeyData keyData = new KeyData(accountUuid, keyInfo.Name);

			lock(this.locker) {
				this.RunDbOperation(litedbDal => {

					if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						litedbDal.Remove<IWalletKey>(k => (k.AccountUuid == accountUuid) && (k.Name == keyInfo.Name), this.KeyTypeName);
					}
					
					using(IWalletKey nextKey = litedbDal.GetOne<IWalletKey>(k =>  (k.AccountUuid == accountUuid) && k.KeyAddress.OrdinalId == keyInfo.Ordinal, this.KeyTypeNextName)) {
						
						litedbDal.Insert(nextKey, this.KeyTypeName, k => k.Id);
					}
					
					litedbDal.Remove<IWalletKey>(k => (k.AccountUuid == accountUuid) && (k.Name == keyInfo.Name), this.KeyTypeNextName);
					
				}, keyData);

				this.SaveFile(false, keyData);
			}

		}

		protected class KeyData {
			public Guid AccountUuid;
			public readonly string Name;

			public KeyData(Guid accountUuid, string name) {
				this.AccountUuid = accountUuid;
				this.Name = name;
			}
		}
	}
}