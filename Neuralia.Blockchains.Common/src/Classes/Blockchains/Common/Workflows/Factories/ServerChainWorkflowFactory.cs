using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories {
	public interface IServerChainWorkflowFactory<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		INetworkingWorkflow<IBlockchainEventsRehydrationFactory> CreateResponseWorkflow(IBlockchainTriggerMessageSet messageSet, PeerConnection peerConnectionn);
	}

	public abstract class ServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : WorkflowFactory<IBlockchainEventsRehydrationFactory>, IServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public ServerChainWorkflowFactory(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;
		}

		public abstract INetworkingWorkflow<IBlockchainEventsRehydrationFactory> CreateResponseWorkflow(IBlockchainTriggerMessageSet messageSet, PeerConnection peerConnectionn);

		protected void ValidateTrigger<T>(IBlockchainTriggerMessageSet messageSet)
			where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory> {
			if(!messageSet.MessageCreated) {
				throw new ApplicationException("Message must be created or loaded");
			}

			if(!(messageSet.BaseHeader.IsWorkflowTrigger && messageSet.BaseMessage is T)) {
				throw new ApplicationException("Message must be both a trigger and set as such");
			}
		}
	}
}