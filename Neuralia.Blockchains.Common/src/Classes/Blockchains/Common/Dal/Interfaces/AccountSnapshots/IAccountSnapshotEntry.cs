using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IAccountSnapshotEntry : IAccountSnapshot {
	}

	public interface IAccountSnapshotEntry<ACCOUNT_ATTRIBUTE> : IAccountSnapshot<ACCOUNT_ATTRIBUTE>, IAccountSnapshotEntry
		where ACCOUNT_ATTRIBUTE : IAccountAttributeEntry {
	}

}