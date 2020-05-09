using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {
	public interface IAccountKeysSnapshotDal : ISnapshotDal {
		Task InsertUpdateAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId);
		Task InsertNewAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId);
	}

	public interface IAccountKeysSnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotDal, ISnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT>
		where ACCOUNT_KEYS_SNAPSHOT_CONTEXT : IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IAccountKeysSnapshotEntry, new() {

		Task Clear();
		Task<List<STANDARD_ACCOUNT_KEYS_SNAPSHOT>> LoadAccountKeys(Func<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, Task<List<STANDARD_ACCOUNT_KEYS_SNAPSHOT>>> operation, List<(long accountId, byte ordinal)> accountIds);
		Task UpdateSnapshotDigestFromDigest(Func<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, Task> operation, STANDARD_ACCOUNT_KEYS_SNAPSHOT accountSnapshotEntry);

		Task<List<(ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, LockContext, Task>>> actions);
	}

}