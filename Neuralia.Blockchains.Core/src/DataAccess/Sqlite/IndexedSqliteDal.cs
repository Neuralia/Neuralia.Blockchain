using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface IIndexedSqliteDal<DBCONTEXT> : ISqliteDal<DBCONTEXT>
		where DBCONTEXT : IIndexedSqliteDbContext {
	}

	public abstract class IndexedSqliteDal<DBCONTEXT> : SqliteDal<DBCONTEXT>, IIndexedSqliteDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IIndexedSqliteDbContext {
		protected readonly int groupSize;

		private string groupRoot;

		protected IndexedSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, contextInstantiator, serializationType) {
			this.groupSize = groupSize;
		}

		protected string GroupRoot {
			get {
				if(string.IsNullOrWhiteSpace(this.groupRoot)) {
					// create a raw context with no initialization. we only want an instance property value
					using(DBCONTEXT db = this.CreateRawContext()) {
						this.groupRoot = db.GroupRoot;
					}
				}

				return this.groupRoot;
			}
		}

		private (long index, long startingId, long endingBlockId) FindIndex(long Id) {

			if(Id == 0) {
				throw new ApplicationException("Block Id cannot be 0.");
			}

			return IndexCalculator.ComputeIndex(Id, this.groupSize);
		}

		/// <summary>
		///     Get all the files that belong to this group root
		/// </summary>
		/// <returns></returns>
		protected List<string> GetAllFileGroups() {
			if(!Directory.Exists(this.folderPath)) {
				return new List<string>();
			}

			return Directory.GetFiles(this.folderPath).Where(f => Path.GetFileName(f).StartsWith(this.GroupRoot)).ToList();
		}

		public override Task Clear(LockContext lockContext = null) {
			foreach(string file in this.GetAllFileGroups()) {
				if(File.Exists(file)) {
					File.Delete(file);
				}
			}

			Type type = this.GetType();

			if(DbCreatedCache.ContainsKey(type)) {
				DbCreatedCache[type].Clear();
			}

			return Task.CompletedTask;
		}

		protected IndexedSqliteDbContext.IndexSet GetKeyGroup(long key) {
			return new IndexedSqliteDbContext.IndexSet(this.FindIndex(key).index, "");
		}
		
		protected IndexedSqliteDbContext.IndexSet GetKeyGroup(AccountId key) {
			string type = "";

			if(!key.IsValid || key.SequenceId == 0) {
				throw new ApplicationException("Invalid account Id");
			}
			
			if(key.AccountType != Enums.AccountTypes.Unknown) {
				type = key.AccountType.ToString().ToLower();
			}
			return new IndexedSqliteDbContext.IndexSet(this.FindIndex(key.SequenceId).index, type);
		}

		/// <summary>
		///     Run a set of operations, each on their own file
		/// </summary>
		/// <param name="operations"></param>
		public void PerformProcessingSet(Dictionary<AccountId, List<Action<DBCONTEXT, LockContext>>> operations, LockContext lockContext = null) {

			// group them by keyGroups

			var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key));
			foreach(var group in groups) {

				foreach(List<Action<DBCONTEXT, LockContext>> operation in group.Select(g => g.Value)) {
					this.PerformOperations(operation, lockContext, group.Key);
				}
			}
		}

		public Task<List<(DBCONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSetHoldTransactions(Dictionary<long, List<Func<DBCONTEXT, LockContext, Task>>> operations, LockContext lockContext = null) {


			var wrappedOperations = operations.ToDictionary(e => new AccountId(e.Key, Enums.AccountTypes.Unknown), e => e.Value.Select(o => {

				Task Func(DBCONTEXT db, LockContext lc) {
					return o(db, lc);
				}

				return (Func<DBCONTEXT, LockContext, Task>) Func;
			}).ToList());

			return this.PerformProcessingSetHoldTransactions(wrappedOperations, lockContext);
		}

		/// <summary>
		///     Run a set of operations on their own file, but return an uncommited transaction
		/// </summary>
		/// <param name="operations"></param>
		/// <returns></returns>
		public async Task<List<(DBCONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSetHoldTransactions(Dictionary<AccountId, List<Func<DBCONTEXT, LockContext, Task>>> operations, LockContext lockContext = null) {

			// group them by keyGroups
			List<(DBCONTEXT db, IDbContextTransaction transaction)> transactions = new List<(DBCONTEXT db, IDbContextTransaction transaction)>();

			try {

				var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key), d => d.Value);

				foreach(var group in groups) {

					(DBCONTEXT db, IDbContextTransaction transaction) transaction = await this.BeginHoldingTransaction(group.Key).ConfigureAwait(false);
					transactions.Add(transaction);

					foreach(Func<DBCONTEXT, LockContext, Task> operation in group.SelectMany(e => e)) {

						await operation(transaction.db, lockContext).ConfigureAwait(false);
					}
				}

				return transactions;
			} catch {

				foreach((DBCONTEXT db, IDbContextTransaction transaction) in transactions) {
					try {
						if(transaction != null) {
							await (transaction?.RollbackAsync()).ConfigureAwait(false);
						}
					} catch {

					}

					try {
						if(db != null) {
							await db.DisposeAsync().ConfigureAwait(false);
						}
					} catch {

					}
				}

				throw;
			}
		}

		public List<T> QueryAll<T>(Func<DBCONTEXT, LockContext, List<T>> operation, LockContext lockContex) {

			List<T> results = new List<T>();

			foreach(string file in this.GetAllFileGroups()) {

				results.AddRange(this.PerformOperation(operation, file, lockContex));
			}

			return results;
		}

		public async Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> operation, LockContext lockContext = null) {

			List<T> results = new List<T>();

			foreach(string file in this.GetAllFileGroups()) {

				results.AddRange(await this.PerformOperation(operation, file, lockContext).ConfigureAwait(false));
			}

			return results;
		}

		public bool AnyAll(Func<DBCONTEXT, LockContext, bool> operation, List<IndexedSqliteDbContext.IndexSet> ids, LockContext lockContext = null) {

			var groups = ids.GroupBy(e => e);
			foreach(var index in groups) {

				if(this.PerformOperation(operation, lockContext, index.Key)) {
					return true;
				}
			}

			return false;
		}

		public Task<bool> AnyAllAsync(Func<DBCONTEXT, LockContext, Task<bool>> operation, List<AccountId> ids, LockContext lockContext = null) {
			return this.AnyAllAsync(operation, ids.Select(e => this.GetKeyGroup(e)).ToList(), lockContext);
		}

		public async Task<bool> AnyAllAsync(Func<DBCONTEXT, LockContext, Task<bool>> operation, List<IndexedSqliteDbContext.IndexSet> ids, LockContext lockContext = null) {

			var groups = ids.GroupBy(e => e);
			foreach(var index in groups) {

				if(await this.PerformOperationAsync(operation, lockContext, index.Key).ConfigureAwait(false)) {
					return true;
				}
			}

			return false;
		}

		public List<T> QueryAll<T>(Func<DBCONTEXT, LockContext, List<T>> operation, List<IndexedSqliteDbContext.IndexSet> ids, LockContext lockContext = null) {

			var groups = ids.GroupBy(e => e);

			List<T> results = new List<T>();
			foreach(var index in groups) {

				results.AddRange(this.PerformOperation(operation, lockContext, index.Key));
			}

			return results;
		}

		public Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> operation, List<long> ids, LockContext lockContext = null) {
			return this.QueryAllAsync(operation, ids.Select(e => new IndexedSqliteDbContext.IndexSet(e)).ToList(), lockContext);
		}
		
		public Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> operation, List<AccountId> ids, LockContext lockContext = null) {
			return this.QueryAllAsync(operation, ids.Select(e => this.GetKeyGroup(e)).ToList(), lockContext);
		}

		public async Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> operation, List<IndexedSqliteDbContext.IndexSet> ids, LockContext lockContext = null) {

			var groups = ids.GroupBy(e => e);

			List<T> results = new List<T>();
			foreach(var index in groups) {

				results.AddRange(await this.PerformOperationAsync(operation, lockContext, index.Key).ConfigureAwait(false));
			}

			return results;
		}

		public async Task RunOnAllAsync<T>(Func<DBCONTEXT, LockContext, Task> operation, LockContext lockContext = null) {

			foreach(string file in this.GetAllFileGroups()) {

				await this.PerformOperation(operation, file, lockContext).ConfigureAwait(false);
			}
		}

		protected void InitContext(DBCONTEXT db, string filename, LockContext lockContext = null) {

			db.SetGroupFile(filename);

			base.InitContext(db, lockContext);
		}

		protected void InitContext(DBCONTEXT db, IndexedSqliteDbContext.IndexSet index, LockContext lockContext = null) {

			db.SetGroupIndex(index, this.groupSize);

			base.InitContext(db, lockContext);
		}

		protected virtual void PerformOperation(Action<DBCONTEXT, LockContext> process, string filename, LockContext lockContext = null) {
			base.PerformOperation(process, lockContext, filename);
		}

		protected virtual void PerformOperation(Action<DBCONTEXT, LockContext> process, int index, LockContext lockContext = null) {
			base.PerformOperation(process, lockContext, index);
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT, LockContext>> processes, int index, LockContext lockContext = null) {
			base.PerformOperations(processes, lockContext, index);
		}

		protected virtual Task PerformOperationsAsync(IEnumerable<Func<DBCONTEXT, LockContext, Task>> processes, int index, LockContext lockContext = null) {
			return base.PerformOperationsAsync(processes, lockContext, index);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, LockContext, T> process, string filename, LockContext lockContext = null) {
			return base.PerformOperation(process, lockContext, filename);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, LockContext, List<T>> process, string filename, LockContext lockContext = null) {
			return base.PerformOperation(process, lockContext, filename);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, LockContext, T> process, int index, LockContext lockContext = null) {
			return base.PerformOperation(process, lockContext, index);
		}

		protected virtual Task<T> PerformOperationsAsync<T>(Func<DBCONTEXT, LockContext, Task<T>> process, int index, LockContext lockContext = null) {
			return base.PerformOperationAsync(process, lockContext, index);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, LockContext, List<T>> process, int index, LockContext lockContext = null) {
			return base.PerformOperation(process, lockContext, index);
		}

		protected virtual Task<List<T>> PerformOperationsAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> process, int index, LockContext lockContext = null) {
			return base.PerformOperationAsync(process, lockContext, index);
		}

		protected override void PerformInnerContextOperation(Action<DBCONTEXT, LockContext> action, LockContext lockContext = null, params object[] contents) {
			using(var handle = this.locker.Lock(lockContext)) {
				try {
					Action<DBCONTEXT, LockContext> initializer = null;

					if(contents[0] is string filename) {
						initializer = (dbx, lc) => this.InitContext(dbx, filename, handle);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = (dbx, lc) => this.InitContext(dbx, indexEntry, handle);
					}

					using(DBCONTEXT db = this.CreateContext(handle, initializer)) {
						action(db, handle);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}

		}

		protected override async Task PerformInnerContextOperationAsync(Func<DBCONTEXT, LockContext, Task> action, LockContext lockContext = null, params object[] contents) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				try {
					Action<DBCONTEXT, LockContext> initializer = null;

					if(contents[0] is string filename) {
						initializer = (dbx, lc) => this.InitContext(dbx, filename, handle);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = (dbx, lc) => this.InitContext(dbx, indexEntry, handle);
					}

					await using(DBCONTEXT db = this.CreateContext(handle, initializer)) {
						await action(db, handle).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}
		}

		protected override async Task<T> PerformInnerContextOperationAsync<T>(Func<DBCONTEXT, LockContext, Task<T>> action, LockContext lockContext = null, params object[] contents) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				try {
					Action<DBCONTEXT, LockContext> initializer = null;

					if(contents[0] is string filename) {
						initializer = (dbx, lc) => this.InitContext(dbx, filename, handle);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = (dbx, lc) => this.InitContext(dbx, indexEntry, handle);
					}

					await using(DBCONTEXT db = this.CreateContext(handle, initializer)) {
						return await action(db, handle).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}
		}

		public async Task<(DBCONTEXT db, IDbContextTransaction transaction)> BeginHoldingTransaction(IndexedSqliteDbContext.IndexSet index) {

			LockContext lockContext = null;
			DBCONTEXT db = this.CreateContext(lockContext, (dbx, lc) => this.InitContext(dbx, index, lc));

			IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);

			return (db, transaction);
		}
		
		protected static readonly Dictionary<(Type, IndexedSqliteDbContext.IndexSet), HashSet<string>> SplitDbCreatedCache = new Dictionary<(Type, IndexedSqliteDbContext.IndexSet), HashSet<string>>();

		protected override void EnsureDatabaseCreated(DBCONTEXT ctx, LockContext lockContext = null) {
			
			string path = this.GetDbPath(ctx);

			Type type = this.GetType();

			var key = (type, ctx.Index);
			if(!SplitDbCreatedCache.ContainsKey(key)) {
				SplitDbCreatedCache.Add(key, new HashSet<string>());
			}

			if(!SplitDbCreatedCache[key].Contains(path)) {
				if(!File.Exists(path)) {

					NLog.Default.Verbose("Ensuring that the Sqlite database '{0}' exists and tables are created", path);

					// let's make sure the database exists and is created
					ctx.EnsureCreated();

					this.EnsureVersionCreated(ctx, lockContext);

					NLog.Default.Verbose("Sqlite database '{0}' structure creation completed.", path);
				}

				SplitDbCreatedCache[key].Add(path);
			}
		}
	}
}