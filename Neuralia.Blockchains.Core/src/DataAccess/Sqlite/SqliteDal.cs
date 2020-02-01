using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Serilog;

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

		public (DBCONTEXT db, IDbContextTransaction transaction) BeginHoldingTransaction() {

			DBCONTEXT db = this.CreateContext();

			IDbContextTransaction transaction = db.Database.BeginTransaction();

			return (db, transaction);
		}
		
		public virtual void Clear() {

			string path = "";
			using(DBCONTEXT ctx = this.CreateRawContext()) {
				ctx.FolderPath = this.folderPath;
				path = this.GetDbPath(ctx);
			}

			if(File.Exists(path)) {
				File.Delete(path);
			}
		}

		protected virtual string GetDbPath(DBCONTEXT ctx) {
			return ctx.GetDbPath();
		}

		protected virtual void EnsureDatabaseCreated(DBCONTEXT ctx) {

			string path = this.GetDbPath(ctx);

			var type = this.GetType();

			if(!DbCreatedCache.ContainsKey(type)) {
				DbCreatedCache.Add(type, new HashSet<string>());
			}
			if(!DbCreatedCache[type].Contains(path)) {
				if(!File.Exists(path)) {

					Log.Verbose("Ensuring that the Sqlite database '{0}' exists and tables are created", path);

					// let's make sure the database exists and is created
					ctx.EnsureCreated();

					this.EnsureVersionCreated(ctx);
					
					Log.Verbose("Sqlite database '{0}' structure creation completed.", path);
				}

				DbCreatedCache[type].Add(path);
			}
		}


		protected override void InitContext(DBCONTEXT db) {
			db.FolderPath = this.folderPath;

			base.InitContext(db);

			this.EnsureDatabaseCreated(db);
		}
		
		// <summary>
		/// in this case its easy, to clear the database, we delete the file and recreate it
		/// </summary>
		protected override void ClearDb() {
			using(DBCONTEXT ctx = this.CreateContext()) {

				string dbfile = ctx.GetDbPath();

				if(File.Exists(dbfile)) {
					File.Delete(dbfile);
				}
			}
		}
	}
}