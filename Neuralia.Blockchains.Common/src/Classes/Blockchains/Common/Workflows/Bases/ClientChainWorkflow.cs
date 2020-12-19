using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {

	public interface IClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : INetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class ClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : NetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected PeerConnection PeerConnection;

		public ClientChainWorkflow(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {

			this.CorrelationId = GlobalRandom.GetNextUInt();

			if(GlobalSettings.ApplicationSettings.P2PEnabled) {

				// this is our own workflow, we ensure the client is always 0. (no client, but rather us)
				this.ClientId = this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.MyClientUuid;
			}
		}
	}
}