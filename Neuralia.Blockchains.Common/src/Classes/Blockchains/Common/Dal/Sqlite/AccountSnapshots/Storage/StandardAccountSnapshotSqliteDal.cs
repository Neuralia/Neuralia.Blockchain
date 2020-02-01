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
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

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
		
		public List<STANDARD_ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds) {

			var longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).Select(a => a.ToLongRepresentation()).ToList();
			var sequenceIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).Select(a => a.SequenceId).ToList();
			
			return this.QueryAll(db => {

				return db.StandardAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToList();
			}, sequenceIds);

		}

		public void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
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