using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IAccountSnapshotSqliteEntry : IAccountSnapshotEntry {
	}

	public interface IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE> : IAccountSnapshotEntry<ACCOUNT_FEATURE>, IAccountSnapshotSqliteEntry
		where ACCOUNT_FEATURE : IAccountFeatureEntry {
	}

}