using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface ITrackedAccountsContext : ISnapshotContext {
	}

	public interface ITrackedAccountsContext<TRACKED_ACCOUNT_SNAPSHOT> : ITrackedAccountsContext
		where TRACKED_ACCOUNT_SNAPSHOT : class, ITrackedAccount, new() {

		DbSet<TRACKED_ACCOUNT_SNAPSHOT> TrackedAccounts { get; set; }
	}
}