using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories {
	public interface IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; set; }
		
		IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> gossipManager { get; set; }
		IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainManager { get; set; }
		IBlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainInterface { get; set; }
	}

	public class ChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; set; }

		public IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> gossipManager { get; set; }
		public IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainManager { get; set; }

		public IBlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainInterface { get; set; }
	}
}