using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainState {
	public interface IChainStateSqliteContext<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : ISqliteDbContext, IChainStateContext<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		DbSet<MODEL_SNAPSHOT> ChainMetadatas { get; set; }
		DbSet<MODERATOR_KEYS_SNAPSHOT> ChainModeratorKeys { get; set; }
	}

	public abstract class ChainStateSqliteContext<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : SqliteDbContext, IChainStateSqliteContext<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		protected override string DbName => "chain-state.db";

		public DbSet<MODEL_SNAPSHOT> ChainMetadatas { get; set; }
		public DbSet<MODERATOR_KEYS_SNAPSHOT> ChainModeratorKeys { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<MODEL_SNAPSHOT>(eb => {
				eb.HasKey(c => c.Id);
				eb.Property(b => b.Id).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.Id).IsUnique();
				eb.Property(e => e.BlockInterpretationStatus).HasConversion(v => (int) v, v => (ChainStateEntryFields.BlockInterpretationStatuses) v);
				eb.ToTable("ChainState");
			});

			modelBuilder.Entity<MODERATOR_KEYS_SNAPSHOT>(eb => {
				eb.HasKey(c => c.OrdinalId);
				eb.Property(b => b.OrdinalId).ValueGeneratedNever();
				eb.HasIndex(b => b.OrdinalId).IsUnique();
				eb.ToTable("ModeratorKeys");
			});

			modelBuilder.Entity<MODERATOR_KEYS_SNAPSHOT>().HasOne(pt => pt.ChainStateEntry).WithMany(p => p.ModeratorKeys).HasForeignKey(pt => pt.ChainStateId);
		}
	}
}