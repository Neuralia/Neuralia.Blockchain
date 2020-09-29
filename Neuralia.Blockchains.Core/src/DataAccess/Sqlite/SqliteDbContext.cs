using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Extensions;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface ISqliteDbContext : IEntityFrameworkContext {

		string FolderPath { get; set; }

		string GetDbPath();

		Task Vacuum();
	}

	public abstract class SqliteDbContext : EntityFrameworkContext<DbContext>, ISqliteDbContext {

		protected abstract string DbName { get; }

		public List<Action<ModelBuilder>> ModelBuilders { get; } = new List<Action<ModelBuilder>>();

		public string FolderPath { get; set; } = null;

		public string GetDbPath() {
			return Path.Combine(this.FolderPath, this.FormatFilename());
		}

		/// <summary>
		///     Ensure the database and tables have been created
		/// </summary>
		public override void EnsureCreated() {

			FileExtensions.EnsureDirectoryStructure(this.FolderPath);

			base.EnsureCreated();
		}

		protected virtual string FormatFilename() {
			return this.DbName;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
			optionsBuilder.UseSqlite($"Data Source={this.GetDbPath()}");
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<DBVersion>(eb => {
				eb.HasKey(c => c.Id);
				eb.ToTable("Version");
			});

			foreach(Action<ModelBuilder> builder in this.ModelBuilders) {
				if(builder != null) {
					builder(modelBuilder);
				}
			}
		}

		public Task Vacuum() {
			return this.Context.Database.ExecuteSqlCommandAsync("VACUUM;");
		}
	}
}