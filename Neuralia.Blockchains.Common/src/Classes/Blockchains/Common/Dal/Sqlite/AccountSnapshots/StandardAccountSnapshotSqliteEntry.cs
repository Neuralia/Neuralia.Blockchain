using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IStandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE> : IStandardAccountSnapshotEntry<ACCOUNT_FEATURE>, IAccountSnapshotSqliteEntry<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : class, IAccountFeatureSqliteEntry, new() {
	}

	/// <summary>
	///     Here we store various metadata state about our chain
	/// </summary>
	public abstract class StandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE> : StandardAccountSnapshot<ACCOUNT_FEATURE>, IStandardAccountSnapshotSqliteEntry<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : AccountFeatureSqliteEntry, new() {
	}
}