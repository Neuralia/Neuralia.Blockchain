using System;
using System.Collections.Generic;
using System.IO;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IUserWalletFileInfo : ISingleEntryWalletFileInfo {
		IUserWallet WalletBase { get; }

		Dictionary<Guid, IAccountFileInfo> Accounts { get; }

		void ChangeKeysEncryption();

		/// <summary>
		///     force a full load of all components of the wallet
		/// </summary>
		void LoadComplete(bool includeWalletItems, bool includeKeys);

		/// <summary>
		///     Load the security details from the wallet file
		/// </summary>
		/// <exception cref="ApplicationException"></exception>
		void LoadFileSecurityDetails();

		void CreateEmptyFileBase(IUserWallet entry);
	}

	public interface IUserWalletFileInfo<ENTRY_TYPE> : ISingleEntryWalletFileInfo
		where ENTRY_TYPE : class, IUserWallet {
		void CreateEmptyFile(ENTRY_TYPE entry);
	}

	public abstract class UserWalletFileInfo<ENTRY_TYPE> : SingleEntryWalletFileInfo<ENTRY_TYPE>, IUserWalletFileInfo<ENTRY_TYPE>
		where ENTRY_TYPE : UserWallet {

		public readonly Dictionary<Guid, IAccountFileInfo> accounts = new Dictionary<Guid, IAccountFileInfo>();
		private readonly string walletCryptoFile;

		private ENTRY_TYPE wallet;

		public UserWalletFileInfo(string filename, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, serviceSet, serialisationFal, walletSecurityDetails) {

			this.walletCryptoFile = this.serialisationFal.GetWalletCryptoFilePath();
		}

		public Dictionary<Guid, IAccountFileInfo> Accounts => this.accounts;

		public IUserWallet WalletBase => this.Wallet;

		public ENTRY_TYPE Wallet {
			get {
				lock(this.locker) {
					if(this.wallet == null) {
						this.wallet = this.RunQueryDbOperation(litedbDal => litedbDal.CollectionExists<ENTRY_TYPE>() ? litedbDal.GetSingle<ENTRY_TYPE>() : null);
					}
				}

				return this.wallet;
			}
		}

		public override void Save(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					lock(this.locker) {
						base.Save();

						foreach(IAccountFileInfo account in this.accounts.Values) {
							account.Save();
						}
					}
				}
			}, data);
		}

		public override void Reset() {
			lock(this.locker) {
				base.Reset();

				foreach(IAccountFileInfo account in this.accounts.Values) {
					account.Reset();
				}

				this.wallet = null;
			}
		}

		public override void ReloadFileBytes(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					lock(this.locker) {
						base.ReloadFileBytes();

						foreach(IAccountFileInfo account in this.accounts.Values) {
							account.ReloadFileBytes();
						}
					}
				}
			}, data);
		}

		public override void ChangeEncryption(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					base.ChangeEncryption(data);

					if(!this.WalletSecurityDetails.EncryptWallet) {
						// ltes delete the crypto file
						this.serialisationFal.TransactionalFileSystem.FileDelete(this.walletCryptoFile);
					} else {
						IByteArray edata = this.EncryptionInfo.encryptionParameters.Dehydrate();
						this.serialisationFal.TransactionalFileSystem.OpenWrite(this.walletCryptoFile, edata);
					}

					//now the attached files
					foreach(IAccountFileInfo account in this.accounts.Values) {
						account.ChangeEncryption();
					}
				}
			}, data);

		}

		public override void CreateEmptyFile(ENTRY_TYPE entry) {
			lock(this.locker) {
				this.CreateSecurityDetails();

				if(this.EncryptionInfo.encrypt) {
					bool walletCryptoFileExists = this.serialisationFal.TransactionalFileSystem.FileExists(this.walletCryptoFile);

					if(walletCryptoFileExists) {
						throw new FileNotFoundException("A wallet crypto file exist. we will not overwrite an unknown keys file");
					}
				}

				base.CreateEmptyFile(entry);

				if(this.EncryptionInfo.encrypt) {

					// no need to overwrite this every time. write only if it does not exist
					IByteArray data = this.EncryptionInfo.encryptionParameters.Dehydrate();

					// write this unencrypted
					this.serialisationFal.TransactionalFileSystem.OpenWrite(this.walletCryptoFile, data);

				}
			}
		}

		public void ChangeKeysEncryption() {
			lock(this.locker) {
				foreach(IAccountFileInfo account in this.accounts.Values) {
					foreach(WalletKeyFileInfo key in account.WalletKeysFileInfo.Values) {
						key.ChangeEncryption();
					}
				}
			}
		}

		/// <summary>
		///     force a full load of all components of the wallet
		/// </summary>
		public void LoadComplete(bool includeWalletItems, bool includeKeys) {
			lock(this.locker) {
				foreach(IAccountFileInfo account in this.accounts.Values) {
					if(includeWalletItems) {
						account.Load();
					}

					if(includeKeys) {
						foreach(WalletKeyFileInfo key in account.WalletKeysFileInfo.Values) {
							key.Load();
						}
					}
				}
			}
		}

		public void CreateEmptyFileBase(IUserWallet entry) {
			if(entry is ENTRY_TYPE entryType) {
				this.CreateEmptyFile(entryType);
			} else {
				throw new InvalidCastException();
			}
		}

		protected override void LoadFileBytes(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					this.Filebytes = this.serialisationFal.LoadFile(this.Filename, this.EncryptionInfo, true);
				}
			}, data);
		}

		protected override void SaveFileBytes(object data = null) {
			this.RunCryptoOperation(() => {
				lock(this.locker) {
					this.serialisationFal.SaveFile(this.Filename, this.Filebytes, this.EncryptionInfo, true);
				}
			}, data);
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			lock(this.locker) {
				litedbDal.CreateDbFile<ENTRY_TYPE, Guid>(i => i.Id);
			}
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(ENTRY_TYPE wallet) {
			lock(this.locker) {
				this.wallet = wallet;

				this.RunDbOperation(litedbDal => {
					litedbDal.Insert(wallet, c => c.Id);
				});
			}
		}

		protected override void PrepareEncryptionInfo() {
			lock(this.locker) {
				if(this.EncryptionInfo == null) {
					this.LoadFileSecurityDetails();
				}
			}
		}

		/// <summary>
		///     Load the security details from the wallet file
		/// </summary>
		/// <exception cref="ApplicationException"></exception>
		public void LoadFileSecurityDetails() {
			lock(this.locker) {
				this.EncryptionInfo = new EncryptionInfo();

				this.WalletSecurityDetails.EncryptWallet = this.serialisationFal.IsFileWalleteEncrypted();

				if(this.WalletSecurityDetails.EncryptWallet) {

					this.EncryptionInfo.encrypt = true;

					if(!this.serialisationFal.TransactionalFileSystem.FileExists(this.walletCryptoFile)) {
						throw new ApplicationException("The wallet crypto file does not exist. Impossible to load encrypted wallet.");
					}

					this.EncryptionInfo.Secret = () => this.WalletSecurityDetails.WalletPassphraseBytes;

					ByteArray cryptoParameterBytes = this.serialisationFal.TransactionalFileSystem.ReadAllBytes(this.walletCryptoFile);
					this.EncryptionInfo.encryptionParameters = EncryptorParameters.RehydrateEncryptor(cryptoParameterBytes);
				}
			}
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
				this.EncryptionInfo = new EncryptionInfo();

				this.EncryptionInfo.encrypt = this.WalletSecurityDetails.EncryptWallet;

				if(this.EncryptionInfo.encrypt) {
					if(!this.WalletSecurityDetails.WalletPassphraseValid) {
						throw new ApplicationException("Encrypted wallet does not have a valid passphrase");
					}

					this.EncryptionInfo.Secret = () => this.WalletSecurityDetails.WalletPassphraseBytes;

					this.EncryptionInfo.encryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				}
			}

		}

		protected override void UpdateDbEntry() {
			this.RunDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<ENTRY_TYPE>()) {
					litedbDal.Update(this.Wallet);
				}
			});

		}
	}
}