using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {

	public class JointAccountSnapshotEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : JointAccountSnapshot<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>, IJointAccountSnapshotEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_FEATURE : IAccountFeatureEntry
		where JOINT_MEMBER_FEATURE : IJointMemberAccountEntry {
	}
}