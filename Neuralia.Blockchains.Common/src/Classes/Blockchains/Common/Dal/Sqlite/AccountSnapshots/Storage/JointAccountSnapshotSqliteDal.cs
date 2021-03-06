using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotSqliteDal : IAccountSnapshotSqliteDal {
	}

	public interface IJointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IJointAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, IJointAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IJointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountSqliteEntry, new() {
	}

	public abstract class JointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>, IJointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : JointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT : JointAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttributeSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : JointMemberAccountSqliteEntry, new() {

		protected JointAccountSnapshotSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateJointAccountSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public Task<List<JOINT_ACCOUNT_SNAPSHOT>> LoadAccounts(List<AccountId> accountIds) {

			List<long> longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.ToLongRepresentation()).ToList();

			return this.QueryAllAsync((db, lc) => {

				return db.JointAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToListAsync();
			}, accountIds);
		}

		public Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			LockContext lockContext = null;
			return this.PerformOperationAsync(operation, lockContext, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId()));
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			LockContext lockContext = null;
			return this.PerformOperationAsync(operation, lockContext, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId()));
		}

		public Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<AccountId, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public Task InsertNewJointAccount(AccountId accountId, long inceptionBlockId, bool Verified) {

			LockContext lockContext = null;
			return this.PerformOperation((db, lc) => {
				JOINT_ACCOUNT_SNAPSHOT accountEntry = new JOINT_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;
				accountEntry.Correlated = Verified;

				db.JointAccountSnapshots.Add(accountEntry);

				return db.SaveChangesAsync();
			}, lockContext, this.GetKeyGroup(accountId));

		}
	}
}