using System;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyLogFileInfo : SingleEntryWalletFileInfo<WalletAccountKeyLog> {

		private readonly IWalletAccount account;

		public WalletKeyLogFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override Task InsertNewDbData(WalletAccountKeyLog keyLog, LockContext lockContext) {

			return this.RunDbOperation((litedbDal, lc) => {
				litedbDal.Insert(keyLog, c => c.Id);

				return Task.CompletedTask;
			}, lockContext);

		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<WalletAccountKeyLog, ObjectId>(i => i.Id);

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

		public async Task InsertKeyLogEntry(WalletAccountKeyLog walletAccountKeyLog, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await RunDbOperation((litedbDal, lc) => {
					// lets check the last one inserted, make sure it was a lower key height than ours now

					// -1 is for keys without an index. otherwise we check it
					if(walletAccountKeyLog.KeyUseIndex?.KeyUseIndex.Value != -1) {
						WalletAccountKeyLog last = null;

						if(litedbDal.CollectionExists<WalletAccountKeyLog>()) {
							last = litedbDal.Get<WalletAccountKeyLog>(k => k.KeyOrdinalId == walletAccountKeyLog.KeyOrdinalId).OrderByDescending(k => k.Timestamp).FirstOrDefault();
						}

						if(last?.KeyUseIndex != null) {

							if(last.KeyUseIndex > walletAccountKeyLog.KeyUseIndex) {
								throw new ApplicationException("You are using a key height that is inferior to one previously used. Your wallet could be an older version.");
							}
						}
					}

					litedbDal.Insert(walletAccountKeyLog, k => k.Id);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await Save(handle).ConfigureAwait(false);
			}
		}

		public Task<bool> ConfirmKeyLogBlockEntry(long confirmationBlockId, LockContext lockContext) {
			return this.ConfirmKeyLogEntry(confirmationBlockId.ToString(), Enums.BlockchainEventTypes.Block, confirmationBlockId, null, lockContext);
		}

		public Task<bool> ConfirmKeyLogTransactionEntry(TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			return this.ConfirmKeyLogEntry(transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, confirmationBlockId, keyUseIndexSet, lockContext);
		}

		public async Task<bool> ConfirmKeyLogEntry(string eventId, Enums.BlockchainEventTypes eventType, long confirmationBlockId, KeyUseIndexSet keyUseIndexSet, LockContext lockContext) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				bool result = await RunDbOperation((litedbDal, lc) => {
					// lets check the last one inserted, make sure it was a lower key height than ours now
					WalletAccountKeyLog entry = null;

					if(litedbDal.CollectionExists<WalletAccountKeyLog>()) {
						byte eventTypeCasted = (byte) eventType;
						entry = litedbDal.Get<WalletAccountKeyLog>(k => k.EventId == eventId && k.EventType == eventTypeCasted).FirstOrDefault();
					}

					if(entry == null) {
						return Task.FromResult(false);
					}

					if(keyUseIndexSet != null && entry.KeyUseIndex != keyUseIndexSet) {
						throw new ApplicationException($"Failed to confirm keylog entry for event '{eventId}'. Expected key use index to be '{keyUseIndexSet}' but found '{entry.KeyUseIndex}' instead.");
					}

					entry.ConfirmationBlockId = confirmationBlockId;

					return Task.FromResult(litedbDal.Update(entry));
				}, handle).ConfigureAwait(false);

				if(result) {
					await Save(handle).ConfigureAwait(false);
				}

				return result;
			}
		}

		public Task<bool> KeyLogBlockExists(BlockId blockId, LockContext lockContext) {

			return this.KeyLogEntryExists(blockId.ToString(), Enums.BlockchainEventTypes.Block, lockContext);
		}

		public Task<bool> KeyLogTransactionExists(TransactionId transactionId, LockContext lockContext) {

			return this.KeyLogEntryExists(transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, lockContext);
		}

		public Task<bool> KeyLogEntryExists(string eventId, Enums.BlockchainEventTypes eventType, LockContext lockContext) {

			return this.RunQueryDbOperation((litedbDal, lc) => {
				if(!litedbDal.CollectionExists<WalletAccountKeyLog>()) {
					return Task.FromResult(false);
				}

				byte eventTypeCasted = (byte) eventType;

				return Task.FromResult(litedbDal.Exists<WalletAccountKeyLog>(k => k.EventId == eventId && k.EventType == eventTypeCasted));
			}, lockContext);
		}

		public Task<bool> KeyLogBlockIsConfirmed(BlockId blockId, LockContext lockContext) {

			return this.KeyLogEntryIsConfirmed(blockId.ToString(), Enums.BlockchainEventTypes.Block, lockContext);
		}

		public Task<bool> KeyLogTransactionIsConfirmed(TransactionId transactionId, LockContext lockContext) {

			return this.KeyLogEntryIsConfirmed(transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, lockContext);
		}

		public Task<bool> KeyLogEntryIsConfirmed(string eventId, Enums.BlockchainEventTypes eventType, LockContext lockContext) {

			return this.RunQueryDbOperation((litedbDal, lc) => {
				WalletAccountKeyLog entry = null;

				if(litedbDal.CollectionExists<WalletAccountKeyLog>()) {

					byte eventTypeCasted = (byte) eventType;
					entry = litedbDal.Get<WalletAccountKeyLog>(k => k.EventId == eventId && k.EventType == eventTypeCasted).FirstOrDefault();
				}

				return Task.FromResult(entry?.ConfirmationBlockId != null);
			}, lockContext);
		}
	}
}