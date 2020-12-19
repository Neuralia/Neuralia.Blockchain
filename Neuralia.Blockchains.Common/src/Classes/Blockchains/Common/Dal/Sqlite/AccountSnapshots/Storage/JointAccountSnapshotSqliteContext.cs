using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IJointAccountSnapshotSqliteContext : IAccountSnapshotSqliteContext, IJointAccountSnapshotContext {
	}

	public interface IJointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, IJointAccountSnapshotSqliteContext
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountSqliteEntry, new() {
	}

	public abstract class JointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : AccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IJointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT : JointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : JointMemberAccountSqliteEntry, new() {

		public override string GroupRoot => "joint-accounts-snapshots";

		public DbSet<JOINT_ACCOUNT_SNAPSHOT> JointAccountSnapshots { get; set; }
		public DbSet<ACCOUNT_ATTRIBUTE_SNAPSHOT> JointAccountSnapshotAttributes { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<JOINT_ACCOUNT_MEMBERS_SNAPSHOT>(eb => {
				eb.HasKey(c => c.AccountId);
				eb.Property(b => b.AccountId).ValueGeneratedNever();
				eb.HasIndex(b => b.AccountId).IsUnique();

				eb.ToTable("JointAccountMembers");
			});
		}
	}
}