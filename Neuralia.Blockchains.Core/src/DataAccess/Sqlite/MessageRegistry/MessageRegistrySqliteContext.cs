using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	public interface IMessageRegistrySqliteContext : ISqliteDbContext, IMessageRegistryContext {
		DbSet<MessageEntrySqlite> MessageEntries { get; set; }
		DbSet<PeerSqlite> Peers { get; set; }
		DbSet<UnvalidatedBlockGossipMessageCacheEntrySqlite> UnvalidatedBlockGossipMessageCacheEntries { get; set; }
	}

	public class MessageRegistrySqliteContext : SqliteDbContext, IMessageRegistrySqliteContext {

		protected override string DbName => "message-registry.db";
		public DbSet<MessageEntrySqlite> MessageEntries { get; set; }
		public DbSet<PeerSqlite> Peers { get; set; }
		public DbSet<UnvalidatedBlockGossipMessageCacheEntrySqlite> UnvalidatedBlockGossipMessageCacheEntries { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			modelBuilder.Entity<MessageEntrySqlite>(mb => {

				mb.HasKey(c => c.Hash);
				mb.Property(b => b.Hash).ValueGeneratedNever();
				mb.HasIndex(b => b.Hash).IsUnique();
				mb.Ignore(b => b.PeersBase);
				mb.ToTable("MessageEntries");
			});

			modelBuilder.Entity<PeerSqlite>(mb => {

				mb.HasKey(c => c.PeerKey);
				mb.Property(b => b.PeerKey).ValueGeneratedNever();
				mb.Ignore(b => b.MessagesBase);
				mb.ToTable("Peers");
			});

			modelBuilder.Entity<MessagePeerSqlite>(mb => {

				mb.HasKey(t => new {t.Hash, t.PeerKey});
				mb.Ignore(b => b.PeerBase);
				mb.Ignore(b => b.MessageBase);
				mb.ToTable("MessagePeer");

				mb.HasOne(pt => pt.Message).WithMany(p => p.Peers).HasForeignKey(pt => pt.Hash);
			});

			modelBuilder.Entity<UnvalidatedBlockGossipMessageCacheEntrySqlite>(mb => {

				mb.HasKey(c => c.Id);

				mb.ToTable("UnvalidatedBlockGossipMessageCacheEntry");
			});
		}
	}
}