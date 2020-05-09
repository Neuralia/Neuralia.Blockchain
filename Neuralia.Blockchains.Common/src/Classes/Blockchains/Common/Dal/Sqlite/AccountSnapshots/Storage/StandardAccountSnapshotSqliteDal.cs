using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IStandardAccountSnapshotSqliteDal : IAccountSnapshotSqliteDal {
	}

	public interface IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeSqliteEntry, new() {
	}

	public abstract class StandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : StandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new() {

		protected StandardAccountSnapshotSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public Task<List<STANDARD_ACCOUNT_SNAPSHOT>> LoadAccounts(List<AccountId> accountIds) {

			List<long> longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).Select(a => a.ToLongRepresentation()).ToList();
			List<long> sequenceIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).Select(a => a.SequenceId).ToList();

			return this.QueryAllAsync(db => {

				return db.StandardAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToListAsync();
			}, sequenceIds);

		}

		public Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public new Task<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> BeginHoldingTransaction() {
			return base.BeginHoldingTransaction();
		}

		public Task InsertNewStandardAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, bool correlated) {

			return this.PerformOperationAsync(db => {
				STANDARD_ACCOUNT_SNAPSHOT accountEntry = new STANDARD_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;
				accountEntry.Correlated = correlated;

				db.StandardAccountSnapshots.Add(accountEntry);

				return db.SaveChangesAsync();

			}, this.GetKeyGroup(accountId.SequenceId));
		}
	}
}