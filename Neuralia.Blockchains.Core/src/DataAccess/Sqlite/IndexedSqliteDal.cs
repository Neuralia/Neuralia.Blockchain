using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface IIndexedSqliteDal<DBCONTEXT> : ISqliteDal<DBCONTEXT>
		where DBCONTEXT : IIndexedSqliteDbContext {
	}

	public abstract class IndexedSqliteDal<DBCONTEXT> : SqliteDal<DBCONTEXT>, IIndexedSqliteDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IIndexedSqliteDbContext {
		protected readonly long groupSize;

		private string groupRoot;

		protected IndexedSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, contextInstantiator, serializationType) {
			this.groupSize = groupSize;
		}

		protected string GroupRoot {
			get {
				if(string.IsNullOrWhiteSpace(this.groupRoot)) {
					using(DBCONTEXT db = this.CreateContext()) {
						this.groupRoot = db.GroupRoot;
					}
				}

				return this.groupRoot;
			}
		}

		private (int index, long startingId) FindIndex(long Id) {

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
			return Directory.GetFiles(this.folderPath).Where(f => Path.GetFileName(f).StartsWith(this.GroupRoot)).ToList();
		}

		
		protected int GetKeyGroup(long key) {
			return this.FindIndex(key).index;
		}

		protected int GetKeyGroup(AccountId key) {
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

		public List<T> QueryAll<T>(Func<DBCONTEXT, List<T>> operation, List<long> ids) {

			var groups = ids.GroupBy(this.GetKeyGroup);

			var results = new List<T>();

			foreach(int index in groups.Select(g => g.Key)) {

				results.AddRange(this.PerformOperation(operation, index));
			}

			return results;
		}

		protected void InitContext(DBCONTEXT db, string filename) {

			db.SetGroupFile(filename);

			base.InitContext(db);
		}

		protected void InitContext(DBCONTEXT db, int index) {

			db.SetGroupIndex(index);

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

			Action<DBCONTEXT> initializer = null;

			if(contents[0] is string filename) {
				initializer = dbx => this.InitContext(dbx, filename);
			} else if(contents[0] is int index) {
				initializer = dbx => this.InitContext(dbx, index);
			}

			using(DBCONTEXT db = this.CreateContext(initializer)) {
				action(db);
			}
		}

		public (DBCONTEXT db, IDbContextTransaction transaction) BeginHoldingTransaction(int index) {

			DBCONTEXT db = this.CreateContext(dbx => this.InitContext(dbx, index));

			IDbContextTransaction transaction = db.Database.BeginTransaction();

			return (db, transaction);
		}
	}
}