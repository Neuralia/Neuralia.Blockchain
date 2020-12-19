using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {
	public class StandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE> : StandardAccountSnapshot<ACCOUNT_ATTRIBUTE>, IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : AccountAttributeEntry {
	}
}