using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {

	public class JointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : JointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>, IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_ATTRIBUTE : IAccountAttributeEntry
		where JOINT_MEMBER_FEATURE : IJointMemberAccountEntry {
	}
}