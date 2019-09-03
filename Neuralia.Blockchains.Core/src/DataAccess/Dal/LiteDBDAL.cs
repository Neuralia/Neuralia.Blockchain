using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using LiteDB;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {
	public class LiteDBDAL {
		private readonly MemoryStream filedata;

		private readonly string filename;

		static LiteDBDAL() {
			// register mapping types
			BsonMapper.Global.RegisterType(wrapper => wrapper.Bytes, bson => {
				return new ByteArray(bson.AsBinary);
			});
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

		protected void Open(Action<LiteDatabase> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				process(db);
			}
		}

		protected T Open<T>(Func<LiteDatabase, T> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}

		protected List<T> Open<T>(Func<LiteDatabase, List<T>> process) {
			using(LiteDatabase db = this.GetDatabase()) {
				return process(db);
			}
		}

		public List<string> GetCollectionNames() {
			return this.Open(db => db.GetCollectionNames().ToList());
		}

		public bool CollectionExists<T>() {
			return this.CollectionExists<T>(typeof(T).Name);
		}

		public bool CollectionExists<T>(string tablename) {
			return this.Open(db => db.CollectionExists(tablename));
		}

		public void CreateDbFile<T, K>(Expression<Func<T, K>> index) {
			this.CreateDbFile(typeof(T).Name, index);
		}

		public void CreateDbFile<T, K>(string tablename, Expression<Func<T, K>> index) {
			this.Open(db => {
				var col = this.EnsureCollectionExists<T>(db, tablename);

				// just an empty file
				col.EnsureIndex(index, true);
			});
		}

		public int Count<T>() {
			return this.Count<T>(typeof(T).Name);
		}

		public int Count<T>(string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

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
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Count(predicate);
			});
		}

		public bool Any<T>() {
			return this.Any<T>(typeof(T).Name);
		}

		public bool Any<T>(string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Count() > 0;
			});
		}

		public bool Any<T>(Expression<Func<T, bool>> predicate) {
			return this.Any(predicate, typeof(T).Name);
		}

		public bool Any<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

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
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindAll().SingleOrDefault();
			});
		}

		public bool Exists<T>(Expression<Func<T, bool>> predicate) {
			return this.Exists(predicate, typeof(T).Name);
		}

		public bool Exists<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Exists(predicate);
			});
		}

		public T GetSingle<T, K>(Expression<Func<T, bool>> predicate) {
			return this.GetSingle<T, K>(predicate, typeof(T).Name);
		}

		public T GetSingle<T, K>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

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
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindAll().ToList();
			});
		}

		public IEnumerable<T> All<T>() {
			return this.All<T>(typeof(T).Name);
		}

		public IEnumerable<T> All<T>(string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindAll();
			});
		}

		public List<T> Get<T>(Expression<Func<T, bool>> predicate) {
			return this.Get(predicate, typeof(T).Name);
		}

		public List<T> Get<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

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
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Find(predicate).Select(selector).ToList();
			});
		}

		public void Insert<T, K>(T item, Expression<Func<T, K>> index) {
			this.Insert(item, typeof(T).Name, index);
		}

		public void Insert<T, K>(T item, string tablename, Expression<Func<T, K>> index) {
			this.Open(db => {
				var col = this.EnsureCollectionExists<T>(db, tablename);

				col.EnsureIndex(index, true);

				col.Insert(item);
			});
		}

		public void Insert<T, K>(List<T> items, Expression<Func<T, K>> index) {
			this.Insert(items, typeof(T).Name, index);
		}

		public void Insert<T, K>(List<T> items, string tablename, Expression<Func<T, K>> index) {
			this.Open(db => {
				var col = this.EnsureCollectionExists<T>(db, tablename);

				col.EnsureIndex(index, true);

				col.InsertBulk(items);
			});
		}

		public bool Update<T>(T item) {
			return this.Update(item, typeof(T).Name);
		}

		public bool Update<T>(T item, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Update(item);
			});
		}

		public void Updates<T>(List<T> items) {
			this.Updates(items, typeof(T).Name);
		}

		public void Updates<T>(List<T> items, string tablename) {
			this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

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

		public int Remove<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.Delete(predicate);
			});
		}

		public T GetOne<T>(Expression<Func<T, bool>> predicate) {
			return this.GetOne(predicate, typeof(T).Name);
		}

		public T GetOne<T>(Expression<Func<T, bool>> predicate, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return col.FindOne(predicate);
			});
		}

		public K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector) {
			return this.GetOne(predicate, selector, typeof(T).Name);
		}

		public K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector, string tablename) {
			return this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return default;
				}

				return selector(col.FindOne(predicate));
			});
		}

		private LiteCollection<T> GetExistingCollection<T>(LiteDatabase db, string tablename) {
			if(!db.CollectionExists(tablename)) {
				return null;
			}

			return this.EnsureCollectionExists<T>(db, tablename);
		}

		private LiteCollection<T> EnsureCollectionExists<T>(LiteDatabase db, string tablename) {
			return db.GetCollection<T>(tablename);
		}

		public void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort)
			where TKey : IEquatable<TKey> {

			this.Trim(keep, getKey, sort, typeof(T).Name);
		}

		public void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort, string tablename)
			where TKey : IEquatable<TKey> {
			this.Open(db => {
				var col = this.GetExistingCollection<T>(db, tablename);

				if(col == null) {
					return;
				}

				var all = col.FindAll();
				all = sort?.Invoke(all);

				foreach(T overflow in all.Skip(keep)) {
					col.Delete(e => getKey(e).Equals(getKey(overflow)));
				}
			});
		}
	}
}