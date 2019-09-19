using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletElectionCacheFileInfo : SingleEntryWalletFileInfo<WalletElectionCache> {

		private readonly IWalletAccount account;

		public WalletElectionCacheFileInfo(IWalletAccount account, string filename, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(WalletElectionCache transactionCache) {

			this.RunDbOperation(litedbDal => {
				litedbDal.Insert(transactionCache, c => c.TransactionId);
			});

		}

		public override void CreateEmptyFile(object data = null) {
			lock(this.locker) {
				this.DeleteFile();

				base.CreateEmptyFile();
			}
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<WalletElectionCache, TransactionId>(i => i.TransactionId);
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

		public void InsertElectionCacheEntry(WalletElectionCache transactionCacheEntry) {

			lock(this.locker) {
				this.RunDbOperation(litedbDal => {

					litedbDal.Insert(transactionCacheEntry, k => k.TransactionId);
				});

				this.Save();
			}
		}

		public void InsertElectionCacheEntries(List<WalletElectionCache> transactions) {
			lock(this.locker) {
				if(transactions.Any()) {
					this.RunDbOperation(litedbDal => {

						var ids = transactions.Select(t => t.TransactionId).ToList();

						if(litedbDal.CollectionExists<WalletElectionCache>() && litedbDal.Exists<WalletElectionCache>(k => ids.Contains(k.TransactionId))) {
							throw new ApplicationException("A transaction already exists in the election cache.");
						}

						foreach(WalletElectionCache transaction in transactions) {

							litedbDal.Insert(transaction, k => k.TransactionId);
						}
					});

					this.Save();
				}
			}
		}

		public bool ElectionExists(TransactionId transactionId) {

			return this.RunQueryDbOperation(litedbDal => {
				if(!litedbDal.CollectionExists<WalletElectionCache>()) {
					return false;
				}

				return litedbDal.Exists<WalletElectionCache>(k => k.TransactionId.Equals(transactionId));
			});
		}

		public bool ElectionAnyExists(List<TransactionId> transactions) {

			lock(this.locker) {
				if(transactions.Any()) {
					return this.RunQueryDbOperation(litedbDal => {
						if(!litedbDal.CollectionExists<WalletElectionCache>()) {
							return false;
						}

						return litedbDal.Exists<WalletElectionCache>(k => transactions.Contains(k.TransactionId));
					});
				}

				return false;
			}
		}

		public void RemoveElection(TransactionId transactionId) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => k.TransactionId.Equals(transactionId));
					}
				});

				this.Save();
			}
		}

		public void RemoveElections(List<TransactionId> transactions) {

			lock(this.locker) {
				if(transactions.Any()) {
					this.RunDbOperation(litedbDal => {
						if(litedbDal.CollectionExists<WalletElectionCache>()) {
							foreach(TransactionId transaction in transactions) {

								litedbDal.Remove<WalletElectionCache>(k => k.TransactionId.Equals(transaction));
							}
						}
					});

					this.Save();
				}
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block ID
		/// </summary>
		/// <param name="blockId"></param>
		public void RemoveBlockElection(long blockId) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => k.BlockId == blockId);
					}

				});

				this.Save();
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block Scope, and remove any other transactions in the list
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="transactions"></param>
		public void RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactions) {
			lock(this.locker) {
				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => (k.BlockId == blockId) || transactions.Contains(k.TransactionId));
					}

				});

				this.Save();
			}
		}

		public List<TransactionId> GetAllTransactions() {
			return this.RunQueryDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<WalletElectionCache>()) {
					return litedbDal.All<WalletElectionCache>().Select(t => t.TransactionId).ToList();
				}

				return new List<TransactionId>();
			});
		}
	}
}