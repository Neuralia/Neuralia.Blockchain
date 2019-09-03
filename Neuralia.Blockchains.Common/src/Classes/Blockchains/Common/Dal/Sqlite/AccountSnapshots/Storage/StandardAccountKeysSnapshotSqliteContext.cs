using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IStandardAccountKeysSnapshotSqliteContext : IAccountKeysSnapshotSqliteContext, IStandardAccountKeysSnapshotContext {
	}

	public interface IStandardAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>, IStandardAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>, IStandardAccountKeysSnapshotSqliteContext
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotSqliteEntry, new() {
	}

	public abstract class StandardAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : AccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>, IStandardAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotSqliteEntry, new() {

		public override string GroupRoot => "standard-account-keys-snapshots";
	}
}