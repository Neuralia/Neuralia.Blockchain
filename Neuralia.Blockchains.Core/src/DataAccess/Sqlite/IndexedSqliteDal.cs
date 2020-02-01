using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
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

		public override void Clear() {
			foreach(string file in this.GetAllFileGroups()) {
				if(File.Exists(file)) {
					File.Delete(file);
				}
			}

			var type = this.GetType();

			if(DbCreatedCache.ContainsKey(type)) {
				DbCreatedCache[type].Clear();
			}
		}

		
		protected long GetKeyGroup(long key) {
			return this.FindIndex(key).index;
		}

		protected long GetKeyGroup(AccountId key) {
			return this.FindIndex(key.SequenceId).index;
		}

		/// <summary>
		///     Run a set of operations, each on their own file
		/// </summary>
		/// <param name="operations"></param>
		public void PerformProcessingSet(Dictionary<long, List<Action<DBCONTEXT>>> operations) {

			// group them by keyGroups

			var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key));

			foreach(var group in groups) {

				foreach(var operation in group.Select(g => g.Value)) {
					this.PerformOperations(operation, group.Key);
				}
			}
		}

		/// <summary>
		///     Run a set of operations on their own file, but return an uncommited transaction
		/// </summary>
		/// <param name="operations"></param>
		/// <returns></returns>
		public List<(DBCONTEXT db, IDbContextTransaction transaction)> PerformProcessingSetHoldTransactions(Dictionary<long, List<Action<DBCONTEXT>>> operations) {

			// group them by keyGroups
			var transactions = new List<(DBCONTEXT db, IDbContextTransaction transaction)>();

			try {

				var groups = operations.GroupBy(e => this.GetKeyGroup(e.Key), d => d.Value);

				foreach(var group in groups) {

					(DBCONTEXT db, IDbContextTransaction transaction) transaction = this.BeginHoldingTransaction(group.Key);
					transactions.Add(transaction);

					foreach(var operation in group.SelectMany(e => e)) {

						operation(transaction.db);

					}
				}

				return transactions;
			} catch {

				foreach((DBCONTEXT db, IDbContextTransaction transaction) entry in transactions) {
					try {
						entry.transaction?.Rollback();
					} catch {

					}

					try {
						entry.db?.Dispose();
					} catch {

					}
				}

				throw;
			}
		}

		public List<T> QueryAll<T>(Func<DBCONTEXT, List<T>> operation) {

			var results = new List<T>();

			foreach(string file in this.GetAllFileGroups()) {

				results.AddRange(this.PerformOperation(operation, file));
			}

			return results;
		}
		
		public bool AnyAll(Func<DBCONTEXT, bool> operation, List<long> ids) {

			var groups = ids.GroupBy(this.GetKeyGroup);

			foreach(long index in groups.Select(g => g.Key)) {

				if(this.PerformOperation(operation, index)) {
					return true;
				}
			}

			return false;
		}

		public List<T> QueryAll<T>(Func<DBCONTEXT, List<T>> operation, List<long> ids) {

			var groups = ids.GroupBy(this.GetKeyGroup);

			var results = new List<T>();

			foreach(long index in groups.Select(g => g.Key)) {

				results.AddRange(this.PerformOperation(operation, index));
			}

			return results;
		}

		protected void InitContext(DBCONTEXT db, string filename) {

			db.SetGroupFile(filename);

			base.InitContext(db);
		}

		protected void InitContext(DBCONTEXT db, long index) {

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

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, string filename) {
			return base.PerformOperation(process, filename);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, string filename) {
			return base.PerformOperation(process, filename);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, int index) {
			return base.PerformOperation(process, index);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, int index) {
			return base.PerformOperation(process, index);
		}

		protected override void PerformInnerContextOperation(Action<DBCONTEXT> action, params object[] contents) {

			try {
				Action<DBCONTEXT> initializer = null;

				if(contents[0] is string filename) {
					initializer = dbx => this.InitContext(dbx, filename);
				} else if(contents[0] is long index) {
					initializer = dbx => this.InitContext(dbx, index);
				}

				using(DBCONTEXT db = this.CreateContext(initializer)) {
					action(db);
				}
			} catch(Exception ex) {
				Log.Error(ex, "exception occured during an indexed Entity Framework action");

				throw;
			}
			
		}

		public (DBCONTEXT db, IDbContextTransaction transaction) BeginHoldingTransaction(long index) {

			DBCONTEXT db = this.CreateContext(dbx => this.InitContext(dbx, index));

			IDbContextTransaction transaction = db.Database.BeginTransaction();

			return (db, transaction);
		}
	}
}