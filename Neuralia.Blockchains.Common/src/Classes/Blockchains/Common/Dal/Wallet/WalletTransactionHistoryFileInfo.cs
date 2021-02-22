using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public interface IWalletTransactionHistoryFileInfo : ISingleEntryWalletFileInfo {
		Task InsertTransactionHistoryEntry(IWalletTransactionHistory transactionHistoryEntry, LockContext lockContext);
		Task RemoveTransaction(TransactionId transactionId, LockContext lockContext);
		Task UpdateTransactionStatus(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext);
		Task<IWalletTransactionHistory> GetTransactionBase(TransactionId transactionId, LockContext lockContext);
		Task<bool> GetTransactionExists(TransactionId transactionId, LockContext lockContext);
	}

	public abstract class WalletTransactionHistoryFileInfo<T> : TypedEntryWalletFileInfo<T>, IWalletTransactionHistoryFileInfo
		where T : WalletTransactionHistory {

		private readonly IWalletAccount account;

		public WalletTransactionHistoryFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public Task InsertTransactionHistoryEntry(IWalletTransactionHistory transactionHistoryEntry, LockContext lockContext) {
			return this.InsertTransactionHistoryEntry((T) transactionHistoryEntry, lockContext);
		}

		public async Task RemoveTransaction(TransactionId transactionId, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<T>()) {
						dbdal.Remove<T>(k => k.TransactionId == transactionId.ToString());
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public async Task UpdateTransactionStatus(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<T>()) {

						T entry = dbdal.GetOne<T>(k => k.TransactionId == transactionId.ToString());

						if((entry != null) && entry.Local) {
							entry.Status = status;
							dbdal.Update(entry);
						}
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public async Task<IWalletTransactionHistory> GetTransactionBase(TransactionId transactionId, LockContext lockContext) {
			return await this.GetTransaction(transactionId, lockContext).ConfigureAwait(false);
		}

		public async Task<bool> GetTransactionExists(TransactionId transactionId, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<T>()) {
						return Task.FromResult(dbdal.Any<T>(k => k.TransactionId == transactionId.ToString()));
					}

					return Task.FromResult(false);
				}, handle).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected Task InsertNewDbData(T transactionHistory, LockContext lockContext) {

			return this.RunDbOperation((dbdal, lc) => {
				dbdal.Insert(transactionHistory, c => c.TransactionId);

				return Task.CompletedTask;
			}, lockContext);
		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<T, string>(i => i.TransactionId);

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

						this.EncryptionInfo.EncryptionParameters = this.account.TransactionHistoryFileEncryptionParameters;
						this.EncryptionInfo.SecretHandler = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}
		public async Task InsertTransactionHistoryEntry(T transactionHistoryEntry, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {

					if(dbdal.CollectionExists<T>() && dbdal.Exists<T>(k => k.TransactionId == transactionHistoryEntry.TransactionId)) {
						return Task.CompletedTask;
					}

					dbdal.Insert(transactionHistoryEntry, k => k.TransactionId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public Task<bool> TransactionExists(TransactionId transactionId, LockContext lockContext) {

			return this.RunQueryDbOperation((dbdal, lc) => {

				if(!dbdal.CollectionExists<T>()) {
					return Task.FromResult(false);
				}

				return Task.FromResult(dbdal.Exists<T>(k => k.TransactionId == transactionId.ToString()));
			}, lockContext);
		}

		public async Task<T> GetTransaction(TransactionId transactionId, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<T>()) {

						return Task.FromResult(dbdal.GetOne<T>(k => k.TransactionId == transactionId.ToString()));
					}

					return Task.FromResult((T) default);
				}, handle).ConfigureAwait(false);
			}
		}
	}
}