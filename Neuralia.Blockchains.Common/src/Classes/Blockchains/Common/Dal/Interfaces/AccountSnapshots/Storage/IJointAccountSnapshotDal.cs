using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotDal : IAccountSnapshotDal {
		void InsertNewJointAccount(AccountId accountId, long inceptionBlockId, long? correlationId);
	}

	public interface IJointAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT> : IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IJointAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IJointAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new() {

		void Clear();
		List<ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds);
		void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);
		void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);

		List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions);
	}

}