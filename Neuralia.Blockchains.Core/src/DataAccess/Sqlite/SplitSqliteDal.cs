using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface ISplitSqliteDal<DBCONTEXT> : ISqliteDal<DBCONTEXT>
		where DBCONTEXT : ISplitSqliteDbContext {
	}

	/// <summary>
	/// a special format that will split the contents in multiple databases of no more than 1 million entries each
	/// </summary>
	/// <typeparam name="DBCONTEXT"></typeparam>
	public abstract class SplitSqliteDal<DBCONTEXT> : SqliteDal<DBCONTEXT>, ISplitSqliteDal<DBCONTEXT>
		where DBCONTEXT : DbContext, ISplitSqliteDbContext {

		private string groupRoot;

		private int currentIndex = 0;
		
		protected SplitSqliteDal(string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, contextInstantiator, serializationType) {

		}

		public async Task InsertEntry<T>(T entry, Func<DBCONTEXT, DbSet<T>> getDbSet) 
			where T: class{
			this.currentIndex = await this.SplitIndex.NextIndex().ConfigureAwait(false);

			await this.PerformOperationAsync(async db => {

				getDbSet(db).Add(entry);

				await db.SaveChangesAsync().ConfigureAwait(false);
			}).ConfigureAwait(false);
			
			await this.SplitIndex.AddOne(this.currentIndex).ConfigureAwait(false);
			this.currentIndex = 0;
		}

		public async Task InsertEntries<T>(List<T> entries, Func<DBCONTEXT, DbSet<T>> getDbSet) 
			where T: class {

			int total = entries.Count;

			while(total > 0) {
				int blockSize = 0;
				(this.currentIndex, blockSize) = await this.SplitIndex.GetBlock().ConfigureAwait(false);

				var currentEntrie = entries.Take(blockSize).ToList();
				entries = entries.Skip(blockSize).ToList();
				
				await this.PerformOperationAsync(async db => {

					var dbSet = getDbSet(db);

					dbSet.AddRange(currentEntrie);

					await db.SaveChangesAsync().ConfigureAwait(false);
				}).ConfigureAwait(false);

				await this.SplitIndex.AddMany(this.currentIndex, currentEntrie.Count).ConfigureAwait(false);
				
				total -= blockSize;
			}
			
			this.currentIndex = 0;
		}

		public async Task<List<T>> SelectAll<T>(Expression<Func<T,bool>> predicate, Func<DBCONTEXT, DbSet<T>> getDbSet) where T: class {
			var indices = await this.SplitIndex.GetAll().ConfigureAwait(false);

			List<T> results = new List<T>();
			foreach(var index in indices) {
				this.currentIndex = index;
				await this.PerformOperationAsync(async db => {

					var dbSet = getDbSet(db);

					var entries = await dbSet.Where(predicate).ToListAsync().ConfigureAwait(false);

					results.AddRange(entries);
					
				}).ConfigureAwait(false);
			}
			
			this.currentIndex = 0;

			return results;
		}

		private class Wrapper {
			public int entry;
		}
		public async Task DeleteAll<T>(Expression<Func<T,bool>> predicate, Func<DBCONTEXT, DbSet<T>> getDbSet) where T: class {
			var indices = await this.SplitIndex.GetAll().ConfigureAwait(false);

			foreach(var index in indices) {
				this.currentIndex = index;
				Wrapper removed = new Wrapper();
				await this.PerformOperationAsync(async db => {

					removed.entry = 0;
					var dbSet = getDbSet(db);

					var entries = await dbSet.Where(predicate).ToListAsync().ConfigureAwait(false);

					if(entries.Any()) {
						removed.entry = entries.Count;
						dbSet.RemoveRange(entries);

						await db.SaveChangesAsync().ConfigureAwait(false);

						await db.Vacuum().ConfigureAwait(false);
					}
				}).ConfigureAwait(false);
				
				await this.SplitIndex.RemoveMany(this.currentIndex, removed.entry).ConfigureAwait(false);
			}
			
			this.currentIndex = 0;
		}
		
		public async Task<bool> Any<T>(Expression<Func<T,bool>> predicate, Func<DBCONTEXT, DbSet<T>> getDbSet) where T: class {
			var indices = await this.SplitIndex.GetAll().ConfigureAwait(false);

			foreach(var index in indices) {
				this.currentIndex = index;
				
				bool any = await this.PerformOperationAsync(db => getDbSet(db).AnyAsync(predicate)).ConfigureAwait(false);

				this.currentIndex = 0;
				
				if(any) {
					return true;
				}
			}
			
			return false;
		} 
		
		public async Task<T> SelectOne<T>(Expression<Func<T,bool>> predicate, Func<DBCONTEXT, DbSet<T>> getDbSet) where T: class {
			var indices = await this.SplitIndex.GetAll().ConfigureAwait(false);

			foreach(var index in indices) {
				this.currentIndex = index;
				
				T entry = await this.PerformOperationAsync( db => getDbSet(db).SingleOrDefaultAsync(predicate)).ConfigureAwait(false);

				this.currentIndex = 0;
				
				if(entry != null) {
					return entry;
				}
			}

			return null;
		} 
		
		public async Task UpdateOne<T>(Expression<Func<T,bool>> predicate, Action<T> update, Func<DBCONTEXT, DbSet<T>> getDbSet) where T: class {
			var indices = await this.SplitIndex.GetAll().ConfigureAwait(false);

			foreach(var index in indices) {
				this.currentIndex = index;
				
				bool updated = await this.PerformOperationAsync( async db => {

					var entry = await getDbSet(db).SingleOrDefaultAsync(predicate).ConfigureAwait(false);
					
					if(entry != null) {
						update(entry);
						
						await db.SaveChangesAsync().ConfigureAwait(false);
						return true;
					}

					return false;
				}).ConfigureAwait(false);

				this.currentIndex = 0;

				if(updated) {
					return;
				}
			}
		} 
		
		

		protected override void InitContext(DBCONTEXT db) {
			
			db.SetIndex(this.currentIndex);
			
			base.InitContext(db);
		}

		private SplitIndexSqliteDbContext splitIndex;
		private SplitIndexSqliteDbContext SplitIndex {
			get {
				if(this.splitIndex == null) {
					this.splitIndex = new SplitIndexSqliteDbContext( "Index", this.folderPath);
					this.splitIndex.EnsureCreated();
				}
				return this.splitIndex;
			}
		}

		
		public class SplitIndexSqliteDbContext : SqliteDbContext {

			public async Task<int> NextIndex() {
				int? index = await this.Indices.Where(i => i.Total < SplitSqliteDbContext.GROUP_SIZE).OrderBy(i => i.Id).Select(i => (int?)i.Id).FirstOrDefaultAsync().ConfigureAwait(false);

				if(!index.HasValue) {
					this.Indices.Add(new IndexEntry());
					await this.SaveChangesAsync().ConfigureAwait(false);
					return  await this.Indices.Where(i => i.Total < SplitSqliteDbContext.GROUP_SIZE).OrderBy(i => i.Id).Select(i => i.Id).FirstAsync().ConfigureAwait(false);
				}

				return index.Value;
			}
			
			public Task AddOne(int index) {
				return this.AddMany(index, 1);
			}
			public async Task AddMany(int index, int count) {
				
				if(count == 0) {
					return;
				}
				
				var entry = await this.Indices.SingleAsync(e => e.Id == index).ConfigureAwait(false);
				
				entry.Total += count;

				if(entry.Total > SplitSqliteDbContext.GROUP_SIZE) {
					throw new ApplicationException("Total larger than maximum");
				}

				await this.SaveChangesAsync().ConfigureAwait(false);
			}
			
			public Task RemoveOne(int index) {
				return this.RemoveMany(index, 1);
			}
			public async Task RemoveMany(int index, int count) {
				if(count == 0) {
					return;
				}
				var entry = await this.Indices.SingleAsync(e => e.Id == index).ConfigureAwait(false);

				entry.Total -= count;

				if(entry.Total < 0) {
					entry.Total = 0;
				}

				await this.SaveChangesAsync().ConfigureAwait(false);
			}

			
			
			public async Task<List<int>> GetAll() {
				return await this.Indices.Select(i => i.Id).ToListAsync().ConfigureAwait(false);
			}
			
			

			
			public async Task<(int index, int blockSize)> GetBlock() {
				int blockSize = 0;
				int index = 0;
				
				var entry = await this.Indices.Where(i => i.Total < SplitSqliteDbContext.GROUP_SIZE).OrderBy(i => i.Id).FirstOrDefaultAsync().ConfigureAwait(false);

				if(entry == null) {
					blockSize = SplitSqliteDbContext.GROUP_SIZE;
					this.Indices.Add(new IndexEntry());
					await this.SaveChangesAsync().ConfigureAwait(false);
					index = await this.Indices.Where(i => i.Total < SplitSqliteDbContext.GROUP_SIZE).OrderBy(i => i.Id).Select(i => i.Id).FirstAsync().ConfigureAwait(false);
				} else {
					blockSize = SplitSqliteDbContext.GROUP_SIZE - entry.Total;
					index = entry.Id;
				}

				return (index, blockSize);
			}
			
			
			
			public class IndexEntry {

				public int Id { get; set; }
				public int Total { get; set; }
			}
			private readonly string dbName;
			public SplitIndexSqliteDbContext(string dbName, string folderPath) {
				this.dbName = dbName;
				this.FolderPath = folderPath;
			}

			protected override string DbName => this.dbName;
			
			public DbSet<IndexEntry> Indices { get; set; }
			
			protected override void OnModelCreating(ModelBuilder modelBuilder) {

				modelBuilder.Entity<IndexEntry>(eb => {
					eb.HasKey(c => c.Id);
					eb.Property(b => b.Id).ValueGeneratedOnAdd();
					eb.HasIndex(b => b.Id).IsUnique();
					eb.ToTable("Indices");
				});
			}
		}
	}
}