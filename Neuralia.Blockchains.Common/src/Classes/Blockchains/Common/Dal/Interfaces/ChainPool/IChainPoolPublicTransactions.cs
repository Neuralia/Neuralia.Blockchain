using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool {

	public interface IChainPoolPublicTransactions {
		string TransactionId { get; set; }

		DateTime Timestamp { get; set; }

		bool Key { get; set; }
	}

	public interface IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolPublicTransactions
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}
}