using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool {
	public interface IChainPoolDal : IDalInterfaceBase {

		Task InsertTransactionEntry(ITransactionEnvelope signedTransactionEnvelope, DateTime chainInception);
		Task RemoveTransactionEntry(TransactionId transactionId);
		Task<List<TransactionId>> GetTransactions();

		Task ClearTransactions();
		Task ClearExpiredTransactions();
		Task ClearTransactions(List<TransactionId> transactionIds);
		Task RemoveTransactionEntries(List<TransactionId> transactionIds);
	}

	public interface IChainPoolDal<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolDal
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}
}