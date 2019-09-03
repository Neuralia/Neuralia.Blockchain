using System;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base {
	public interface IAccountKeysSnapshotSqliteContext : IIndexedSqliteDbContext, IAccountKeysSnapshotContext {
	}

	public interface IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>, IAccountKeysSnapshotSqliteContext
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IAccountKeysSnapshotSqliteEntry, new() {
	}

	public abstract class AccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IndexedSqliteDbContext, IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IAccountKeysSnapshotSqliteEntry, new() {

		public DbSet<STANDARD_ACCOUNT_KEYS_SNAPSHOT> StandardAccountkeysSnapshots { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<STANDARD_ACCOUNT_KEYS_SNAPSHOT>(eb => {
				eb.HasKey(c => new Tuple<byte, long>(c.OrdinalId, c.AccountId));
				eb.ToTable("StandardAccountKeys");
			});

		}
	}
}