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
						KeyData keyData = new KeyData(this.account.AccountUuid, this.KeyName);
						this.key = this.RunQueryDbOperation(litedbDal => litedbDal.Any<IWalletKey>() ? litedbDal.GetSingle<IWalletKey>() : null, keyData);
					}
				}

				return this.key;
			}
		}

		protected string KeyTypeName => this.keyType.Name;

		public bool IsNextKeySet => this.Key?.NextKey != null;

		public string KeyName { get; }

		public byte OrdinalId { get; }

		public override void CreateEmptyFile(IWalletKey entry) {

			// ensure the key is of the expected type
			if(!this.keyType.IsInstanceOfType(entry)) {
				throw new ApplicationException($"Invalid key type. Not of expected type {this.keyType.FullName}");
			}

			base.CreateEmptyFile(entry);
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
					throw new KeyDecryptionException(keyData.accountUuid, keyData.name, dex);
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
					throw new KeyDecryptionException(keyData.accountUuid, keyData.name, dex);
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

			KeyData keyData = new KeyData(accountUuid, name);

			return this.RunQueryDbOperation(litedbDal => {

				string keyName = this.KeyTypeName;

				if(!litedbDal.CollectionExists<K>()) {
					// ok, we did not find it. lets see if it has another name, and the type is assignable
					var collectionNames = litedbDal.GetCollectionNames();

					if(!collectionNames.Any()) {
						return null;
					}

					Type basicType = typeof(K);
					bool foundMatch = false;

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

				T result = selector(loadedKey);

				// if we selected a subset of the key, we dispose of the key
				if(!result.Equals(loadedKey)) {
					loadedKey.Dispose();
				}

				return result;
			}, keyData);
		}

		public void SetNextKey(KeyInfo keyInfo, IWalletKey nextKey) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, nextKey.Name);

			this.RunDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
					using(IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => k.KeyAddress.OrdinalId == keyInfo.Ordinal, this.KeyTypeName)) {

						if(currentKey.NextKey != null) {
							throw new ApplicationException("A key is already set to be our next key. Since it may already be promised, we can not overwrite it.");
						}

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;

						currentKey.NextKey = nextKey;

						litedbDal.Update(currentKey, this.KeyTypeName);
					}
				}

			}, keyData);

			this.SaveFile(false, keyData);
		}

		public void UpdateNextKey(KeyInfo keyInfo, IWalletKey nextKey) {

			KeyData keyData = new KeyData(nextKey.AccountUuid, nextKey.Name);

			this.RunDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
					using(IWalletKey currentKey = litedbDal.GetOne<IWalletKey>(k => k.KeyAddress.OrdinalId == keyInfo.Ordinal, this.KeyTypeName)) {

						// increment the sequence
						nextKey.KeySequenceId = currentKey.KeySequenceId + 1;

						currentKey.NextKey = nextKey;

						litedbDal.Update(currentKey, this.KeyTypeName);
					}
				}

			}, keyData);

			this.SaveFile(false, keyData);

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

		public void SwapNextKey(IWalletKey key) {
			KeyData keyData = new KeyData(key.AccountUuid, key.Name);

			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					IWalletKey nextKey = key.NextKey;

					if(litedbDal.CollectionExists<IWalletKey>(this.KeyTypeName)) {
						litedbDal.Remove<IWalletKey>(k => (k.AccountUuid == key.AccountUuid) && (k.Name == key.Name), this.KeyTypeName);
					}

					litedbDal.Insert(nextKey, this.KeyTypeName, k => k.Id);

					key.Dispose();
					nextKey.Dispose();
				}, keyData);

				this.SaveFile(false, keyData);
			}

		}

		protected class KeyData {
			public Guid accountUuid;
			public string name;

			public KeyData(Guid accountUuid, string name) {
				this.accountUuid = accountUuid;
				this.name = name;
			}
		}
	}
}