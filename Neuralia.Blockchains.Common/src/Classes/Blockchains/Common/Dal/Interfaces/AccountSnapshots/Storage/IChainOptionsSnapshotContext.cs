using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IChainOptionsSnapshotContext : ISnapshotContext {
	}

	public interface IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT> : IChainOptionsSnapshotContext
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {

		DbSet<CHAIN_OPTIONS_SNAPSHOT> ChainOptionsSnapshots { get; set; }
	}

}