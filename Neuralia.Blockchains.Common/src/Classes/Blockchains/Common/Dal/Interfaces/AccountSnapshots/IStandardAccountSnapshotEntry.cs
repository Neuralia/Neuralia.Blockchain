using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IStandardAccountSnapshotEntry : IStandardAccountSnapshot, IAccountSnapshotEntry {
	}

	public interface IStandardAccountSnapshotEntry<ACCOUNT_FEATURE> : IStandardAccountSnapshot<ACCOUNT_FEATURE>, IAccountSnapshotEntry<ACCOUNT_FEATURE>, IStandardAccountSnapshotEntry
		where ACCOUNT_FEATURE : IAccountFeatureEntry {
	}

}