using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {
	public interface ISqliteDal<DBCONTEXT> : IExtendedEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : ISqliteDbContext {
	}

	public abstract class SqliteDal<DBCONTEXT> : ExtendedEntityFrameworkDal<DBCONTEXT>, ISqliteDal<DBCONTEXT>
		where DBCONTEXT : DbContext, ISqliteDbContext {

		private static readonly HashSet<string> DbCreatedCache = new HashSet<string>();

		protected readonly string folderPath; // if null, it will use the wallet path

		public SqliteDal(string folderPath, ServiceSet serviceSet, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(serviceSet, contextInstantiator, serializationType) {
			this.folderPath = folderPath;

		}

		public (DBCONTEXT db, IDbContextTransaction transaction) BeginHoldingTransaction() {

			DBCONTEXT db = this.CreateContext();

			IDbContextTransaction transaction = db.Database.BeginTransaction();

			return (db, transaction);
		}

		protected virtual string GetDbPath(DBCONTEXT ctx) {
			return ctx.GetDbPath();
		}

		protected virtual void EnsureDatabaseCreated(DBCONTEXT ctx) {

			string path = this.GetDbPath(ctx);

			if(!DbCreatedCache.Contains(path)) {
				if(!File.Exists(path)) {

					Log.Verbose("Ensuring that the Sqlite database '{0}' exists and tables are created", path);

					// let's make sure the database exists and is created
					ctx.EnsureCreated();

					Log.Verbose("Sqlite database '{0}' structure creation completed.", path);
				}

				DbCreatedCache.Add(path);
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