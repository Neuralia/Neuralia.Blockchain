using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletElectionCacheFileInfo : TypedEntryWalletFileInfo<WalletElectionCache> {

		private readonly IWalletAccount account;

		public WalletElectionCacheFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected Task InsertNewDbData(WalletElectionCache electionCache, LockContext lockContext) {

			return this.RunDbOperation((dbdal, lc) => {
				dbdal.Insert(electionCache, c => c.TransactionId);

				return Task.CompletedTask;
			}, lockContext);

		}

		public override async Task CreateEmptyFile(LockContext lockContext, object data = null) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.DeleteFile();

				await base.CreateEmptyFile(handle).ConfigureAwait(false);
			}
		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<WalletElectionCache, string>(i => i.TransactionId);

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

						this.EncryptionInfo.EncryptionParameters = this.account.ElectionCacheFileEncryptionParameters;
						this.EncryptionInfo.SecretHandler = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		public async Task InsertElectionCacheEntry(WalletElectionCache electionCacheEntry, LockContext lockContext) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {

					dbdal.Insert(electionCacheEntry, k => k.TransactionId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public async Task InsertElectionCacheEntries(List<WalletElectionCache> transactions, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					await this.RunDbOperation( async (dbdal, lc) => {

						return dbdal.OpenAsync(async db => {
							foreach(WalletElectionCache transaction in transactions) {

								bool collectionExists = dbdal.CollectionExists<WalletElectionCache>(db);
								
								if(!collectionExists || !dbdal.Exists<WalletElectionCache>(k => k.TransactionId == transaction.TransactionId, db)) {
									dbdal.Insert(transaction, k => k.TransactionId, db);
								}
							}
						});
						
						
					}, handle).ConfigureAwait(false);

					await this.Save(handle).ConfigureAwait(false);
				}
			}
		}

		public Task<bool> ElectionExists(TransactionId transactionId, LockContext lockContext) {

			return this.RunQueryDbOperation((dbdal, lc) => {
				if(!dbdal.CollectionExists<WalletElectionCache>()) {
					return Task.FromResult(false);
				}

				return Task.FromResult(dbdal.Exists<WalletElectionCache>(k => k.TransactionId == transactionId.ToString()));
			}, lockContext);
		}

		public async Task<bool> ElectionAnyExists(List<TransactionId> transactions, LockContext lockContext) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					return await this.RunQueryDbOperation((dbdal, lc) => {
						if(!dbdal.CollectionExists<WalletElectionCache>()) {
							return Task.FromResult(false);
						}

						List<string> transactionIds = transactions.Select(t => t.ToString()).ToList();
						return Task.FromResult(dbdal.Exists<WalletElectionCache>(k => transactionIds.Contains(k.TransactionId)));
					}, handle).ConfigureAwait(false);
				}

				return false;
			}
		}

		public async Task RemoveElection(TransactionId transactionId, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<WalletElectionCache>()) {
						dbdal.Remove<WalletElectionCache>(k => k.TransactionId == transactionId.ToString());
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(true);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public async Task RemoveElections(List<TransactionId> transactions, LockContext lockContext) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					await this.RunDbOperation((dbdal, lc) => {
						if(dbdal.CollectionExists<WalletElectionCache>()) {
							foreach(TransactionId transaction in transactions) {

								dbdal.Remove<WalletElectionCache>(k => k.TransactionId == transaction.ToString());
							}
						}

						return Task.CompletedTask;
					}, handle).ConfigureAwait(false);

					await this.Save(handle).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block ID
		/// </summary>
		/// <param name="blockId"></param>
		public async Task RemoveBlockElection(long blockId, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<WalletElectionCache>()) {
						dbdal.Remove<WalletElectionCache>(k => k.BlockId == blockId);
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block Scope, and remove any other transactions in the list
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="transactions"></param>
		public async Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactions, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<WalletElectionCache>()) {
						
						List<string> transactionIds = transactions.Select(t => t.ToString()).ToList();

						dbdal.Remove<WalletElectionCache>(k => (k.BlockId == blockId) || transactionIds.Contains(k.TransactionId));
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public Task<List<TransactionId>> GetAllTransactions(LockContext lockContext) {
			return this.RunQueryDbOperation((dbdal, lc) => {

				return Task.FromResult(dbdal.Open(db => {
					return dbdal.CollectionExists<WalletElectionCache>(db) ? dbdal.All<WalletElectionCache>(db).Select(t => TransactionId.FromString(t.TransactionId)).ToList() : new List<TransactionId>();
				}));

			}, lockContext);
		}
	}
}