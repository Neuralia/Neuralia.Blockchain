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
using Serilog;

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

		public override Task Clear() {
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
		public void PerformProcessingSet(Dictionary<AccountId, List<Action<DBCONTEXT>>> operations) {

			// group them by keyGroups

			var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key));

			foreach(var group in groups) {

				foreach(List<Action<DBCONTEXT>> operation in group.Select(g => g.Value)) {
					this.PerformOperations(operation, group.Key);
				}
			}
		}

		public Task<List<(DBCONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSetHoldTransactions(Dictionary<long, List<Func<DBCONTEXT, LockContext, Task>>> operations) {

			LockContext lockContext = null;

			Dictionary<AccountId, List<Func<DBCONTEXT, Task>>> wrappedOperations = operations.ToDictionary(e => new AccountId(e.Key, Enums.AccountTypes.Unknown), e => e.Value.Select(o => {

				Task Func(DBCONTEXT db) {
					return o(db, lockContext);
				}

				return (Func<DBCONTEXT, Task>) Func;
			}).ToList());

			return this.PerformProcessingSetHoldTransactions(wrappedOperations);
		}
		
		public Task<List<(DBCONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSetHoldTransactions(Dictionary<AccountId, List<Func<DBCONTEXT, LockContext, Task>>> operations) {

			LockContext lockContext = null;

			Dictionary<AccountId, List<Func<DBCONTEXT, Task>>> wrappedOperations = operations.ToDictionary(e => e.Key, e => e.Value.Select(o => {

				Task Func(DBCONTEXT db) {
					return o(db, lockContext);
				}

				return (Func<DBCONTEXT, Task>) Func;
			}).ToList());

			return this.PerformProcessingSetHoldTransactions(wrappedOperations);
		}

		/// <summary>
		///     Run a set of operations on their own file, but return an uncommited transaction
		/// </summary>
		/// <param name="operations"></param>
		/// <returns></returns>
		public async Task<List<(DBCONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSetHoldTransactions(Dictionary<AccountId, List<Func<DBCONTEXT, Task>>> operations) {

			// group them by keyGroups
			List<(DBCONTEXT db, IDbContextTransaction transaction)> transactions = new List<(DBCONTEXT db, IDbContextTransaction transaction)>();

			try {

				var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key), d => d.Value);

				foreach(var group in groups) {

					(DBCONTEXT db, IDbContextTransaction transaction) transaction = await this.BeginHoldingTransaction(group.Key).ConfigureAwait(false);
					transactions.Add(transaction);

					foreach(Func<DBCONTEXT, Task> operation in group.SelectMany(e => e)) {

						await operation(transaction.db).ConfigureAwait(false);
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

		public List<T> QueryAll<T>(Func<DBCONTEXT, List<T>> operation) {

			List<T> results = new List<T>();

			foreach(string file in this.GetAllFileGroups()) {

				results.AddRange(this.PerformOperation(operation, file));
			}

			return results;
		}

		public async Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, Task<List<T>>> operation) {

			List<T> results = new List<T>();

			foreach(string file in this.GetAllFileGroups()) {

				results.AddRange(await this.PerformOperation(operation, file).ConfigureAwait(false));
			}

			return results;
		}

		public bool AnyAll(Func<DBCONTEXT, bool> operation, List<IndexedSqliteDbContext.IndexSet> ids) {

			var groups = ids.GroupBy(e => e);

			foreach(var index in groups) {

				if(this.PerformOperation(operation, index.Key)) {
					return true;
				}
			}

			return false;
		}

		public Task<bool> AnyAllAsync(Func<DBCONTEXT, Task<bool>> operation, List<AccountId> ids) {
			return this.AnyAllAsync(operation, ids.Select(e => this.GetKeyGroup(e)).ToList());
		}

		public async Task<bool> AnyAllAsync(Func<DBCONTEXT, Task<bool>> operation, List<IndexedSqliteDbContext.IndexSet> ids) {

			var groups = ids.GroupBy(e => e);

			foreach(var index in groups) {

				if(await this.PerformOperationAsync(operation, index.Key).ConfigureAwait(false)) {
					return true;
				}
			}

			return false;
		}

		public List<T> QueryAll<T>(Func<DBCONTEXT, List<T>> operation, List<IndexedSqliteDbContext.IndexSet> ids) {

			var groups = ids.GroupBy(e => e);

			List<T> results = new List<T>();

			foreach(var index in groups) {

				results.AddRange(this.PerformOperation(operation, index.Key));
			}

			return results;
		}

		public Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, Task<List<T>>> operation, List<long> ids) {
			return this.QueryAllAsync(operation, ids.Select(e => new IndexedSqliteDbContext.IndexSet(e)).ToList());
		}
		
		public Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, Task<List<T>>> operation, List<AccountId> ids) {
			return this.QueryAllAsync(operation, ids.Select(e => this.GetKeyGroup(e)).ToList());
		}

		public async Task<List<T>> QueryAllAsync<T>(Func<DBCONTEXT, Task<List<T>>> operation, List<IndexedSqliteDbContext.IndexSet> ids) {

			var groups = ids.GroupBy(e => e);

			List<T> results = new List<T>();

			foreach(var index in groups) {

				results.AddRange(await this.PerformOperationAsync(operation, index.Key).ConfigureAwait(false));
			}

			return results;
		}

		public async Task RunOnAllAsync<T>(Func<DBCONTEXT, Task> operation) {

			foreach(string file in this.GetAllFileGroups()) {

				await this.PerformOperation(operation, file).ConfigureAwait(false);
			}
		}

		protected void InitContext(DBCONTEXT db, string filename) {

			db.SetGroupFile(filename);

			base.InitContext(db);
		}

		protected void InitContext(DBCONTEXT db, IndexedSqliteDbContext.IndexSet index) {

			db.SetGroupIndex(index, this.groupSize);

			base.InitContext(db);
		}

		protected virtual void PerformOperation(Action<DBCONTEXT> process, string filename) {
			base.PerformOperation(process, filename);
		}

		protected virtual void PerformOperation(Action<DBCONTEXT> process, int index) {
			base.PerformOperation(process, index);
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT>> processes, int index) {
			base.PerformOperations(processes, index);
		}

		protected virtual Task PerformOperationsAsync(IEnumerable<Func<DBCONTEXT, Task>> processes, int index) {
			return base.PerformOperationsAsync(processes, index);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, string filename) {
			return base.PerformOperation(process, filename);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, string filename) {
			return base.PerformOperation(process, filename);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, int index) {
			return base.PerformOperation(process, index);
		}

		protected virtual Task<T> PerformOperationsAsync<T>(Func<DBCONTEXT, Task<T>> process, int index) {
			return base.PerformOperationAsync(process, index);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, int index) {
			return base.PerformOperation(process, index);
		}

		protected virtual Task<List<T>> PerformOperationsAsync<T>(Func<DBCONTEXT, Task<List<T>>> process, int index) {
			return base.PerformOperationAsync(process, index);
		}

		protected override void PerformInnerContextOperation(Action<DBCONTEXT> action, params object[] contents) {
			using(this.locker.Lock()) {
				try {
					Action<DBCONTEXT> initializer = null;

					if(contents[0] is string filename) {
						initializer = dbx => this.InitContext(dbx, filename);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = dbx => this.InitContext(dbx, indexEntry);
					}

					using(DBCONTEXT db = this.CreateContext(initializer)) {
						action(db);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}

		}

		protected override async Task PerformInnerContextOperationAsync(Func<DBCONTEXT, Task> action, params object[] contents) {
			using(await this.locker.LockAsync().ConfigureAwait(false)) {
				try {
					Action<DBCONTEXT> initializer = null;

					if(contents[0] is string filename) {
						initializer = dbx => this.InitContext(dbx, filename);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = dbx => this.InitContext(dbx, indexEntry);
					}

					await using(DBCONTEXT db = this.CreateContext(initializer)) {
						await action(db).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}
		}

		protected override async Task<T> PerformInnerContextOperationAsync<T>(Func<DBCONTEXT, Task<T>> action, params object[] contents) {
			using(await this.locker.LockAsync().ConfigureAwait(false)) {
				try {
					Action<DBCONTEXT> initializer = null;

					if(contents[0] is string filename) {
						initializer = dbx => this.InitContext(dbx, filename);
					} else if(contents[0] is IndexedSqliteDbContext.IndexSet indexEntry) {
						initializer = dbx => this.InitContext(dbx, indexEntry);
					}

					await using(DBCONTEXT db = this.CreateContext(initializer)) {
						return await action(db).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an indexed Entity Framework action");

					throw;
				}
			}
		}

		public async Task<(DBCONTEXT db, IDbContextTransaction transaction)> BeginHoldingTransaction(IndexedSqliteDbContext.IndexSet index) {

			DBCONTEXT db = this.CreateContext(dbx => this.InitContext(dbx, index));

			IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);

			return (db, transaction);
		}
		
		protected static readonly Dictionary<(Type, IndexedSqliteDbContext.IndexSet), HashSet<string>> SplitDbCreatedCache = new Dictionary<(Type, IndexedSqliteDbContext.IndexSet), HashSet<string>>();

		protected override void EnsureDatabaseCreated(DBCONTEXT ctx) {
			
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

					this.EnsureVersionCreated(ctx);

					NLog.Default.Verbose("Sqlite database '{0}' structure creation completed.", path);
				}

				SplitDbCreatedCache[key].Add(path);
			}
		}
	}
}