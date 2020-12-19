using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {

	public interface IStandardAccountSnapshotContext : IAccountSnapshotContext {
	}

	public interface IStandardAccountSnapshotContext<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT> : IStandardAccountSnapshotContext
		where ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new() {

		DbSet<ACCOUNT_SNAPSHOT> StandardAccountSnapshots { get; set; }
		DbSet<ACCOUNT_ATTRIBUTE_SNAPSHOT> StandardAccountSnapshotAttributes { get; set; }
	}

}