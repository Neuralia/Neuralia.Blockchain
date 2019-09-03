using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IJointAccountSnapshotSqliteEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : IJointAccountSnapshotEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>, IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : class, IAccountFeatureSqliteEntry, new()
		where JOINT_MEMBER_FEATURE : class, IJointMemberAccountSqliteEntry, new() {
	}

	/// <summary>
	///     Here we store various metadata state about our chain
	/// </summary>
	public abstract class JointAccountSnapshotSqliteEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : JointAccountSnapshotEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>, IJointAccountSnapshotSqliteEntry<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_FEATURE : AccountFeatureSqliteEntry, new()
		where JOINT_MEMBER_FEATURE : JointMemberAccountSqliteEntry, new() {
	}

}