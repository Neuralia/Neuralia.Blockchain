using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IStandardAccountSnapshotDal : IAccountSnapshotDal {
	}

	public interface IStandardAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IStandardAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new() {

		Task Clear();
		Task<List<ACCOUNT_SNAPSHOT>> LoadAccounts(List<AccountId> accountIds);

		Task UpdateSnapshotEntry(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);
		Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_SNAPSHOT_CONTEXT, Task> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);

		Task<List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> actions);
	}

}