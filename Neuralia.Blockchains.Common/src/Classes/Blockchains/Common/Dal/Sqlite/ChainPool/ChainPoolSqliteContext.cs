using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainPool {
	public interface IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS> : ISqliteDbContext, IChainPoolContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {

		DbSet<CHAIN_POOL_PUBLIC_TRANSACTIONS> PublicTransactions { get; set; }
	}

	public abstract class ChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS> : SqliteDbContext, IChainPoolSqliteContext<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {

		protected override string DbName => "chain-pool.db";

		public DbSet<CHAIN_POOL_PUBLIC_TRANSACTIONS> PublicTransactions { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<CHAIN_POOL_PUBLIC_TRANSACTIONS>(eb => {
				eb.HasKey(c => c.TransactionId);
				eb.Property(b => b.TransactionId).ValueGeneratedNever();
				eb.HasIndex(b => b.TransactionId).IsUnique();
				eb.ToTable("PublicTransactions");
			});
		}
	}
}