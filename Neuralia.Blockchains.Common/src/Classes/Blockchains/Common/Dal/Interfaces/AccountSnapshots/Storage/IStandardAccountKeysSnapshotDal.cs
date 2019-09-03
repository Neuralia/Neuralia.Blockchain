using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IStandardAccountKeysSnapshotDal : IAccountKeysSnapshotDal {
	}

	public interface IStandardAccountKeysSnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where ACCOUNT_KEYS_SNAPSHOT_CONTEXT : IStandardAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new() {
	}
}