using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Locking;

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

		protected ChainOptionsSnapshotSqliteDal(string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateChainOptionsSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public void EnsureEntryCreated(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation) {
			this.PerformOperation(operation);
		}

		public Task<CHAIN_OPTIONS_SNAPSHOT> LoadChainOptionsSnapshot(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task<CHAIN_OPTIONS_SNAPSHOT>> operation) {
			return this.PerformOperationAsync(operation);
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation) {

			return this.PerformOperationAsync(operation);
		}

		public async Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> result = new List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>();

			(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction) trx = await this.BeginHoldingTransaction().ConfigureAwait(false);
			result.Add(trx);

			LockContext lockContext = null;

			List<Func<ACCOUNT_SNAPSHOT_CONTEXT, Task>> wrappedOperations = actions.SelectMany(e => e.Value).Select(o => {

				Task Func(ACCOUNT_SNAPSHOT_CONTEXT db) {
					return o(db, lockContext);
				}

				return (Func<ACCOUNT_SNAPSHOT_CONTEXT, Task>) Func;
			}).ToList();

			await this.PerformContextOperationsAsync(trx.db, wrappedOperations).ConfigureAwait(false);

			return result;
		}
	}
}