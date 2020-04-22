using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IJointAccountSnapshotDal : IAccountSnapshotDal {
		Task InsertNewJointAccount(AccountId accountId, long inceptionBlockId, bool correlated);
	}

	public interface IJointAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT> : IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IJointAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IJointAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT, ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new() {

		Task Clear();
		Task<List<ACCOUNT_SNAPSHOT>> LoadAccounts(List<AccountId> accountIds);
		Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);
		Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);

		Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions);
	}

}