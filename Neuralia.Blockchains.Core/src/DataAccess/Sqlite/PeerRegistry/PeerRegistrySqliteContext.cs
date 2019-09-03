using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.PeerRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.PeerRegistry {
	public interface IPeerRegistrySqliteContext : ISqliteDbContext, IPeerRegistryContext {
		DbSet<PeerRegistryEntry> Peers { get; set; }
	}

	public class PeerRegistrySqliteContext : SqliteDbContext, IPeerRegistrySqliteContext {

		protected override string DbName => "peer-registry.db";

		public DbSet<PeerRegistryEntry> Peers { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			modelBuilder.Entity<PeerRegistryEntry>().HasKey(c => c.Ip);
			modelBuilder.Entity<PeerRegistryEntry>().Property(b => b.Ip).ValueGeneratedNever();
			modelBuilder.Entity<PeerRegistryEntry>().HasIndex(b => b.Ip).IsUnique();

		}
	}
}