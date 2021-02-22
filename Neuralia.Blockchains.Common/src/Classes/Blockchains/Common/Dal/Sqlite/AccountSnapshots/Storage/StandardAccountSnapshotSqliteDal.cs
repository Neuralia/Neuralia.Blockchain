using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IStandardAccountSnapshotSqliteDal : IAccountSnapshotSqliteDal {
	}

	public interface IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IStandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeSqliteEntry, new() {
	}

	public abstract class StandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IStandardAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : StandardAccountSnapshotSqliteContext<STANDARD_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new() {

		protected StandardAccountSnapshotSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public abstract ICardUtils CardUtils { get; }

		public Task<List<STANDARD_ACCOUNT_SNAPSHOT>> LoadAccounts(List<AccountId> accountIds) {

			List<long> longAccountIds = accountIds.Where(a => a.IsStandard).Select(a => a.ToLongRepresentation()).ToList();

			return this.QueryAllAsync((db, lc) => {

				return db.StandardAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToListAsync();
			}, accountIds);

		}

		public Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {
			LockContext lockContext = null;
			return this.PerformOperationAsync(operation, lockContext, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId()));
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task> operation, STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {
			LockContext lockContext = null;
			return this.PerformOperationAsync(operation, lockContext, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId()));
		}

		public Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<AccountId, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public new Task<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> BeginHoldingTransaction() {
			return base.BeginHoldingTransaction();
		}

		public STANDARD_ACCOUNT_SNAPSHOT PrepareNewStandardAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, bool verified) {
			STANDARD_ACCOUNT_SNAPSHOT source = new STANDARD_ACCOUNT_SNAPSHOT();

			source.AccountId = accountId.ToLongRepresentation();
			source.InceptionBlockId = inceptionBlockId;
			source.Correlated = verified;
			
			STANDARD_ACCOUNT_SNAPSHOT accountEntry = new STANDARD_ACCOUNT_SNAPSHOT();
			this.CardUtils.Copy(source, accountEntry);

			return accountEntry;
		}
		
		public Task InsertNewStandardAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, bool verified) {
			LockContext lockContext = null;
			return this.PerformOperationAsync((db, lc) => {
				STANDARD_ACCOUNT_SNAPSHOT accountEntry = this.PrepareNewStandardAccount(accountId,keys, inceptionBlockId, verified);
				
				db.StandardAccountSnapshots.Add(accountEntry);

				return db.SaveChangesAsync();

			}, lockContext, this.GetKeyGroup(accountId));
		}
	}
}