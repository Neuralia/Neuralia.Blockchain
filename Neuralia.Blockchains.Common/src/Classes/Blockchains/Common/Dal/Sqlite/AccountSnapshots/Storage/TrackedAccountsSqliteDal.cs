using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
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


			Dictionary<AccountId, List<Action<TRACKED_ACCOUNT_CONTEXT>>> actions = accountIds.ToDictionary(a => a, e => new Action<TRACKED_ACCOUNT_CONTEXT>[] {
				db => {
					List<AccountId> longAccounts = accountIds.Where(a => (a.SequenceId >= db.IndexRange.start) && (a.SequenceId <= db.IndexRange.end)).ToList();

					List<AccountId> existing = db.TrackedAccounts.Where(a => longAccounts.Contains(a.AccountId.ToAccountId())).Select(a => a.AccountId.ToAccountId()).ToList();

					foreach(AccountId account in longAccounts.Where(a => !existing.Contains(a))) {
						TrackedAccountSqliteEntry entry = new TrackedAccountSqliteEntry {AccountId = account.ToLongRepresentation()};
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

		public async Task<List<AccountId>> GetTrackedAccounts(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return new List<AccountId>();
			}

			List<long> longAccountIds = accountIds.Select(a => a.ToLongRepresentation()).ToList();

			return await this.QueryAllAsync(db => {

				return db.TrackedAccounts.Where(a => longAccountIds.Contains(a.AccountId)).Select(a => a.AccountId.ToAccountId()).ToListAsync();
			}, accountIds).ConfigureAwait(false);
		}

		public Task<bool> AnyAccountsTracked() {

			return this.PerformOperationAsync(db => db.TrackedAccounts.AnyAsync());
		}

		public async Task<bool> AnyAccountsTracked(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return false;
			}

			List<long> longAccountIds = accountIds.Select(a => a.ToLongRepresentation()).ToList();

			return await this.AnyAllAsync(db => {

				return db.TrackedAccounts.AnyAsync(a => longAccountIds.Contains(a.AccountId));
			}, accountIds).ConfigureAwait(false);

		}

		public async Task<bool> IsAccountTracked(AccountId accountId) {
			if(accountId == default(AccountId)) {
				return false;
			}

			return await this.PerformOperationAsync(db => {

				return db.TrackedAccounts.AnyAsync(a => a.AccountId == accountId.ToLongRepresentation());
			}, this.GetKeyGroup(accountId)).ConfigureAwait(false);
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