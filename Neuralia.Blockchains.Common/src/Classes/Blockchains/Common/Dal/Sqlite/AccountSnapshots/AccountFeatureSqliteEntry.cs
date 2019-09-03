using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IAccountFeatureSqliteEntry : IAccountFeatureEntry {
	}

	public class AccountFeatureSqliteEntry : AccountFeatureEntry, IAccountFeatureSqliteEntry {
	}
}