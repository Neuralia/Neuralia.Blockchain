using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {
	public class StandardAccountSnapshotEntry<ACCOUNT_FEATURE> : StandardAccountSnapshot<ACCOUNT_FEATURE>, IStandardAccountSnapshotEntry<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : AccountFeatureEntry {
	}
}