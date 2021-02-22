using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base {

	public interface IAccountKeysSnapshotSqliteDal : IAccountKeysSnapshotDal {
	}

	public interface IAccountKeysSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IIndexedSqliteDal<IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>>, IAccountKeysSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT>, IAccountKeysSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotSqliteEntry, new() {
	}

	public abstract class AccountKeysSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IndexedSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT>, IAccountKeysSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IAccountKeysSnapshotSqliteContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IStandardAccountKeysSnapshotSqliteEntry, new() {

		protected AccountKeysSnapshotSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateStandardAccountKeysSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public Task<List<STANDARD_ACCOUNT_KEYS_SNAPSHOT>> LoadAccountKeys(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task<List<STANDARD_ACCOUNT_KEYS_SNAPSHOT>>> operation, List<(long accountId, byte ordinal)> keyIds) {

			List<AccountId> accountIds = keyIds.Select(a => AccountId.FromLongRepresentation(a.accountId)).ToList();
			LockContext lockContext = null;
			return this.QueryAllAsync(operation, accountIds, lockContext);

		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task> operation, STANDARD_ACCOUNT_KEYS_SNAPSHOT accountSnapshotEntry) {
			LockContext lockContext = null;
			return this.PerformOperationAsync(operation, lockContext, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId()));
		}

		public Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<AccountId, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			LockContext lockContext = null;
			return this.PerformProcessingSetHoldTransactions(actions, lockContext);
		}

		public Task InsertNewAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId) {
			LockContext lockContext = null;
			return this.PerformOperationAsync((db, lc) => {
				STANDARD_ACCOUNT_KEYS_SNAPSHOT accountKey = new STANDARD_ACCOUNT_KEYS_SNAPSHOT();

				accountKey.CompositeKey = this.GetCardUtils().GenerateCompositeKey(accountId, ordinal);
				accountKey.AccountId = accountId.ToLongRepresentation();
				accountKey.OrdinalId = ordinal;

				if((key == null) || key.IsNull) {
					accountKey.PublicKey = null;
				} else {
					accountKey.PublicKey = key.ToExactByteArray(); // accountId sequential key
				}

				accountKey.DeclarationTransactionId = declarationTransactionId.ToString();
				accountKey.DeclarationBlockId = inceptionBlockId;

				db.StandardAccountKeysSnapshots.Add(accountKey);

				return db.SaveChangesAsync();

			}, lockContext, this.GetKeyGroup(accountId));

		}

		public Task InsertUpdateAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId) {

			long accountIdLong = accountId.ToLongRepresentation();
			LockContext lockContext = null;
			return this.PerformOperationAsync(async (db, lc) => {
				STANDARD_ACCOUNT_KEYS_SNAPSHOT accountKey = db.StandardAccountKeysSnapshots.SingleOrDefault(e => (e.OrdinalId == ordinal) && (e.AccountId == accountIdLong));

				if(accountKey == null) {
					await this.InsertNewAccountKey(accountId, ordinal, key, declarationTransactionId, inceptionBlockId).ConfigureAwait(false);

					return;
				}

				if((key == null) || key.IsNull) {
					accountKey.PublicKey = null;
				} else {
					accountKey.PublicKey = key.ToExactByteArray();
				}

				accountKey.DeclarationTransactionId = declarationTransactionId.ToString();
				accountKey.DeclarationBlockId = inceptionBlockId;

				await db.SaveChangesAsync().ConfigureAwait(false);
			}, lockContext, this.GetKeyGroup(accountId));

		}

		protected abstract ICardUtils GetCardUtils();
	}
}