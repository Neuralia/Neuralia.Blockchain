using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {
	public interface IAccountKeysSnapshotDal : ISnapshotDal {
		void InsertUpdateAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId);
		void InsertNewAccountKey(AccountId accountId, byte ordinal, SafeArrayHandle key, TransactionId declarationTransactionId, long inceptionBlockId);
	}

	public interface IAccountKeysSnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT> : IAccountKeysSnapshotDal, ISnapshotDal<ACCOUNT_KEYS_SNAPSHOT_CONTEXT>
		where ACCOUNT_KEYS_SNAPSHOT_CONTEXT : IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEYS_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT : class, IAccountKeysSnapshotEntry, new() {

		void Clear();
		List<STANDARD_ACCOUNT_KEYS_SNAPSHOT> LoadAccountKeys(Func<ACCOUNT_KEYS_SNAPSHOT_CONTEXT, List<STANDARD_ACCOUNT_KEYS_SNAPSHOT>> operation, List<(long accountId, byte ordinal)> accountIds);
		void UpdateSnapshotDigestFromDigest(Action<ACCOUNT_KEYS_SNAPSHOT_CONTEXT> operation, STANDARD_ACCOUNT_KEYS_SNAPSHOT accountSnapshotEntry);

		List<(ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCOUNT_KEYS_SNAPSHOT_CONTEXT>>> actions);
	}

}