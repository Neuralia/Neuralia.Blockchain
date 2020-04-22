using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IChainOptionsSnapshotDal : ISnapshotDal {
	}

	public interface IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT> : IChainOptionsSnapshotDal, ISnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : IChainOptionsSnapshotContext
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {

		void EnsureEntryCreated(Action<CHAIN_OPTIONS_SNAPSHOT_CONTEXT> operation);
		Task<CHAIN_OPTIONS_SNAPSHOT> LoadChainOptionsSnapshot(Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, Task<CHAIN_OPTIONS_SNAPSHOT>> operation);

		Task Clear();
		Task UpdateSnapshotDigestFromDigest(Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, Task> operation);

		Task<List<(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, LockContext, Task>>> actions);
	}

}