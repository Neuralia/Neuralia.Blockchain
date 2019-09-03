using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IAccountSnapshotEntry : IAccountSnapshot {
	}

	public interface IAccountSnapshotEntry<ACCOUNT_FEATURE> : IAccountSnapshot<ACCOUNT_FEATURE>, IAccountSnapshotEntry
		where ACCOUNT_FEATURE : IAccountFeatureEntry {
	}

}