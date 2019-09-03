using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IChainOptionsSnapshotSqliteDal {
	}

	public interface IChainOptionsSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT> : ISqliteDal<IChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT>>, IChainOptionsSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>, IChainOptionsSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotSqliteEntry, new() {
	}

	public abstract class ChainOptionsSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT> : SqliteDal<ACCOUNT_SNAPSHOT_CONTEXT>, IChainOptionsSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotSqliteEntry, new() {

		protected ChainOptionsSnapshotSqliteDal(string folderPath, ServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, chainDalCreationFactory.CreateChainOptionsSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public void EnsureEntryCreated(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation) {
			this.PerformOperation(operation);
		}

		public List<CHAIN_OPTIONS_SNAPSHOT> LoadChainOptionsSnapshots(Func<ACCOUNT_SNAPSHOT_CONTEXT, List<CHAIN_OPTIONS_SNAPSHOT>> operation) {
			return this.PerformOperation(operation);
		}

		public void Clear() {

			this.PerformOperation(db => {
				db.ChainOptionsSnapshots.RemoveRange(db.ChainOptionsSnapshots);

				db.SaveChanges();
			});
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation) {

			this.PerformOperation(operation);
		}

		public List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions) {
			var result = new List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>();

			(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction) trx = this.BeginHoldingTransaction();
			result.Add(trx);

			this.PerformContextOperations(trx.db, actions.SelectMany(e => e.Value).ToList());

			return result;
		}
	}
}