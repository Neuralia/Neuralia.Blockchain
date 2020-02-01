using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IJointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>, IAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : class, IAccountAttributeSqliteEntry, new()
		where JOINT_MEMBER_FEATURE : class, IJointMemberAccountSqliteEntry, new() {
	}

	/// <summary>
	///     Here we store various metadata state about our chain
	/// </summary>
	public abstract class JointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : JointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>, IJointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_ATTRIBUTE : AccountAttributeSqliteEntry, new()
		where JOINT_MEMBER_FEATURE : JointMemberAccountSqliteEntry, new() {
	}

}