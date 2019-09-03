using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotContext : IAccountSnapshotContext {
	}

	public interface IJointAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT> : IJointAccountSnapshotContext
		where ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<ACCOUNT_FEATURE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
		where ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new() {

		DbSet<ACCOUNT_SNAPSHOT> JointAccountSnapshots { get; set; }
		DbSet<ACCOUNT_FEATURE_SNAPSHOT> JointAccountSnapshotAttributes { get; set; }
	}
}