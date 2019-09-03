using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface ITrackedAccountsDal : ISnapshotDal {
		void AddTrackedAccounts(List<AccountId> accounts);
		void RemoveTrackedAccounts(List<AccountId> accounts);

		List<AccountId> GetTrackedAccounts(List<AccountId> accounts);
		bool AnyAccountsTracked(List<AccountId> accounts);
		bool AnyAccountsTracked();
		bool IsAccountTracked(AccountId account);
	}

	public interface ITrackedAccountsDal<TRACKED_ACCOUNTS_CONTEXT> : ISnapshotDal<TRACKED_ACCOUNTS_CONTEXT>, ITrackedAccountsDal
		where TRACKED_ACCOUNTS_CONTEXT : ITrackedAccountsContext {
	}
}