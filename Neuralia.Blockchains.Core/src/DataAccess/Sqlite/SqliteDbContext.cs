using System.IO;
using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface ISqliteDbContext : IEntityFrameworkContext {

		string FolderPath { get; set; }

		string GetDbPath();
	}

	public abstract class SqliteDbContext : EntityFrameworkContext<DbContext>, ISqliteDbContext {

		protected abstract string DbName { get; }

		public string FolderPath { get; set; } = null;

		public string GetDbPath() {
			return Path.Combine(this.FolderPath, this.FormatFilename());
		}

		/// <summary>
		///     Ensure the database and tables have been created
		/// </summary>
		public override void EnsureCreated() {

			new DirectoryInfo(this.FolderPath).Create();

			base.EnsureCreated();
		}

		protected virtual string FormatFilename() {
			return this.DbName;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
			optionsBuilder.UseSqlite($"Data Source={this.GetDbPath()}");
		}
	}
}