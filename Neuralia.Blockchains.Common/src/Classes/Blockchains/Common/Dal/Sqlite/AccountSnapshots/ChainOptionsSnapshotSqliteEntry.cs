using System.ComponentModel.DataAnnotations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IChainOptionsSnapshotSqliteEntry : IChainOptionsSnapshotEntry {
	}

	public class ChainOptionsSnapshotSqliteEntry : IChainOptionsSnapshotSqliteEntry {

		[Key]
		public int Id { get; set; }

		public string MaximumVersionAllowed { get; set; }
		public string MinimumWarningVersionAllowed { get; set; }
		public string MinimumVersionAllowed { get; set; }
		public int MaxBlockInterval { get; set; }
	}
}