using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {

	public interface IAccountKeysSnapshotContext : ISnapshotContext {
	}

	public interface IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotContext
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IAccountKeysSnapshotEntry, new() {

		DbSet<STANDARD_ACCOUNT_KEYS_SNAPSHOT> StandardAccountKeysSnapshots { get; set; }
	}

}