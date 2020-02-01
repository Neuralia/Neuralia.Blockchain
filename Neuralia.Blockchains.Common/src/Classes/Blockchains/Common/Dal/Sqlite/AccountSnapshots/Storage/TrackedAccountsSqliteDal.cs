using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface ITrackedAccountsSqliteDal : ITrackedAccountsDal {
	}

	public interface ITrackedAccountsSqliteDal<TRACKED_ACCOUNT_CONTEXT> : IIndexedSqliteDal<ITrackedAccountsSqliteContext>, ITrackedAccountsDal<TRACKED_ACCOUNT_CONTEXT>, ITrackedAccountsSqliteDal
		where TRACKED_ACCOUNT_CONTEXT : class, ITrackedAccountsSqliteContext {
	}

	public abstract class TrackedAccountsSqliteDal<TRACKED_ACCOUNT_CONTEXT> : IndexedSqliteDal<TRACKED_ACCOUNT_CONTEXT>, ITrackedAccountsSqliteDal
		where TRACKED_ACCOUNT_CONTEXT : DbContext, ITrackedAccountsSqliteContext {

		protected TrackedAccountsSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateTrackedAccountsContext<TRACKED_ACCOUNT_CONTEXT>, serializationType) {
		}

		public void AddTrackedAccounts(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return;
			}

			var sequenceAccounts = accountIds.Select(a => a.SequenceId).ToList();
			
			var actions = sequenceAccounts.ToDictionary(a => a, e => new Action<TRACKED_ACCOUNT_CONTEXT>[] {
				(db) => {

					
					var longAccounts = accountIds.Where(a => a.SequenceId >= db.IndexRange.start && a.SequenceId <= db.IndexRange.end).Select(a => a.ToLongRepresentation()).ToList();

					var existing = db.TrackedAccounts.Where(a => longAccounts.Contains(a.AccountId)).Select(a => a.AccountId).ToList();

					foreach(var account in longAccounts.Where(a => !existing.Contains(a))) {
						TrackedAccountSqliteEntry entry = new TrackedAccountSqliteEntry {AccountId = account};
						db.TrackedAccounts.Add(entry);
					}

					db.SaveChanges();

				}
			}.ToList());

			this.PerformProcessingSet(actions);
		}

		public void RemoveTrackedAccounts(List<AccountId> accounts) {
			throw new NotImplementedException();
		}

		public List<AccountId> GetTrackedAccounts(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return new List<AccountId>();
			}
			
			var longAccountIds = accountIds.Select(a => a.ToLongRepresentation()).ToList();

			return this.QueryAll(db => {

				return db.TrackedAccounts.Where(a => longAccountIds.Contains(a.AccountId)).Select(a => a.AccountId.ToAccountId()).ToList();
			}, accountIds.Select(a => a.SequenceId).ToList());
		}

		public bool AnyAccountsTracked() {

			return this.PerformOperation(db => db.TrackedAccounts.Any());
		}

		public bool AnyAccountsTracked(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return false;
			}

			var longAccountIds = accountIds.Select(a => a.ToLongRepresentation()).ToList();

			return this.AnyAll(db => {

				return db.TrackedAccounts.Any(a => longAccountIds.Contains(a.AccountId));
			}, accountIds.Select(a => a.SequenceId).ToList());

		}

		public bool IsAccountTracked(AccountId accountId) {
			if(accountId == null) {
				return false;
			}

			return this.PerformOperation(db => {

				return db.TrackedAccounts.Any(a => a.AccountId == accountId.ToLongRepresentation());
			}, this.GetKeyGroup(accountId.SequenceId));
		}

		// public void PerformOperation(Action<TRACKED_ACCOUNT_CONTEXT> process, AccountId accountId) {
		// 	this.PerformOperation(process, this.GetKeyGroup(accountId));
		// }
		//
		// public T PerformOperation<T>(Func<TRACKED_ACCOUNT_CONTEXT, T> process) {
		//
		// 	return base.PerformOperation(process, this.GetKeyGroup(accountSnapshotEntry.AccountId));
		// }
	}
}