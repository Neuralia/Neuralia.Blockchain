using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using Microsoft.IO;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {
	public class LiteDatabaseWrapper : IFileDatabase {

		private readonly LiteDatabase liteDatabase;
		public LiteDatabaseWrapper(string filename) {
			this.liteDatabase = new LiteDatabase(filename);
			this.InitializeDb();
		}
		
		public LiteDatabaseWrapper(RecyclableMemoryStream ms) {
			this.liteDatabase = new LiteDatabase(ms);
			this.InitializeDb();
		}

		private void InitializeDb() {
			this.liteDatabase.UtcDate = true;
		}
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.liteDatabase.Dispose();
			}
			this.IsDisposed = true;
		}

		~LiteDatabaseWrapper() {
			this.Dispose(false);
		}

	#endregion
		
		public IEnumerable<string> GetCollectionNames() {
			return this.liteDatabase.GetCollectionNames();
		}

		public bool CollectionExists(string tableName) {
			return this.liteDatabase.CollectionExists(tableName);
		}

		public ILiteCollection<T> GetCollection<T>(string tablename) {
			return this.liteDatabase.GetCollection<T>(tablename);
		}
		
		public ILiteCollection<T> GetCollection<T>() {
			return this.liteDatabase.GetCollection<T>();
		}
	}
}