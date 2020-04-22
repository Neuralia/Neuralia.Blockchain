using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Zio.FileSystems;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface ISqliteDbContext : IEntityFrameworkContext {

		string FolderPath { get; set; }

		string GetDbPath();
		
		
	}

	public abstract class SqliteDbContext : EntityFrameworkContext<DbContext>, ISqliteDbContext {

		protected abstract string DbName { get; }

		public string FolderPath { get; set; } = null;

		public List<Action<ModelBuilder>> ModelBuilders { get; } = new List<Action<ModelBuilder>>();
		
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

			foreach(var builder in this.ModelBuilders) {
if(				builder != null){				builder(modelBuilder);}
			}
		}
	}
}