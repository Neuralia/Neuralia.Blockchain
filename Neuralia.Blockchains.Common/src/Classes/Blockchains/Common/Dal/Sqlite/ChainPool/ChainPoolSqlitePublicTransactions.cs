using System;
using System.ComponentModel.DataAnnotations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainPool {

	public interface IChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}

	public abstract class ChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS>
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolSqlitePublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
		public DateTime Expiration { get; set; }

		[Key]
		public string TransactionId { get; set; }

		public DateTime Timestamp { get; set; }
		public bool Key { get; set; }
	}
}