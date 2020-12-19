using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IStandardAccountSnapshotEntry : IStandardAccountSnapshot, IAccountSnapshotEntry {
	}

	public interface IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE> : IStandardAccountSnapshot<ACCOUNT_ATTRIBUTE>, IAccountSnapshotEntry<ACCOUNT_ATTRIBUTE>, IStandardAccountSnapshotEntry
		where ACCOUNT_ATTRIBUTE : IAccountAttributeEntry {
	}

}