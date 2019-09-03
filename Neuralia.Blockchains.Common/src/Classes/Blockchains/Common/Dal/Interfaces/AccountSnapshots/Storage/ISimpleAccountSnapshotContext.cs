using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IStandardAccountSnapshotContext : IAccountSnapshotContext {
	}

	public interface IStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IStandardAccountSnapshotContext
		where ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new() {

		DbSet<ACCOUNT_SNAPSHOT> StandardAccountSnapshots { get; set; }
		DbSet<ACCOUNT_FEATURE_SNAPSHOT> StandardAccountSnapshotAttributes { get; set; }
	}

}