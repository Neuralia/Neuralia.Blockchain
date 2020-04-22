using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface ITrackedAccountsDal : ISnapshotDal {
		void AddTrackedAccounts(List<AccountId> accounts);
		void RemoveTrackedAccounts(List<AccountId> accounts);

		Task<List<AccountId>> GetTrackedAccounts(List<AccountId> accounts);
		Task<bool> AnyAccountsTracked(List<AccountId> accounts);
		Task<bool> AnyAccountsTracked();
		Task<bool> IsAccountTracked(AccountId account);
	}

	public interface ITrackedAccountsDal<TRACKED_ACCOUNTS_CONTEXT> : ISnapshotDal<TRACKED_ACCOUNTS_CONTEXT>, ITrackedAccountsDal
		where TRACKED_ACCOUNTS_CONTEXT : ITrackedAccountsContext {
	}
}