using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IAccountKeysSnapshotSqliteEntry : IAccountKeysSnapshotEntry {
	}

	public abstract class AccountKeysSnapshotSqliteEntry : IAccountKeysSnapshotSqliteEntry {

		public byte OrdinalId { get; set; }
		public long AccountId { get; set; }

		public byte[] PublicKey { get; set; }
		public string DeclarationTransactionId { get; set; }
		public long DeclarationBlockId { get; set; }
	}
}