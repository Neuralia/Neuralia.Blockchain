using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IStandardAccountSnapshotSqliteDal : IAccountSnapshotSqliteDal {
	}

	public interface IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IStandardAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureSqliteEntry, new() {
	}

	public abstract class StandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : StandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : AccountFeatureSqliteEntry, new() {

		protected StandardAccountSnapshotSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, chainDalCreationFactory.CreateStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public void Clear() {
			foreach(string file in this.GetAllFileGroups()) {
				if(File.Exists(file)) {
					File.Delete(file);
				}
			}
		}

		public List<STANDARD_ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds) {

			var longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).Select(a => a.ToLongRepresentation()).ToList();

			return this.QueryAll(db => {

				return db.StandardAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToList();
			}, longAccountIds.Select(a => a).ToList());

		}

		public void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId));
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId));
		}

		public new List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public new (ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction) BeginHoldingTransaction() {
			return base.BeginHoldingTransaction();
		}

		public void InsertNewStandardAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, long? correlationId) {

			this.PerformOperation(db => {
				STANDARD_ACCOUNT_SNAPSHOT accountEntry = new STANDARD_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;
				accountEntry.CorrelationId = correlationId;

				db.StandardAccountSnapshots.Add(accountEntry);

				db.SaveChanges();

			}, this.GetKeyGroup(accountId.SequenceId));
		}
	}
}