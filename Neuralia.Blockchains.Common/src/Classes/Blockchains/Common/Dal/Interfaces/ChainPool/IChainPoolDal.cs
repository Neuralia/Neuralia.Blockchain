using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool {
	public interface IChainPoolDal : IDalInterfaceBase {

		void InsertTransactionEntry(ITransactionEnvelope transactionEnvelope, DateTime chainInception);
		void RemoveTransactionEntry(TransactionId transactionId);
		List<TransactionId> GetTransactions();

		void ClearTransactions();
		void ClearExpiredTransactions();
		void ClearTransactions(List<TransactionId> transactionIds);
		void RemoveTransactionEntries(List<TransactionId> transactionIds);
	}

	public interface IChainPoolDal<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolDal
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}
}