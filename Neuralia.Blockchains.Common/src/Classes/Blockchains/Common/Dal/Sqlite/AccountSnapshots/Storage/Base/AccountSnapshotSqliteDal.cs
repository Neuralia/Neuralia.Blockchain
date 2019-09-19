using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base {

	public interface IAccountSnapshotSqliteDal : IAccountSnapshotDal {
	}

	public interface IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IIndexedSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT>, IAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IAccountSnapshotSqliteContext
		where ACCOUNT_SNAPSHOT : class, IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureSqliteEntry {
	}

	public abstract class AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IndexedSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT>, IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : AccountSnapshotSqliteContext<ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : class, IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : AccountFeatureSqliteEntry, new() {

		protected AccountSnapshotSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, Func<AppSettingsBase.SerializationTypes, ACCOUNT_SNAPSHOT_CONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, contextInstantiator, serializationType) {
		}

		public abstract void InsertNewAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, long? correlationId);
	}
}