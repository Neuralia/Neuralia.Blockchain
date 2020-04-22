using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {
	public interface IAccountSnapshotDal : ISnapshotDal {
		Task InsertNewAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, bool correlated);
	}

	public interface IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT> : ISnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IAccountSnapshotContext {
	}
}