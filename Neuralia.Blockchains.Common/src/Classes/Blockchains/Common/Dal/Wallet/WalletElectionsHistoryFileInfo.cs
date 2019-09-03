using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public interface IWalletElectionsHistoryFileInfo : ISingleEntryWalletFileInfo {
		void InsertElectionsHistoryEntry(IWalletElectionsHistory transactionHistoryEntry);
	}

	public abstract class WalletElectionsHistoryFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletElectionsHistoryFileInfo
		where T : WalletElectionsHistory {

		private readonly IWalletAccount account;

		public WalletElectionsHistoryFileInfo(IWalletAccount account, string filename, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public void InsertElectionsHistoryEntry(IWalletElectionsHistory transactionHistoryEntry) {
			this.InsertElectionsHistoryEntry((T) transactionHistoryEntry);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(T transactionHistory) {

			this.RunDbOperation(litedbDal => {
				litedbDal.Insert(transactionHistory, c => c.BlockId);
			});
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<T, long>(i => i.BlockId);
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

						this.EncryptionInfo.encryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override void UpdateDbEntry() {
			// do nothing, we dont udpate
		}

		public void InsertElectionsHistoryEntry(T transactionHistoryEntry) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {

					if(litedbDal.CollectionExists<T>() && litedbDal.Exists<T>(k => k.BlockId == transactionHistoryEntry.BlockId)) {
						return;
					}

					litedbDal.Insert(transactionHistoryEntry, k => k.BlockId);
				});

				this.Save();
			}
		}
	}
}