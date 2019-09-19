using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {
	public interface IAccountSnapshotDal : ISnapshotDal {
		void InsertNewAccount(AccountId accountId, List<(byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId)> keys, long inceptionBlockId, long? correlationId);
	}

	public interface IAccountSnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT> : ISnapshotDal<ACCOUNT_SNAPSHOT_CONTEXT>, IAccountSnapshotDal
		where ACCOUNT_SNAPSHOT_CONTEXT : IAccountSnapshotContext {
	}
}