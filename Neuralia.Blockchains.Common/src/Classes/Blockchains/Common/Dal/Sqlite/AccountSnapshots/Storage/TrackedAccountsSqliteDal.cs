using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface ITrackedAccountsSqliteDal : ITrackedAccountsDal {
	}

	public interface ITrackedAccountsSqliteDal<TRACKED_ACCOUNT_CONTEXT> : IIndexedSqliteDal<ITrackedAccountsSqliteContext>, ITrackedAccountsDal<TRACKED_ACCOUNT_CONTEXT>, ITrackedAccountsSqliteDal
		where TRACKED_ACCOUNT_CONTEXT : class, ITrackedAccountsSqliteContext {
	}

	public abstract class TrackedAccountsSqliteDal<TRACKED_ACCOUNT_CONTEXT> : IndexedSqliteDal<TRACKED_ACCOUNT_CONTEXT>, ITrackedAccountsSqliteDal
		where TRACKED_ACCOUNT_CONTEXT : DbContext, ITrackedAccountsSqliteContext {

		protected TrackedAccountsSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, chainDalCreationFactory.CreateTrackedAccountsContext<TRACKED_ACCOUNT_CONTEXT>, serializationType) {
		}

		public void AddTrackedAccounts(List<AccountId> accounts) {

			var longAccounts = accounts.Select(a => a.ToLongRepresentation()).ToList();

			this.PerformOperation(db => {

				var existing = db.TrackedAccounts.Where(a => longAccounts.Contains(a.AccountId)).Select(a => a.AccountId).ToList().Select(a => a.ToAccountId()).ToList();

				foreach(AccountId account in accounts) {
					if(!existing.Contains(account)) {
						TrackedAccountSqliteEntry entry = new TrackedAccountSqliteEntry {AccountId = account.ToLongRepresentation()};
						db.TrackedAccounts.Add(entry);
					}
				}

				db.SaveChanges();

			});
		}

		public void RemoveTrackedAccounts(List<AccountId> accounts) {
			throw new NotImplementedException();
		}

		public List<AccountId> GetTrackedAccounts(List<AccountId> accounts) {
			var longAccounts = accounts.Select(a => a.ToLongRepresentation()).ToList();

			return this.PerformOperation(db => {

				return db.TrackedAccounts.Where(a => longAccounts.Contains(a.AccountId)).Select(a => a.AccountId.ToAccountId()).ToList();
			});
		}

		public bool AnyAccountsTracked() {

			return this.PerformOperation(db => db.TrackedAccounts.Any());
		}

		public bool AnyAccountsTracked(List<AccountId> accounts) {
			var longAccounts = accounts.Select(a => a.ToLongRepresentation()).ToList();

			return this.PerformOperation(db => {

				return db.TrackedAccounts.Any(a => longAccounts.Contains(a.AccountId));
			});
		}

		public bool IsAccountTracked(AccountId account) {

			return this.PerformOperation(db => {

				return db.TrackedAccounts.Any(a => a.AccountId == account.ToLongRepresentation());
			});
		}

		public void PerformOperation(Action<TRACKED_ACCOUNT_CONTEXT> process) {

			base.PerformOperation(process);
		}

		public T PerformOperation<T>(Func<TRACKED_ACCOUNT_CONTEXT, T> process) {

			return base.PerformOperation(process);
		}
	}
}