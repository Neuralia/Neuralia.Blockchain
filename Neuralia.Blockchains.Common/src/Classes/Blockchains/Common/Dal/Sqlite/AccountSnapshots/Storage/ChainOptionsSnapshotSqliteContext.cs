using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IChainOptionsSnapshotSqliteContext : ISqliteDbContext, IChainOptionsSnapshotContext {
	}

	public interface IChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT> : IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>, IChainOptionsSnapshotSqliteContext
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotSqliteEntry, new() {
	}

	public abstract class ChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT> : SqliteDbContext, IChainOptionsSnapshotSqliteContext<CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotSqliteEntry, new() {

		protected override string DbName => "chain-options-snapshots.db";

		public DbSet<CHAIN_OPTIONS_SNAPSHOT> ChainOptionsSnapshots { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<CHAIN_OPTIONS_SNAPSHOT>(eb => {
				eb.HasKey(c => c.Id);
				eb.Property(b => b.Id).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.Id).IsUnique();
				eb.ToTable("ChainOptions");
			});

		}
	}
}