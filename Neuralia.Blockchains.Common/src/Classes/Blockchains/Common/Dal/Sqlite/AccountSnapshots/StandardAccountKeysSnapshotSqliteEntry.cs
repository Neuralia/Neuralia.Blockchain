using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IStandardAccountKeysSnapshotSqliteEntry : IAccountKeysSnapshotSqliteEntry, IStandardAccountKeysSnapshotEntry {
	}

	public class StandardAccountKeysSnapshotSqliteEntry : AccountKeysSnapshotSqliteEntry, IStandardAccountKeysSnapshotSqliteEntry {
	}
}