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
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotSqliteDal : IAccountSnapshotSqliteDal {
	}

	public interface IJointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : IAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IJointAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, IJointAccountSnapshotSqliteDal
		where ACCOUNT_SNAPSHOT_CONTEXT : class, IJointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountSqliteEntry, new() {
	}

	public abstract class JointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT> : AccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>, IJointAccountSnapshotSqliteDal<ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT_CONTEXT : JointAccountSnapshotSqliteContext<JOINT_ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT : JointAccountSnapshotSqliteEntry<ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_FEATURE_SNAPSHOT : AccountFeatureSqliteEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : JointMemberAccountSqliteEntry, new() {

		protected JointAccountSnapshotSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, chainDalCreationFactory.CreateJointAccountSnapshotContext<ACCOUNT_SNAPSHOT_CONTEXT>, serializationType) {
		}

		public void Clear() {
			foreach(string file in this.GetAllFileGroups()) {
				if(File.Exists(file)) {
					File.Delete(file);
				}
			}
		}

		public List<JOINT_ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds) {

			var longAccountIds = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).Select(a => a.ToLongRepresentation()).ToList();

			return this.QueryAll(db => {

				return db.JointAccountSnapshots.Where(s => longAccountIds.Contains(s.AccountId)).ToList();
			}, longAccountIds);
		}

		public void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId));
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.AccountId));
		}

		public new List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}

		public void InsertNewJointAccount(AccountId accountId, long inceptionBlockId) {

			this.PerformOperation(db => {
				JOINT_ACCOUNT_SNAPSHOT accountEntry = new JOINT_ACCOUNT_SNAPSHOT();

				accountEntry.AccountId = accountId.ToLongRepresentation();
				accountEntry.InceptionBlockId = inceptionBlockId;

				db.JointAccountSnapshots.Add(accountEntry);

				db.SaveChanges();
			}, this.GetKeyGroup(accountId.SequenceId));

		}
	}
}