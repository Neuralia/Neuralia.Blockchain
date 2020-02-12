using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletTransactionCacheFileInfo : ISingleEntryWalletFileInfo {
		void InsertTransactionCacheEntry(IWalletTransactionCache transactionCacheEntry);
		void RemoveTransaction(TransactionId transactionId);
		void UpdateTransaction(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash);
		IWalletTransactionCache GetTransactionBase(TransactionId transactionId);
		int ClearTimedOutTransactions(DateTime lastBlockTimestamp);
	}

	public class WalletTransactionCacheFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletTransactionCacheFileInfo
		where T : WalletTransactionCache {

		private readonly IWalletAccount account;

		public WalletTransactionCacheFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public void InsertTransactionCacheEntry(IWalletTransactionCache transactionCacheEntry) {
			this.InsertTransactionCacheEntry((T) transactionCacheEntry);
		}

		public void RemoveTransaction(TransactionId transactionId) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {
						litedbDal.Remove<T>(k => k.TransactionId  ==  transactionId.ToString());
					}
				});

				this.Save();
			}
		}

		public void UpdateTransaction(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {

						T entry = litedbDal.GetOne<T>(k => k.TransactionId  == transactionId.ToString());

						if(entry != null) {
							entry.Status = (byte) status;
							entry.GossipMessageHash = gossipMessageHash;
							litedbDal.Update(entry);
						}
					}
				});

				this.Save();
			}
		}

		public IWalletTransactionCache GetTransactionBase(TransactionId transactionId) {
			return this.GetTransaction(transactionId);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(T transactionCache) {

			this.RunDbOperation(litedbDal => {
				litedbDal.Insert(transactionCache, c => c.TransactionId);
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

		public void InsertTransactionCacheEntry(T transactionCacheEntry) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {

					litedbDal.Insert(transactionCacheEntry, k => k.TransactionId);
				});

				this.Save();
			}
		}

		public bool TransactionExists(TransactionId transactionId) {

			return this.RunQueryDbOperation(litedbDal => {

				if(!litedbDal.CollectionExists<T>()) {
					return false;
				}

				return litedbDal.Exists<T>(k => k.TransactionId == transactionId.ToString());
			});
		}

		public T GetTransaction(TransactionId transactionId) {
			lock(this.locker) {
				return this.RunQueryDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {

						return litedbDal.GetOne<T>(k => k.TransactionId == transactionId.ToString());
					}

					return null;
				});
			}
		}

		/// <summary>
		/// remove all timed outs
		/// </summary>
		/// <returns></returns>
		public int ClearTimedOutTransactions(DateTime lastBlockTimestamp) {
			lock(this.locker) {
				return this.RunQueryDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>()) {

						return litedbDal.Remove<T>(k => k.Expiration < lastBlockTimestamp);
					}

					return 0;
				});
			}
		}
	}
}