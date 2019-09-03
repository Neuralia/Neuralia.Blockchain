using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public class TrackedAccountSqliteEntry : ITrackedAccount {

		public long AccountId { get; set; }
	}
}