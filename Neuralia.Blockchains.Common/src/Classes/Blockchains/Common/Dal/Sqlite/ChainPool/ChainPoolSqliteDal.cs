using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainPool {

	public interface IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS> : ISqliteDal<IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>>, IChainPoolDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}

	public abstract class ChainPoolSqliteDal<CHAIN_POOL_CONTEXT, CHAIN_POOL_PUBLIC_TRANSACTIONS> : SqliteDal<CHAIN_POOL_CONTEXT>, IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_CONTEXT : DbContext, IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS>, new() {

		private readonly IBlockchainTimeService timeService;
		public ChainPoolSqliteDal(string folderPath, BlockchainServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateChainPoolContext<CHAIN_POOL_CONTEXT>, serializationType) {

			this.timeService = serviceSet.BlockchainTimeService;
		}

		public async Task<bool> InsertTransactionEntry(ITransactionEnvelope signedTransactionEnvelope, DateTime chainInception) {
			CHAIN_POOL_PUBLIC_TRANSACTIONS entry = new CHAIN_POOL_PUBLIC_TRANSACTIONS();

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			this.PrepareTransactionEntry(entry, signedTransactionEnvelope, chainInception);

			LockContext lockContext = null;
			return await this.PerformOperationAsync(async (db, lc) => {

				if(!await db.PublicTransactions.AnyAsync(t => t.TransactionId == entry.TransactionId).ConfigureAwait(false)) {
					db.PublicTransactions.Add(entry);
					
					await db.SaveChangesAsync().ConfigureAwait(false);

					return true;
				}

				return false;

			}, lockContext).ConfigureAwait(false);
		}

		public async Task RemoveTransactionEntry(TransactionId transactionId) {

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			await this.PerformOperationAsync(async (db, lc) => {
				string transactionString = transactionId.ToCompactString();
				CHAIN_POOL_PUBLIC_TRANSACTIONS transactionEntry = await db.PublicTransactions.SingleOrDefaultAsync(t => t.TransactionId == transactionString).ConfigureAwait(false);

				if(transactionEntry != null) {
					db.PublicTransactions.Remove(transactionEntry);

					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			}).ConfigureAwait(false);
		}

		public async Task<List<TransactionId>> GetTransactions() {
			await this.ClearExpiredTransactions().ConfigureAwait(false);

			return await this.PerformOperationAsync(async (db, lc) => {

				return (await db.PublicTransactions.Select(t => t.TransactionId).ToListAsync().ConfigureAwait(false)).Select(TransactionId.FromCompactString).ToList();
			}).ConfigureAwait(false);
		}

		public async Task ClearExpiredTransactions() {
			try {
				await this.PerformOperationAsync((db, lc) => {
					DateTime time = DateTimeEx.CurrentTime;
					db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => t.Expiration < time));

					return db.SaveChangesAsync();
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				//TODO: what to do?
				NLog.Default.Error("Failed to clear expired transactions", ex);
			}
		}

		public Task ClearTransactions() {
			return this.PerformOperationAsync((db, lc) => {

				db.PublicTransactions.RemoveRange(db.PublicTransactions);

				return db.SaveChangesAsync();
			});
		}

		public async Task ClearTransactions(List<TransactionId> transactionIds) {
			List<string> stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

			await this.PerformOperationAsync((db, lc) => {
				db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId)));

				return db.SaveChangesAsync();
			}).ConfigureAwait(false);

			await this.ClearExpiredTransactions().ConfigureAwait(false);
		}

		public async Task RemoveTransactionEntries(List<TransactionId> transactionIds) {

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			if(transactionIds.Any()) {
				List<string> stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

				await this.PerformOperationAsync((db, lc) => {

					foreach(CHAIN_POOL_PUBLIC_TRANSACTIONS transaction in db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId))) {
						db.PublicTransactions.Remove(transaction);
					}

					return db.SaveChangesAsync();
				}).ConfigureAwait(false);
			}
		}

		protected virtual void PrepareTransactionEntry(CHAIN_POOL_PUBLIC_TRANSACTIONS entry, ITransactionEnvelope signedTransactionEnvelope, DateTime chainInception) {
			entry.TransactionId = signedTransactionEnvelope.Contents.Uuid.ToCompactString();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.Expiration = this.timeService.GetTransactionExpiration(signedTransactionEnvelope, chainInception);

		}
	}
}