using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;

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
		

		public List<JOINT_ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds) {

			var longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.ToLongRepresentation()).ToList();
			var sequenceIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.SequenceId).ToList();
			
			return this.QueryAll(db => {

				return db.JointAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToList();
			}, sequenceIds);
		}

		public void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId.ToAccountId().SequenceId));
		}

		public new List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public void InsertNewJointAccount(AccountId accountId, long inceptionBlockId, long? correlationId) {

			this.PerformOperation(db => {
				JOINT_ACCOUNT_SNAPSHOT accountEntry = new JOINT_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;
				accountEntry.CorrelationId = correlationId;

				db.JointAccountSnapshots.Add(accountEntry);

				db.SaveChanges();
			}, this.GetKeyGroup(accountId.SequenceId));

		}
	}
}