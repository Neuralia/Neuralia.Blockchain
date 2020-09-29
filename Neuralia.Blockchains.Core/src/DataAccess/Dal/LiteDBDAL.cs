using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {
	public class LiteDBDAL {
		private readonly MemoryStream filedata;

		private readonly string filename;

		static LiteDBDAL() {

		}

		public LiteDBDAL(string filename) {
			this.filename = filename;
		}

		public LiteDBDAL(MemoryStream filedata) {
			this.filedata = filedata;
		}

		public static LiteDBDAL GetLiteDBDAL(string filepath) {
			return new LiteDBDAL(filepath);
		}

		public static LiteDBDAL GetLiteDBDAL(MemoryStream filedata) {
			filedata.Position = 0;

			return new LiteDBDAL(filedata);
		}

		protected LiteDatabase GetDatabase() {
			if(!string.IsNullOrWhiteSpace(this.filename)) {
				return new LiteDatabase(this.filename);
			}

			if(this.filedata != null) {
				return new LiteDatabase(this.filedata);
			}

			throw new ApplicationException("Invalid database creation options");
		}

		public void Open(Action<LiteDatabase> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				process(db);
			}
		}

		public T Open<T>(Func<LiteDatabase, T> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}

		public Task OpenAsync(Func<LiteDatabase, Task> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}
		
		public Task<T> OpenAsync<T>(Func<LiteDatabase, Task<T>> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}

		public List<T> Open<T>(Func<LiteDatabase, List<T>> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}

		public List<string> GetCollectionNames() {
			return this.Open(db => db.GetCollectionNames().ToList());
		}

		public bool CollectionExists<T>(LiteDatabase db = null) {
			return this.CollectionExists<T>(typeof(T).Name, db);
		}

		public bool CollectionExists<T>(string tablename, LiteDatabase db = null) {
			bool Action(LiteDatabase dbx) {
				return dbx.CollectionExists(tablename);
			}

			return db != null ? Action(db) : this.Open(Action);
		}

		public void CreateDbFile<T, K>(Expression<Func<T, K>> index) {
			this.CreateDbFile(typeof(T).Name, index);
		}

		public void CreateDbFile<T, K>(string tablename, Expression<Func<T, K>> index) {
			this.Open(db => {
				ILiteCollection<T> col = this.EnsureCollectionExists<T>(db, tablename);

				// just an empty file
				col.EnsureIndex(index, true);
			});
		}

		public int Count<T>() {
			return this.Count<T>(typeof(T).Name);
		}

		public int Count<T>(string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Count();
			});
		}

		public int Count<T>(Expression<Func<T, bool>> predicate) {
			return this.Count(predicate, typeof(T).Name);
		}

		public int Count<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Count(predicate);
			});
		}

		public bool Any<T>(LiteDatabase ldb = null) {
			return this.Any<T>(typeof(T).Name, ldb);
		}

		public bool Any<T>(string tablename, LiteDatabase ldb = null) {

			bool Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.GetExistingCollection<T>(dbx, tablename);

				if(col == null) {
					return default;
				}

				return col.Count() > 0;
			}

			return ldb != null ? Action(ldb) : this.Open(Action);
		}

		public bool Any<T>(Expression<Func<T, bool>> predicate) {
			return this.Any(predicate, typeof(T).Name);
		}

		public bool Any<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Count(predicate) > 0;
			});
		}

		public T GetSingle<T>() {
			return this.GetSingle<T>(typeof(T).Name);
		}

		public T GetSingle<T>(string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindAll().SingleOrDefault();
			});
		}

		public bool Exists<T>(Expression<Func<T, bool>> predicate, LiteDatabase ldb = null) {
			return this.Exists(predicate, typeof(T).Name, ldb);
		}

		public bool Exists<T>(Expression<Func<T, bool>> predicate, string tablename, LiteDatabase ldb = null) {

			bool Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.GetExistingCollection<T>(dbx, tablename);

				return col?.Exists(predicate) ?? default;

			}

			return ldb != null ? Action(ldb) : this.Open(Action);
		}

		public T GetSingle<T, K>(Expression<Func<T, bool>> predicate) {
			return this.GetSingle<T, K>(predicate, typeof(T).Name);
		}

		public T GetSingle<T, K>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindOne(predicate);
			});
		}

		public List<T> GetAll<T>() {
			return this.GetAll<T>(typeof(T).Name);
		}

		public List<T> GetAll<T>(string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindAll().ToList();
			});
		}

		public IEnumerable<T> All<T>(LiteDatabase ldb = null) {
			return this.All<T>(typeof(T).Name, ldb);
		}

		public IEnumerable<T> All<T>(string tablename, LiteDatabase ldb = null) {

			IEnumerable<T> Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.GetExistingCollection<T>(dbx, tablename);

				return col?.FindAll();
			}

			;

			return ldb != null ? Action(ldb) : this.Open(Action);
		}

		public List<T> Get<T>(Expression<Func<T, bool>> predicate) {
			return this.Get(predicate, typeof(T).Name);
		}

		public List<T> Get<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Find(predicate).ToList();
			});
		}

		public List<K> Get<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector) {
			return this.Get(predicate, selector, typeof(T).Name);
		}

		public List<K> Get<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Find(predicate).Select(selector).ToList();
			});
		}

		public void Insert<T, K>(T item, Expression<Func<T, K>> index, LiteDatabase ldb = null) {
			this.Insert(item, typeof(T).Name, index, ldb);
		}

		public void Insert<T, K>(T item, string tablename, Expression<Func<T, K>> index, LiteDatabase ldb = null) {

			void Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.EnsureCollectionExists<T>(dbx, tablename);

				col.EnsureIndex(index, true);

				col.Insert(item);
			}

			if(ldb == null) {
				this.Open(Action);
			} else {
				Action(ldb);
			}
		}

		public void Insert<T, K>(List<T> items, Expression<Func<T, K>> index) {
			this.Insert(items, typeof(T).Name, index);
		}

		public void Insert<T, K>(List<T> items, string tablename, Expression<Func<T, K>> index) {
			this.Open(db => {
				ILiteCollection<T> col = this.EnsureCollectionExists<T>(db, tablename);

				col.EnsureIndex(index, true);

				col.InsertBulk(items);
			});
		}

		public bool Update<T>(T item, LiteDatabase ldb = null) {
			return this.Update(item, typeof(T).Name, ldb);
		}

		public bool Update<T>(T item, string tablename, LiteDatabase ldb = null) {

			bool Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.GetExistingCollection<T>(dbx, tablename);

				return col?.Update(item) ?? default;

			}

			return ldb != null ? Action(ldb) : this.Open(Action);
		}

		public void Updates<T>(List<T> items) {
			this.Updates(items, typeof(T).Name);
		}

		public void Updates<T>(List<T> items, string tablename) {
			this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return;
				}

				foreach(T item in items) {
					col.Update(item);
				}
			});
		}

		public int Remove<T>(Expression<Func<T, bool>> predicate) {
			return this.Remove(predicate, typeof(T).Name);
		}

		public int Remove<T>(Expression<Func<T, bool>> predicate, LiteDatabase ldb) {
			return this.Remove(predicate, typeof(T).Name, ldb);
		}
		
		public int Remove<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				return this.Remove(predicate, tablename, db);
			});
		}
		
		public int Remove<T>(Expression<Func<T, bool>> predicate, string tablename, LiteDatabase ldb ) {
			ILiteCollection<T> col = this.GetExistingCollection<T>(ldb, tablename);

			if(col == null) {
				return default;
			}

			return col.DeleteMany(predicate);
		}

		public T GetOne<T>(Expression<Func<T, bool>> predicate, LiteDatabase ldb = null) {
			return this.GetOne(predicate, typeof(T).Name, ldb);
		}

		public T GetOne<T>(Expression<Func<T, bool>> predicate, string tablename, LiteDatabase ldb = null) {

			T Action(LiteDatabase dbx) {
				ILiteCollection<T> col = this.GetExistingCollection<T>(dbx, tablename);

				return col == null ? default : col.FindOne(predicate);

			}

			return ldb != null ? Action(ldb) : this.Open(Action);
		}

		public K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector) {
			return this.GetOne(predicate, selector, typeof(T).Name);
		}

		public K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector, string tablename) {
			return this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return selector(col.FindOne(predicate));
			});
		}

		private ILiteCollection<T> GetExistingCollection<T>(LiteDatabase db, string tablename) {
			if(!db.CollectionExists(tablename)) {
				return null;
			}

			return this.EnsureCollectionExists<T>(db, tablename);
		}

		private ILiteCollection<T> EnsureCollectionExists<T>(LiteDatabase db, string tablename) {
			return db.GetCollection<T>(tablename);
		}

		public void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort)
			where TKey : IEquatable<TKey> {

			this.Trim(keep, getKey, sort, typeof(T).Name);
		}

		public void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort, string tablename)
			where TKey : IEquatable<TKey> {
			this.Open(db => {
				ILiteCollection<T> col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return;
				}

				IEnumerable<T> all = col.FindAll();

				if(sort != null) {
					all = sort(all);
				}

				foreach(T overflow in all.Skip(keep)) {
					col.DeleteMany(e => getKey(e).Equals(getKey(overflow)));
				}
			});
		}
	}
}