using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyHistoryFileInfo : SingleEntryWalletFileInfo<WalletKeyHistory> {

		private readonly IWalletAccount account;

		public WalletKeyHistoryFileInfo(IWalletAccount account, string filename, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(WalletKeyHistory keyHistory) {

			this.RunDbOperation(litedbDal => {
				litedbDal.Insert(keyHistory, c => c.Id);
			});

		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<IWalletKeyHistory, Guid>(i => i.Id);
		}

		protected override void PrepareEncryptionInfo() {
			this.CreateSecurityDetails();
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.encrypt) {

						this.EncryptionInfo.encryptionParameters = this.account.KeyHistoryFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.account.KeyHistoryFileSecret;
					}
				}
			}
		}

		protected override void UpdateDbEntry() {
			// do nothing, we dont udpate

		}

		public void InsertKeyHistoryEntry(IWalletKey key, WalletKeyHistory walletAccountKeyHistory) {
			lock(this.locker) {

				walletAccountKeyHistory.Copy(key);

				this.RunDbOperation(litedbDal => {
					// lets check the last one inserted, make sure it was a lower key height than ours now

					litedbDal.Insert(walletAccountKeyHistory, k => k.Id);
				});

				this.Save();
			}
		}
	}
}