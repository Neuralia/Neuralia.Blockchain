using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base {

	public interface IAccountSnapshotSqliteContext : IIndexedSqliteDbContext, IAccountSnapshotContext {
	}

	public abstract class AccountSnapshotSqliteContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IndexedSqliteDbContext, IAccountSnapshotSqliteContext
		where ACCOUNT_SNAPSHOT : class, IAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new() {

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<ACCOUNT_SNAPSHOT>(eb => {
				eb.HasKey(c => c.AccountId);
				eb.Property(b => b.AccountId).ValueGeneratedNever();
				eb.HasIndex(b => b.AccountId).IsUnique();

				eb.Ignore(b => b.AppliedAttributes);

				eb.ToTable("AccountSnapshot");
			});

			modelBuilder.Entity<ACCOUNT_ATTRIBUTE_SNAPSHOT>(eb => {
				eb.HasKey(c => new {c.CorrelationId, FeatureType = c.AttributeType, c.AccountId});

				eb.HasIndex(c => c.CorrelationId);
				eb.HasIndex(c => c.AttributeType);
				eb.HasIndex(c => c.AccountId);

				eb.Property(b => b.AttributeType).HasConversion(v => v.Value, v => (AccountAttributeType) v);

				eb.ToTable("AccountAttributes");
			});

		}
	}
}