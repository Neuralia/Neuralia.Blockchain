using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IStandardAccountSnapshotSqliteContext : IAccountSnapshotSqliteContext, IStandardAccountSnapshotContext {
	}

	public interface IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteContext
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeSqliteEntry, new() {
	}

	public abstract class StandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : AccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new() {

		public override string GroupRoot => "standard-accounts-snapshots";

		public DbSet<STANDARD_ACCOUNT_SNAPSHOT> StandardAccountSnapshots { get; set; }
		public DbSet<ACCOUNT_ATTRIBUTE_SNAPSHOT> StandardAccountSnapshotAttributes { get; set; }
	}
}