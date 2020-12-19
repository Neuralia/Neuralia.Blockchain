using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotContext : IAccountSnapshotContext {
	}

	public interface IJointAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT> : IJointAccountSnapshotContext
		where ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new() {

		DbSet<ACCOUNT_SNAPSHOT> JointAccountSnapshots { get; set; }
		DbSet<ACCOUNT_ATTRIBUTE_SNAPSHOT> JointAccountSnapshotAttributes { get; set; }
	}
}