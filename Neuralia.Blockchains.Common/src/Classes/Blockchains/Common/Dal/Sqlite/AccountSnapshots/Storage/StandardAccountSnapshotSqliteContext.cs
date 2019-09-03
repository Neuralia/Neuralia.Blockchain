using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IStandardAccountSnapshotSqliteContext : IAccountSnapshotSqliteContext, IStandardAccountSnapshotContext {
	}

	public interface IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IStandardAccountSnapshotSqliteContext
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureSqliteEntry, new() {
	}

	public abstract class StandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : AccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : AccountFeatureSqliteEntry, new() {

		public override string GroupRoot => "simple-accounts-snapshots";

		public DbSet<STANDARD_ACCOUNT_SNAPSHOT> StandardAccountSnapshots { get; set; }
		public DbSet<ACCOUNT_FEATURE_SNAPSHOT> StandardAccountSnapshotAttributes { get; set; }
	}
}