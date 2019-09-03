using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IStandardAccountKeysSnapshotContext : IAccountKeysSnapshotContext {
	}

	public interface IStandardAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IStandardAccountKeysSnapshotContext, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new() {
	}
}