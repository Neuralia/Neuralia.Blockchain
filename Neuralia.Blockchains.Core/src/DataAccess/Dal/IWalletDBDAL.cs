using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {




	public interface IWalletDBDAL {
		
		void Open(Action<IFileDatabase> process);
		T Open<T>(Func<IFileDatabase, T> process);
		Task OpenAsync(Func<IFileDatabase, Task> process);
		Task<T> OpenAsync<T>(Func<IFileDatabase, Task<T>> process);
		List<T> Open<T>(Func<IFileDatabase, List<T>> process);
		List<string> GetCollectionNames();
		bool CollectionExists<T>(IFileDatabase db = null);
		bool CollectionExists<T>(string tablename, IFileDatabase db = null);
		void CreateDbFile<T, K>(Expression<Func<T, K>> index);
		void CreateDbFile<T, K>(string tablename, Expression<Func<T, K>> index);
		int Count<T>();
		int Count<T>(string tablename);
		int Count<T>(Expression<Func<T, bool>> predicate);
		int Count<T>(Expression<Func<T, bool>> predicate, string tablename);
		bool Any<T>(IFileDatabase ldb = null);
		bool Any<T>(string tablename, IFileDatabase ldb = null);
		bool Any<T>(Expression<Func<T, bool>> predicate);
		bool Any<T>(Expression<Func<T, bool>> predicate, string tablename);
		T GetSingle<T>();
		T GetSingle<T>(string tablename);
		bool Exists<T>(Expression<Func<T, bool>> predicate, IFileDatabase ldb = null);
		bool Exists<T>(Expression<Func<T, bool>> predicate, string tablename, IFileDatabase ldb = null);
		T GetSingle<T, K>(Expression<Func<T, bool>> predicate);
		T GetSingle<T, K>(Expression<Func<T, bool>> predicate, string tablename);
		List<T> GetAll<T>();
		List<T> GetAll<T>(string tablename);
		IEnumerable<T> All<T>(IFileDatabase ldb = null);
		IEnumerable<T> All<T>(string tablename, IFileDatabase ldb = null);
		List<T> Get<T>(Expression<Func<T, bool>> predicate);
		List<T> Get<T>(Expression<Func<T, bool>> predicate, string tablename);
		List<K> Get<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector);
		List<K> Get<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector, string tablename);
		void Insert<T, K>(T item, Expression<Func<T, K>> index, IFileDatabase ldb = null);
		void Insert<T, K>(T item, string tablename, Expression<Func<T, K>> index, IFileDatabase ldb = null);
		void Insert<T, K>(List<T> items, Expression<Func<T, K>> index);
		void Insert<T, K>(List<T> items, string tablename, Expression<Func<T, K>> index);
		bool Update<T>(T item, IFileDatabase ldb = null);
		bool Update<T>(T item, string tablename, IFileDatabase ldb = null);
		void Updates<T>(List<T> items);
		void Updates<T>(List<T> items, string tablename);
		int Remove<T>(Expression<Func<T, bool>> predicate);
		int Remove<T>(Expression<Func<T, bool>> predicate, IFileDatabase ldb);
		int Remove<T>(Expression<Func<T, bool>> predicate, string tablename);
		int Remove<T>(Expression<Func<T, bool>> predicate, string tablename, IFileDatabase ldb );
		T GetOne<T>(Expression<Func<T, bool>> predicate, IFileDatabase ldb = null);
		T GetOne<T>(Expression<Func<T, bool>> predicate, string tablename, IFileDatabase ldb = null);
		K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector);
		K GetOne<T, K>(Expression<Func<T, bool>> predicate, Func<T, K> selector, string tablename);

		void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort)
			where TKey : IEquatable<TKey>;

		void Trim<T, TKey>(int keep, Func<T, TKey> getKey, Func<IEnumerable<T>, IEnumerable<T>> sort, string tablename)
			where TKey : IEquatable<TKey>;
	}
}