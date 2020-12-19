using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IAccountSnapshotSqliteEntry : IAccountSnapshotEntry {
	}

	public interface IAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE> : IAccountSnapshotEntry<ACCOUNT_ATTRIBUTE>, IAccountSnapshotSqliteEntry
		where ACCOUNT_ATTRIBUTE : IAccountAttributeEntry {
	}

}