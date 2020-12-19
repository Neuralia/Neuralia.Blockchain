using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {
	public interface ISqliteDal<DBCONTEXT> : IExtendedEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : ISqliteDbContext {
	}

	public abstract class SqliteDal<DBCONTEXT> : ExtendedEntityFrameworkDal<DBCONTEXT>, ISqliteDal<DBCONTEXT>
		where DBCONTEXT : DbContext, ISqliteDbContext {

		protected static readonly Dictionary<Type, HashSet<string>> DbCreatedCache = new Dictionary<Type, HashSet<string>>();

		protected readonly string folderPath; // if null, it will use the wallet path

		public SqliteDal(string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(serviceSet, softwareVersion, contextInstantiator, serializationType) {
			this.folderPath = folderPath;
		}

		public async Task<(DBCONTEXT db, IDbContextTransaction transaction)> BeginHoldingTransaction() {

			DBCONTEXT db = this.CreateContext();

			IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);

			return (db, transaction);
		}

		public virtual Task Clear() {

			return this.PerformInnerContextOperationAsync(ctx => {

				ctx.FolderPath = this.folderPath;
				string path = this.GetDbPath(ctx);

				if(File.Exists(path)) {
					File.Delete(path);
				}

				return Task.CompletedTask;
			});
		}

		protected virtual string GetDbPath(DBCONTEXT ctx) {
			return ctx.GetDbPath();
		}

		protected virtual void EnsureDatabaseCreated(DBCONTEXT ctx) {

			string path = this.GetDbPath(ctx);

			Type type = this.GetType();

			if(!DbCreatedCache.ContainsKey(type)) {
				DbCreatedCache.Add(type, new HashSet<string>());
			}

			if(!DbCreatedCache[type].Contains(path)) {
				if(!File.Exists(path)) {

					NLog.Default.Verbose("Ensuring that the Sqlite database '{0}' exists and tables are created", path);

					// let's make sure the database exists and is created
					ctx.EnsureCreated();

					this.EnsureVersionCreated(ctx);

					NLog.Default.Verbose("Sqlite database '{0}' structure creation completed.", path);
				}

				DbCreatedCache[type].Add(path);
			}
		}

		protected override void InitContext(DBCONTEXT db) {
			db.FolderPath = this.folderPath;

			base.InitContext(db);

			this.EnsureDatabaseCreated(db);
			
			int cacheSize = 10000;
			int pageSize = 4096;
			
			// perform some optimizations
			db.Database.ExecuteSqlRaw("pragma journal_mode = WAL;");
			db.Database.ExecuteSqlRaw("pragma synchronous = normal;");
			db.Database.ExecuteSqlRaw("pragma temp_store = memory;");
			db.Database.ExecuteSqlRaw("pragma mmap_size = 30000000000;");
			db.Database.ExecuteSqlRaw($"pragma page_size = {pageSize};");
			db.Database.ExecuteSqlRaw($"pragma cache_size = {cacheSize};");
			db.Database.ExecuteSqlRaw($"pragma journal_size_limit = {cacheSize * pageSize};");
		}

		// <summary>
		/// in this case its easy, to clear the database, we delete the file and recreate it
		/// </summary>
		protected override void ClearDb() {
			this.PerformInnerContextOperation(ctx => {
				string dbfile = ctx.GetDbPath();

				if(File.Exists(dbfile)) {
					File.Delete(dbfile);
				}
			});
		}
	}
}