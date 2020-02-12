using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public interface IWalletTransactionHistoryFileInfo : ISingleEntryWalletFileInfo {
		void InsertTransactionHistoryEntry(IWalletTransactionHistory transactionHistoryEntry);
		void RemoveTransaction(TransactionId transactionId);
		void UpdateTransactionStatus(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status);
		IWalletTransactionHistory GetTransactionBase(TransactionId transactionId);
	}

	public abstract class WalletTransactionHistoryFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletTransactionHistoryFileInfo
		where T : WalletTransactionHistory {

		private readonly IWalletAccount account;

		public WalletTransactionHistoryFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public void InsertTransactionHistoryEntry(IWalletTransactionHistory transactionHistoryEntry) {
			this.InsertTransactionHistoryEntry((T) transactionHistoryEntry);
		}

		public void RemoveTransaction(TransactionId transactionId) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {
						litedbDal.Remove<T>(k => k.TransactionId == transactionId.ToString());
					}
				});

				this.Save();
			}
		}

		public void UpdateTransactionStatus(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {

						T entry = litedbDal.GetOne<T>(k => k.TransactionId == transactionId.ToString());

						if((entry != null) && entry.Local) {
							entry.Status = (byte) status;
							litedbDal.Update(entry);
						}
					}
				});

				this.Save();
			}
		}

		public IWalletTransactionHistory GetTransactionBase(TransactionId transactionId) {
			return this.GetTransaction(transactionId);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(T transactionHistory) {

			this.RunDbOperation(litedbDal => {
				litedbDal.Insert(transactionHistory, c => c.TransactionId);
			});
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<T, string>(i => i.TransactionId);
		}

		protected override void PrepareEncryptionInfo() {
			this.CreateSecurityDetails();
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override void UpdateDbEntry() {
			// do nothing, we dont udpate

		}

		public void InsertTransactionHistoryEntry(T transactionHistoryEntry) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {

					if(litedbDal.CollectionExists<T>() && litedbDal.Exists<T>(k => k.TransactionId == transactionHistoryEntry.TransactionId)) {
						return;
					}

					litedbDal.Insert(transactionHistoryEntry, k => k.TransactionId);
				});

				this.Save();
			}
		}

		public bool TransactionExists(TransactionId transactionId) {

			return this.RunQueryDbOperation(litedbDal => {

				if(!litedbDal.CollectionExists<T>()) {
					return false;
				}

				return litedbDal.Exists<T>(k => k.TransactionId.Equals(transactionId.ToString()));
			});
		}

		public T GetTransaction(TransactionId transactionId) {
			lock(this.locker) {
				return this.RunQueryDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {

						return litedbDal.GetOne<T>(k => k.TransactionId.Equals(transactionId.ToString()));
					}

					return null;
				});
			}
		}
	}
}