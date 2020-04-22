using System;
using System.Collections.Generic;
using System.IO;
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

			var longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.ToLongRepresentation()).ToList();
			var sequenceIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.SequenceId).ToList();
			
			return this.QueryAllAsync(db => {

				return db.JointAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToListAsync();
			}, sequenceIds);
		}

		public Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public Task InsertNewJointAccount(AccountId accountId, long inceptionBlockId, bool correlated) {

			return this.PerformOperation(db => {
				JOINT_ACCOUNT_SNAPSHOT accountEntry = new JOINT_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;
				accountEntry.Correlated = correlated;

				db.JointAccountSnapshots.Add(accountEntry);

				return db.SaveChangesAsync();
			}, this.GetKeyGroup(accountId.SequenceId));

		}
	}
}