using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.P2p.Connections;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {
	public interface IGossipChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : INetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class GossipChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, GOSSIP_MESSAGE_SET, GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE> : NetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IGossipChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where GOSSIP_MESSAGE_SET : IBlockchainGossipMessageSet<GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE>
		where GOSSIP_WORKFLOW_TRIGGER_MESSAGE : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IEnvelope {

		private readonly BlockchainType chainType;

		protected readonly PeerConnection PeerConnection;

		/// <summary>
		///     the trigger message that prompted the creation of this server workflow
		/// </summary>
		protected readonly GOSSIP_MESSAGE_SET triggerMessage;

		public GossipChainWorkflow(BlockchainType chainType, CENTRAL_COORDINATOR centralCoordinator, GOSSIP_MESSAGE_SET triggerMessage, PeerConnection peerConnectionn) : base(centralCoordinator) {

			this.chainType = chainType;
			this.triggerMessage = triggerMessage;
			this.PeerConnection = peerConnectionn;
		}
	}
}