using System;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletKeyLogFileInfo : TypedEntryWalletFileInfo<WalletAccountKeyLog> {

		private readonly IWalletAccount account;

		public WalletKeyLogFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<WalletAccountKeyLog, ObjectId>(i => i.Id);
			
			dbdal.CreateDbFile<WalletAccountKeyLogMetadata, int>(i => i.Id);

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

						this.EncryptionInfo.EncryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.SecretHandler = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		public async Task<WalletAccountKeyLogMetadata> GetKeyLogMetadata(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await RunQueryDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<WalletAccountKeyLogMetadata>()) {
						return Task.FromResult(dbdal.GetSingle<WalletAccountKeyLogMetadata>());
					}

					return Task.FromResult((WalletAccountKeyLogMetadata) default);
				}, lockContext).ConfigureAwait(false);
			}
		}

		public async Task<bool> UpdateKeyLogMetadata(WalletAccountKeyLogMetadata keyLogMetadata, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await RunDbOperation((dbdal, lc) => {

					if(!dbdal.CollectionExists<WalletAccountKeyLogMetadata>()) {
						dbdal.CreateDbFile<WalletAccountKeyLogMetadata, int>(i => i.Id);
					}

					if(!dbdal.Any<WalletAccountKeyLogMetadata>()) {

						dbdal.Insert(keyLogMetadata, k => k.Id);
					} else {
						dbdal.Update(keyLogMetadata);
					}

					return Task.FromResult(true);
				}, lockContext).ConfigureAwait(false);
			}
		}
		
		public async Task InsertKeyLogEntry(WalletAccountKeyLog walletAccountKeyLog, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((dbdal, lc) => {
					// lets check the last one inserted, make sure it was a lower key height than ours now

					// -1 is for keys without an index. otherwise we check it
					if(walletAccountKeyLog.KeyUseIndex?.KeyUseIndex != -1) {
						WalletAccountKeyLog last = null;

						if(dbdal.CollectionExists<WalletAccountKeyLog>()) {
							last = dbdal.Get<WalletAccountKeyLog>(k => k.KeyOrdinalId == walletAccountKeyLog.KeyOrdinalId).OrderByDescending(k => k.Timestamp).FirstOrDefault();
						}

						if(last?.KeyUseIndex != null) {

							if(last.KeyUseIndex > walletAccountKeyLog.KeyUseIndex) {
								throw new ApplicationException("You are using a key height that is inferior to one previously used. Your wallet could be an older version.");
							}
						}
					}

					dbdal.Insert(walletAccountKeyLog, k => k.Id);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public Task<bool> ConfirmKeyLogBlockEntry(long confirmationBlockId, LockContext lockContext) {
			return this.ConfirmKeyLogEntry(confirmationBlockId.ToString(), Enums.BlockchainEventTypes.Block, confirmationBlockId, null, lockContext);
		}

		public Task<bool> ConfirmKeyLogTransactionEntry(TransactionId transactionId, IdKeyUseIndexSet idKeyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			return this.ConfirmKeyLogEntry(transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, confirmationBlockId, idKeyUseIndexSet, lockContext);
		}

		public async Task<bool> ConfirmKeyLogEntry(string eventId, Enums.BlockchainEventTypes eventType, long confirmationBlockId, IdKeyUseIndexSet idKeyUseIndexSet, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				bool result = await this.RunDbOperation((dbdal, lc) => {
					// lets check the last one inserted, make sure it was a lower key height than ours now
					WalletAccountKeyLog entry = null;

					if(dbdal.CollectionExists<WalletAccountKeyLog>()) {
						byte eventTypeCasted = (byte) eventType;
						entry = dbdal.Get<WalletAccountKeyLog>(k => (k.EventId == eventId) && (k.EventType == eventTypeCasted)).FirstOrDefault();
					}

					if(entry == null) {
						return Task.FromResult(false);
					}

					if((idKeyUseIndexSet != null) && (entry.KeyUseIndex != idKeyUseIndexSet)) {
						throw new ApplicationException($"Failed to confirm keylog entry for event '{eventId}'. Expected key use index to be '{idKeyUseIndexSet}' but found '{entry.KeyUseIndex}' instead.");
					}

					entry.ConfirmationBlockId = confirmationBlockId;

					return Task.FromResult(dbdal.Update(entry));
				}, handle).ConfigureAwait(false);

				if(result) {
					await this.Save(handle).ConfigureAwait(false);
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

			return this.RunQueryDbOperation((dbdal, lc) => {
				if(!dbdal.CollectionExists<WalletAccountKeyLog>()) {
					return Task.FromResult(false);
				}

				byte eventTypeCasted = (byte) eventType;

				return Task.FromResult(dbdal.Exists<WalletAccountKeyLog>(k => (k.EventId == eventId) && (k.EventType == eventTypeCasted)));
			}, lockContext);
		}

		public Task<bool> KeyLogBlockIsConfirmed(BlockId blockId, LockContext lockContext) {

			return this.KeyLogEntryIsConfirmed(blockId.ToString(), Enums.BlockchainEventTypes.Block, lockContext);
		}

		public Task<bool> KeyLogTransactionIsConfirmed(TransactionId transactionId, LockContext lockContext) {

			return this.KeyLogEntryIsConfirmed(transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, lockContext);
		}

		public Task<bool> KeyLogEntryIsConfirmed(string eventId, Enums.BlockchainEventTypes eventType, LockContext lockContext) {

			return this.RunQueryDbOperation((dbdal, lc) => {
				WalletAccountKeyLog entry = null;

				if(dbdal.CollectionExists<WalletAccountKeyLog>()) {

					byte eventTypeCasted = (byte) eventType;
					entry = dbdal.Get<WalletAccountKeyLog>(k => (k.EventId == eventId) && (k.EventType == eventTypeCasted)).FirstOrDefault();
				}

				return Task.FromResult(entry?.ConfirmationBlockId != null);
			}, lockContext);
		}
	}
}