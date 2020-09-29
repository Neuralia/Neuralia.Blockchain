using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IUserWalletFileInfo : ISingleEntryWalletFileInfo {

		Dictionary<string, IAccountFileInfo> Accounts { get; }

		string WalletPath { get; }
		Task<IUserWallet> WalletBase(LockContext lockContext);

		Task ChangeKeysEncryption(LockContext lockContext);

		/// <summary>
		///     force a full load of all components of the wallet
		/// </summary>
		Task LoadComplete(bool includeWalletItems, bool includeKeys, LockContext lockContext);

		/// <summary>
		///     Load the security details from the wallet file
		/// </summary>
		/// <exception cref="ApplicationException"></exception>
		Task LoadFileSecurityDetails(LockContext lockContext);

		Task CreateEmptyFileBase(IUserWallet entry, LockContext lockContext);
	}

	public interface IUserWalletFileInfo<ENTRY_TYPE> : ISingleEntryWalletFileInfo
		where ENTRY_TYPE : class, IUserWallet {
		Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext);
	}

	public abstract class UserWalletFileInfo<ENTRY_TYPE> : SingleEntryWalletFileInfo<ENTRY_TYPE>, IUserWalletFileInfo<ENTRY_TYPE>
		where ENTRY_TYPE : UserWallet {

		public readonly Dictionary<string, IAccountFileInfo> accounts = new Dictionary<string, IAccountFileInfo>();
		private readonly string walletCryptoFile;

		private ENTRY_TYPE wallet;

		public UserWalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {

			this.walletCryptoFile = this.serialisationFal.GetWalletCryptoFilePath();
		}

		public Dictionary<string, IAccountFileInfo> Accounts => this.accounts;

		public string WalletPath => this.serialisationFal.GetWalletFolderPath();

		protected override ENTRY_TYPE CreateEntryType() {
			return default;
		}
		
		public override Task CreateEmptyFile(LockContext lockContext, object data = null) {
			
			// we will call an explicit external insert call
			return Task.CompletedTask;
		}
		
		public override Task Save(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await base.Save(handle).ConfigureAwait(false);

					foreach(IAccountFileInfo account in this.accounts.Values) {
						await account.Save(handle).ConfigureAwait(false);
					}
				}
			}, data);
		}

		public override async Task Reset(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await base.Reset(handle).ConfigureAwait(false);

				foreach(IAccountFileInfo account in this.accounts.Values) {
					await account.Reset(handle).ConfigureAwait(false);
				}

				this.wallet = null;
			}
		}

		public override Task ReloadFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await base.ReloadFileBytes(handle).ConfigureAwait(false);

					foreach(IAccountFileInfo account in this.accounts.Values) {
						await account.ReloadFileBytes(handle).ConfigureAwait(false);
					}
				}

			}, data);
		}
		
		public override void ClearCached(LockContext lockContext) {
			this.RunCryptoOperation(async () => {
				using(LockHandle handle = this.locker.Lock(lockContext)) {
					base.ClearCached(handle);

					foreach(IAccountFileInfo account in this.accounts.Values) {
						account.ClearCached(handle);
					}
				}

			}, null).WaitAndUnwrapException();
		}

		public override Task ChangeEncryption(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await base.ChangeEncryption(handle, data).ConfigureAwait(false);

					if(!this.WalletSecurityDetails.EncryptWallet) {
						// ltes delete the crypto file
						this.serialisationFal.TransactionalFileSystem.FileDelete(this.walletCryptoFile);
					} else {
						SafeArrayHandle edata = this.EncryptionInfo.EncryptionParameters.Dehydrate();
						await serialisationFal.TransactionalFileSystem.OpenWriteAsync(walletCryptoFile, edata).ConfigureAwait(false);
					}

					//now the attached files
					foreach(IAccountFileInfo account in this.accounts.Values) {
						await account.ChangeEncryption(handle).ConfigureAwait(false);
					}
				}
			}, data);

		}

		public override async Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.CreateSecurityDetails(handle).ConfigureAwait(false);

				if(this.EncryptionInfo.Encrypt) {
					bool walletCryptoFileExists = this.serialisationFal.TransactionalFileSystem.FileExists(this.walletCryptoFile);

					if(walletCryptoFileExists) {
						throw new FileNotFoundException("A wallet crypto file exist. we will not overwrite an unknown keys file");
					}
				}

				await base.CreateEmptyFile(entry, handle).ConfigureAwait(false);

				if(this.EncryptionInfo.Encrypt) {

					// no need to overwrite this every time. write only if it does not exist
					SafeArrayHandle data = this.EncryptionInfo.EncryptionParameters.Dehydrate();

					// write this unencrypted
					await serialisationFal.TransactionalFileSystem.OpenWriteAsync(walletCryptoFile, data).ConfigureAwait(false);

				}
			}
		}

		public async Task<IUserWallet> WalletBase(LockContext lockContext) {
			return await this.Wallet(lockContext).ConfigureAwait(false);
		}

		public async Task<ENTRY_TYPE> Wallet(LockContext lockContext) {

			using(LockHandle handle = await locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.wallet == null) {
					this.wallet = await this.RunQueryDbOperation((litedbDal, lc) => Task.FromResult(litedbDal.CollectionExists<ENTRY_TYPE>() ? litedbDal.GetSingle<ENTRY_TYPE>() : null), handle).ConfigureAwait(false);
				}
			}

			return this.wallet;
		}

		public async Task ChangeKeysEncryption(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				foreach(IAccountFileInfo account in this.accounts.Values) {
					foreach(WalletKeyFileInfo key in account.WalletKeysFileInfo.Values) {
						await key.ChangeEncryption(handle).ConfigureAwait(false);
					}
				}
			}
		}

		/// <summary>
		///     force a full load of all components of the wallet
		/// </summary>
		public async Task LoadComplete(bool includeWalletItems, bool includeKeys, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				foreach(IAccountFileInfo account in this.accounts.Values) {
					if(includeWalletItems) {
						await account.Load(handle).ConfigureAwait(false);
					}

					if(includeKeys) {
						foreach(WalletKeyFileInfo key in account.WalletKeysFileInfo.Values) {
							await key.Load(handle).ConfigureAwait(false);
						}
					}
				}
			}
		}

		public Task CreateEmptyFileBase(IUserWallet entry, LockContext lockContext) {
			if(entry is ENTRY_TYPE entryType) {
				return this.CreateEmptyFile(entryType, lockContext);
			}

			throw new InvalidCastException();
		}

		protected override Task LoadFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					this.Filebytes.Entry = (await this.serialisationFal.LoadFile(this.Filename, this.EncryptionInfo, true).ConfigureAwait(false)).Entry;
				}
			}, data);
		}

		protected override Task SaveFileBytes(LockContext lockContext, object data = null) {
			return this.RunCryptoOperation(async () => {
				using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					await this.serialisationFal.SaveFile(this.Filename, this.Filebytes, this.EncryptionInfo, true).ConfigureAwait(false);
				}
			}, data);
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
			foreach(IAccountFileInfo account in this.accounts.Values) {
				account?.Dispose();
			}
		}

		protected override async Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				litedbDal.CreateDbFile<ENTRY_TYPE, Guid>(i => i.Id);
			}
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override async Task InsertNewDbData(ENTRY_TYPE wallet, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.wallet = wallet;

				await this.RunDbOperation((litedbDal, lc) => {
					litedbDal.Insert(wallet, c => c.Id);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);
			}
		}

		protected override async Task PrepareEncryptionInfo(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					await this.LoadFileSecurityDetails(handle).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		///     Load the security details from the wallet file
		/// </summary>
		/// <exception cref="ApplicationException"></exception>
		public async Task LoadFileSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.EncryptionInfo = new EncryptionInfo();

				this.WalletSecurityDetails.EncryptWallet = this.serialisationFal.IsFileWalleteEncrypted();

				if(this.WalletSecurityDetails.EncryptWallet) {

					this.EncryptionInfo.Encrypt = true;

					if(!this.serialisationFal.TransactionalFileSystem.FileExists(this.walletCryptoFile)) {
						throw new ApplicationException("The wallet crypto file does not exist. Impossible to load encrypted wallet.");
					}

					this.EncryptionInfo.Secret = () => this.WalletSecurityDetails.WalletPassphraseBytes;

					using SafeArrayHandle cryptoParameterSimpleBytes = SafeArrayHandle.Wrap(await this.serialisationFal.TransactionalFileSystem.ReadAllBytesAsync(this.walletCryptoFile).ConfigureAwait(false));
					this.EncryptionInfo.EncryptionParameters = EncryptorParameters.RehydrateEncryptor(cryptoParameterSimpleBytes);
				}
			}
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.EncryptionInfo = new EncryptionInfo();

				this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

				if(this.EncryptionInfo.Encrypt) {
					if(!this.WalletSecurityDetails.WalletPassphraseValid) {
						throw new ApplicationException("Encrypted wallet does not have a valid passphrase");
					}

					this.EncryptionInfo.Secret = () => this.WalletSecurityDetails.WalletPassphraseBytes;

					this.EncryptionInfo.EncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(this.chainConfiguration);
				}

			}
		}

		protected override Task UpdateDbEntry(LockContext lockContext) {
			return this.RunDbOperation(async (litedbDal, lc) => {
				if(litedbDal.CollectionExists<ENTRY_TYPE>()) {
					litedbDal.Update(this.Wallet(lc).WaitAndUnwrapException());

				}
			}, lockContext);

		}
	}
}