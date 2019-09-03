using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base {

	public interface IAccountSnapshotSqliteContext : IIndexedSqliteDbContext, IAccountSnapshotContext {
	}

	public abstract class AccountSnapshotSqliteContext<ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT> : IndexedSqliteDbContext, IAccountSnapshotSqliteContext
		where ACCOUNT_SNAPSHOT : class, IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : AccountFeatureSqliteEntry, new() {

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<ACCOUNT_SNAPSHOT>(eb => {
				eb.HasKey(c => c.AccountId);
				eb.Property(b => b.AccountId).ValueGeneratedNever();
				eb.HasIndex(b => b.AccountId).IsUnique();

				eb.Ignore(b => b.AppliedFeatures);

				eb.ToTable("AccountSnapshot");
			});

			modelBuilder.Entity<ACCOUNT_FEATURE_SNAPSHOT>(eb => {
				eb.HasKey(c => c.CertificateId);

				eb.ToTable("Attributes");
			});

		}
	}
}