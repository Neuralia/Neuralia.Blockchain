using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IChainOptionsSnapshotDal : ISnapshotDal {
	}

	public interface IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT> : IChainOptionsSnapshotDal, ISnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : IChainOptionsSnapshotContext
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {

		void EnsureEntryCreated(Action<CHAIN_OPTIONS_SNAPSHOT_CONTEXT> operation);
		List<CHAIN_OPTIONS_SNAPSHOT> LoadChainOptionsSnapshots(Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, List<CHAIN_OPTIONS_SNAPSHOT>> operation);

		void Clear();
		void UpdateSnapshotDigestFromDigest(Action<CHAIN_OPTIONS_SNAPSHOT_CONTEXT> operation);

		List<(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>>> actions);
	}

}