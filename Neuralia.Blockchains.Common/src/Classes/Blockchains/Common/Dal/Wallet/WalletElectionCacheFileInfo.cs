using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletElectionCacheFileInfo : SingleEntryWalletFileInfo<WalletElectionCache> {

		private readonly IWalletAccount account;

		public WalletElectionCacheFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override Task InsertNewDbData(WalletElectionCache transactionCache, LockContext lockContext) {

			return this.RunDbOperation((litedbDal, lc) => {
				litedbDal.Insert(transactionCache, c => c.TransactionId);

				return Task.CompletedTask;
			}, lockContext);

		}

		public override async Task CreateEmptyFile(LockContext lockContext, object data = null) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.DeleteFile();

				await base.CreateEmptyFile(handle).ConfigureAwait(false);
			}
		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<WalletElectionCache, TransactionId>(i => i.TransactionId);

			return Task.CompletedTask;
		}

		protected override Task PrepareEncryptionInfo(LockContext lockContext) {
			return this.CreateSecurityDetails(lockContext);
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret               = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override Task UpdateDbEntry(LockContext lockContext) {
			// do nothing, we dont udpate

			return Task.CompletedTask;
		}

		public async Task InsertElectionCacheEntry(WalletElectionCache transactionCacheEntry, LockContext lockContext) {

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {

					litedbDal.Insert(transactionCacheEntry, k => k.TransactionId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public async Task InsertElectionCacheEntries(List<WalletElectionCache> transactions, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					await RunDbOperation((litedbDal, lc) => {

						var ids = transactions.Select(t => t.TransactionId).ToList();

						if(litedbDal.CollectionExists<WalletElectionCache>() && litedbDal.Exists<WalletElectionCache>(k => ids.Contains(k.TransactionId))) {
							throw new ApplicationException("A transaction already exists in the election cache.");
						}

						foreach(WalletElectionCache transaction in transactions) {

							litedbDal.Insert(transaction, k => k.TransactionId);
						}

						return Task.CompletedTask;
					}, handle).ConfigureAwait(false);

					await Save(handle).ConfigureAwait(false);
				}
			}
		}

		public Task<bool> ElectionExists(TransactionId transactionId, LockContext lockContext) {

			return this.RunQueryDbOperation((litedbDal, lc) => {
				if(!litedbDal.CollectionExists<WalletElectionCache>()) {
					return Task.FromResult(false);
				}

				return Task.FromResult(litedbDal.Exists<WalletElectionCache>(k => k.TransactionId == transactionId));
			}, lockContext);
		}

		public async Task<bool> ElectionAnyExists(List<TransactionId> transactions, LockContext lockContext) {

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					return await RunQueryDbOperation((litedbDal, lc) => {
						if(!litedbDal.CollectionExists<WalletElectionCache>()) {
							return Task.FromResult(false);
						}

						return Task.FromResult(litedbDal.Exists<WalletElectionCache>(k => transactions.Contains(k.TransactionId)));
					}, handle).ConfigureAwait(false);
				}

				return false;
			}
		}

		public async Task RemoveElection(TransactionId transactionId, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => k.TransactionId == transactionId);
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(true);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public async Task RemoveElections(List<TransactionId> transactions, LockContext lockContext) {

			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(transactions.Any()) {
					await RunDbOperation((litedbDal, lc) => {
						if(litedbDal.CollectionExists<WalletElectionCache>()) {
							foreach(TransactionId transaction in transactions) {

								litedbDal.Remove<WalletElectionCache>(k => k.TransactionId == transaction);
							}
						}

						return Task.CompletedTask;
					}, handle).ConfigureAwait(false);

					await Save(handle).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block ID
		/// </summary>
		/// <param name="blockId"></param>
		public async Task RemoveBlockElection(long blockId, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => k.BlockId == blockId);
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Remove all transactions associated with the block Scope, and remove any other transactions in the list
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="transactions"></param>
		public async Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactions, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<WalletElectionCache>()) {
						litedbDal.Remove<WalletElectionCache>(k => k.BlockId == blockId || transactions.Contains(k.TransactionId));
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public Task<List<TransactionId>> GetAllTransactions(LockContext lockContext) {
			return this.RunQueryDbOperation((litedbDal, lc) => {

				return Task.FromResult(litedbDal.Open(db => {
					return litedbDal.CollectionExists<WalletElectionCache>(db) ? litedbDal.All<WalletElectionCache>(db).Select(t => t.TransactionId).ToList() : new List<TransactionId>();
				}));

			}, lockContext);
		}

	}
}