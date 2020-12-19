using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface ITrackedAccountsSqliteContext : IIndexedSqliteDbContext, ITrackedAccountsContext {

		DbSet<TrackedAccountSqliteEntry> TrackedAccounts { get; set; }
	}

	public abstract class TrackedAccountsSqliteContext : IndexedSqliteDbContext, ITrackedAccountsSqliteContext {

		public override string GroupRoot => "tracked-accounts";

		public DbSet<TrackedAccountSqliteEntry> TrackedAccounts { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<TrackedAccountSqliteEntry>(eb => {
				eb.HasKey(c => c.AccountId);

				eb.ToTable("TrackedAccounts");
			});
		}
	}
}