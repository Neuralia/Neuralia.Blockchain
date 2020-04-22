using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletTransactionCacheFileInfo : ISingleEntryWalletFileInfo {
		Task                          InsertTransactionCacheEntry(IWalletTransactionCache transactionCacheEntry, LockContext lockContext);
		Task                          RemoveTransaction(TransactionId transactionId, LockContext lockContext);
		Task                          UpdateTransaction(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash, LockContext lockContext);
		Task<IWalletTransactionCache> GetTransactionBase(TransactionId transactionId, LockContext lockContext);
		Task<int>                     ClearTimedOutTransactions(DateTime lastBlockTimestamp, LockContext lockContext);
	}

	public class WalletTransactionCacheFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletTransactionCacheFileInfo
		where T : WalletTransactionCache {

		private readonly IWalletAccount account;

		public WalletTransactionCacheFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public Task InsertTransactionCacheEntry(IWalletTransactionCache transactionCacheEntry, LockContext lockContext) {
			return this.InsertTransactionCacheEntry((T) transactionCacheEntry, lockContext);
		}

		public async Task RemoveTransaction(TransactionId transactionId, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {
						litedbDal.Remove<T>(k => k.TransactionId == transactionId.ToString());
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public async Task UpdateTransaction(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						T entry = litedbDal.GetOne<T>(k => k.TransactionId == transactionId.ToString());

						if(entry != null) {
							entry.Status            = (byte) status;
							entry.GossipMessageHash = gossipMessageHash;
							litedbDal.Update(entry);
						}
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public async Task<IWalletTransactionCache> GetTransactionBase(TransactionId transactionId, LockContext lockContext) {
			return await GetTransaction(transactionId, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override Task InsertNewDbData(T transactionCache, LockContext lockContext) {

			return this.RunDbOperation((litedbDal, lc) => {
				litedbDal.Insert(transactionCache, c => c.TransactionId);

				return Task.CompletedTask;
			}, lockContext);

		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<T, string>(i => i.TransactionId);

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

		public async Task InsertTransactionCacheEntry(T transactionCacheEntry, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {

					litedbDal.Insert(transactionCacheEntry, k => k.TransactionId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(true);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public Task<bool> TransactionExists(TransactionId transactionId, LockContext lockContext) {

			return this.RunQueryDbOperation((litedbDal, lc) => {

				if(!litedbDal.CollectionExists<T>()) {
					return Task.FromResult(false);
				}

				return Task.FromResult(litedbDal.Exists<T>(k => k.TransactionId == transactionId.ToString()));
			}, lockContext);
		}

		public async Task<T> GetTransaction(TransactionId transactionId, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						return Task.FromResult(litedbDal.GetOne<T>(k => k.TransactionId == transactionId.ToString()));
					}

					return Task.FromResult((T) default);
				}, handle).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// remove all timed outs
		/// </summary>
		/// <returns></returns>
		public async Task<int> ClearTimedOutTransactions(DateTime lastBlockTimestamp, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						return Task.FromResult(litedbDal.Remove<T>(k => k.Expiration < lastBlockTimestamp));
					}

					return Task.FromResult(0);
				}, handle).ConfigureAwait(false);
			}
		}
	}
}