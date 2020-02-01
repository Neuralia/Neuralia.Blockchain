using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IStandardAccountSnapshotDal : IAccountSnapshotDal {
	}

	public interface IStandardAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT, ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IStandardAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new() {

		void Clear();
		List<ACCOUNT_SNAPSHOT> LoadAccounts(List<AccountId> accountIds);
		void UpdateSnapshotEntry(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);
		void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_SNAPSHOT_CONTEXT> operation, ACCOUNT_SNAPSHOT accountSnapshotEntry);

		List<(ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_SNAPSHOT_CONTEXT>>> actions);
	}

}