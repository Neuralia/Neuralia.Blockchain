using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories {

	public interface IGossipChainWorkflowFactory<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		INetworkingWorkflow<IBlockchainEventsRehydrationFactory> CreateGossipResponseWorkflow(IBlockchainGossipMessageSet messageSet, PeerConnection peerConnectionn);
	}

	public abstract class GossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : WorkflowFactory<IBlockchainEventsRehydrationFactory>, IGossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public GossipChainWorkflowFactory(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;
		}

		public abstract INetworkingWorkflow<IBlockchainEventsRehydrationFactory> CreateGossipResponseWorkflow(IBlockchainGossipMessageSet messageSet, PeerConnection peerConnectionn);

		protected void ValidateTrigger(IBlockchainGossipMessageSet messageSet) {
			if(!messageSet.MessageCreated) {
				throw new ApplicationException("Message must be created or loaded");
			}

			if(!messageSet.BaseHeader.IsWorkflowTrigger) {
				throw new ApplicationException("Message header must be marked as a workflow trigger");
			}

			if(!(messageSet.BaseMessage is IGossipWorkflowTriggerMessage)) {
				throw new ApplicationException("Message must be both a gossip message trigger and set as such");
			}
		}
	}
}