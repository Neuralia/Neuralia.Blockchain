using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainPool {

	public interface IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS> : ISqliteDal<IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>>, IChainPoolDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}

	public abstract class ChainPoolSqliteDal<CHAIN_STATE_CONTEXT, CHAIN_POOL_PUBLIC_TRANSACTIONS> : SqliteDal<CHAIN_STATE_CONTEXT>, IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_STATE_CONTEXT : DbContext, IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS>, new() {

		public ChainPoolSqliteDal(string folderPath, BlockchainServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateChainPoolContext<CHAIN_STATE_CONTEXT>, serializationType) {

		}

		public async Task InsertTransactionEntry(ITransactionEnvelope transactionEnvelope, DateTime chainInception) {
			CHAIN_POOL_PUBLIC_TRANSACTIONS entry = new CHAIN_POOL_PUBLIC_TRANSACTIONS();

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			this.PrepareTransactionEntry(entry, transactionEnvelope, chainInception);

			await this.PerformOperationAsync(db => {
				db.PublicTransactions.Add(entry);

				return db.SaveChangesAsync();
			}).ConfigureAwait(false);
		}

		public async Task RemoveTransactionEntry(TransactionId transactionId) {

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			await this.PerformOperationAsync(async db => {
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

			return await this.PerformOperationAsync(async db => {

				return (await db.PublicTransactions.Select(t => t.TransactionId).ToListAsync().ConfigureAwait(false)).Select(TransactionId.FromCompactString).ToList();
			}).ConfigureAwait(false);
		}

		public async Task ClearExpiredTransactions() {
			try {
				await this.PerformOperationAsync(db => {

					db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => t.Expiration < DateTime.UtcNow));

					return db.SaveChangesAsync();
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				//TODO: what to do?
				Log.Error("Failed to clear expired transactions", ex);
			}
		}

		public Task ClearTransactions() {
			return this.PerformOperationAsync(db => {

				db.PublicTransactions.RemoveRange(db.PublicTransactions);

				return db.SaveChangesAsync();
			});
		}

		public async Task ClearTransactions(List<TransactionId> transactionIds) {
			var stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

			await this.PerformOperationAsync(db => {
				db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId)));

				return db.SaveChangesAsync();
			}).ConfigureAwait(false);

			await this.ClearExpiredTransactions().ConfigureAwait(false);
		}

		public async Task RemoveTransactionEntries(List<TransactionId> transactionIds) {

			await this.ClearExpiredTransactions().ConfigureAwait(false);

			if(transactionIds.Any()) {
				var stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

				await this.PerformOperationAsync(db => {

					foreach(CHAIN_POOL_PUBLIC_TRANSACTIONS transaction in db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId))) {
						db.PublicTransactions.Remove(transaction);
					}

					return db.SaveChangesAsync();
				}).ConfigureAwait(false);
			}
		}

		protected virtual void PrepareTransactionEntry(CHAIN_POOL_PUBLIC_TRANSACTIONS entry, ITransactionEnvelope transactionEnvelope, DateTime chainInception) {
			entry.TransactionId = transactionEnvelope.Contents.Uuid.ToCompactString();
			entry.Timestamp = DateTime.UtcNow;
			entry.Expiration = transactionEnvelope.GetExpirationTime(this.timeService, chainInception);
		}
	}
}