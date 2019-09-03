using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainPool {

	public interface IChainPoolContext : IContextInterfaceBase {
	}

	public interface IChainPoolContext<CHAIN_POOL_PUBLIC_TRANSACTIONS> : IChainPoolContext
		where CHAIN_POOL_PUBLIC_TRANSACTIONS : class, IChainPoolPublicTransactions<CHAIN_POOL_PUBLIC_TRANSACTIONS> {
	}
}