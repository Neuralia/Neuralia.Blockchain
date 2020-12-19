using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyHistoryFileInfo : TypedEntryWalletFileInfo<WalletKeyHistory> {

		private readonly IWalletAccount account;

		public WalletKeyHistoryFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected Task InsertNewDbData(WalletKeyHistory keyHistory, LockContext lockContext) {

			return this.RunDbOperation((dbdal, lc) => {
				dbdal.Insert(keyHistory, c => c.Id);

				return Task.CompletedTask;
			}, lockContext);

		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<IWalletKeyHistory, Guid>(i => i.Id);

			return Task.CompletedTask;
		}

		protected override Task PrepareEncryptionInfo(LockContext lockContext) {
			return this.CreateSecurityDetails(lockContext);
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.account.KeyHistoryFileEncryptionParameters;
						this.EncryptionInfo.SecretHandler = () => this.account.KeyHistoryFileSecret;
					}
				}
			}
		}

		public async Task InsertKeyHistoryEntry(IWalletKey key, WalletKeyHistory walletAccountKeyHistory, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				walletAccountKeyHistory.Copy(key);

				await this.RunDbOperation((dbdal, lc) => {
					// lets check the last one inserted, make sure it was a lower key height than ours now

					dbdal.Insert(walletAccountKeyHistory, k => k.Id);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}
	}
}