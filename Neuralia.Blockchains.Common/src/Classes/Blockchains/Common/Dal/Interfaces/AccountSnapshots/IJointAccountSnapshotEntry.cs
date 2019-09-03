using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IJointAccountSnapshotEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : IJointAccountSnapshot<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>, IAccountSnapshotEntry<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : IAccountFeatureEntry
		where JOINT_MEMBER_FEATURE : IJointMemberAccountEntry {
	}
}