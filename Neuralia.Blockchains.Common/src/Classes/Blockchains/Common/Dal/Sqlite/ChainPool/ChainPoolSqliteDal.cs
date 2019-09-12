using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainPool {

	public interface IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS> : ISqliteDal<IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>>, IChainPoolDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}

	public abstract class ChainPoolSqliteDal<CHAIN_STATE_CONTEXT, CHAIN_POOL_PUBLIC_TRANSACTIONS> : SqliteDal<CHAIN_STATE_CONTEXT>, IChainPoolSqliteDal<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_STATE_CONTEXT : DbContext, IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS>, new() {

		public ChainPoolSqliteDal(string folderPath, BlockchainServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, chainDalCreationFactory.CreateChainPoolContext<CHAIN_STATE_CONTEXT>, serializationType) {

		}

		public void InsertTransactionEntry(ITransactionEnvelope transactionEnvelope, DateTime chainInception) {
			CHAIN_POOL_PUBLIC_TRANSACTIONS entry = new CHAIN_POOL_PUBLIC_TRANSACTIONS();

			this.ClearExpiredTransactions();

			this.PrepareTransactionEntry(entry, transactionEnvelope, chainInception);

			this.PerformOperation(db => {
				db.PublicTransactions.Add(entry);

				db.SaveChanges();
			});
		}

		public void RemoveTransactionEntry(TransactionId transactionId) {

			this.ClearExpiredTransactions();

			this.PerformOperation(db => {
				CHAIN_POOL_PUBLIC_TRANSACTIONS transactionEntry = db.PublicTransactions.SingleOrDefault(t => t.TransactionId == transactionId.ToCompactString());

				if(transactionEntry != null) {
					db.PublicTransactions.Remove(transactionEntry);

					db.SaveChanges();
				}
			});
		}

		public List<TransactionId> GetTransactions() {
			this.ClearExpiredTransactions();

			return this.PerformOperation(db => {

				return db.PublicTransactions.Select(t => TransactionId.FromCompactString(t.TransactionId)).ToList();
			});
		}

		public void ClearExpiredTransactions() {
			try {
				this.PerformOperation(db => {

					db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => t.Expiration < DateTime.Now));

					db.SaveChanges();
				});
			} catch(Exception ex) {
				//TODO: what to do?
				Log.Error("Failed to clear expired transactions", ex);
			}
		}

		public void ClearTransactions() {
			this.PerformOperation(db => {

				db.PublicTransactions.RemoveRange(db.PublicTransactions);

				db.SaveChanges();
			});
		}

		public void ClearTransactions(List<TransactionId> transactionIds) {
			var stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

			this.PerformOperation(db => {
				db.PublicTransactions.RemoveRange(db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId)));

				db.SaveChanges();
			});

			this.ClearExpiredTransactions();
		}

		public void RemoveTransactionEntries(List<TransactionId> transactionIds) {

			this.ClearExpiredTransactions();

			if(transactionIds.Any()) {
				var stringTransactionIds = transactionIds.Select(t => t.ToCompactString()).ToList();

				this.PerformOperation(db => {

					var transactions = db.PublicTransactions.Where(t => stringTransactionIds.Contains(t.TransactionId));

					foreach(CHAIN_POOL_PUBLIC_TRANSACTIONS transaction in transactions) {
						db.PublicTransactions.Remove(transaction);
					}

					db.SaveChanges();
				});
			}
		}

		protected virtual void PrepareTransactionEntry(CHAIN_POOL_PUBLIC_TRANSACTIONS entry, ITransactionEnvelope transactionEnvelope, DateTime chainInception) {
			entry.TransactionId = transactionEnvelope.Contents.Uuid.SimpleTransactionId.ToCompactString();
			entry.Timestamp = DateTime.Now;
			entry.Expiration = transactionEnvelope.GetExpirationTime(this.timeService, chainInception);
		}
	}
}