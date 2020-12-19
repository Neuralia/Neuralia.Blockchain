using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : IJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>, IAccountSnapshotEntry<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : IAccountAttributeEntry
		where JOINT_MEMBER_FEATURE : IJointMemberAccountEntry {
	}
}