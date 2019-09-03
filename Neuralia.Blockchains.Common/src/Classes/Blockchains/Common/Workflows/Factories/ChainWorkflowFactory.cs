using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.HandleReceivedGossipMessage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories {

	public interface IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		ICreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreatePresentationTransactionChainWorkflow(CorrelationContext correlationContext);
		ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChangeKeyTransactionWorkflow(byte changingKeyOrdinal, string note, CorrelationContext correlationContext);
		IReceiveGossipMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateReceiveGossipMessageWorkflow(IBlockchainGossipMessageSet gossipMessageSet, PeerConnection connection);

		ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendElectionsCandidateRegistrationMessageWorkflow(AccountId candidateAccountId, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, CorrelationContext correlationContext);

		ILoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateLoadWalletWorkflow();
	}

	public abstract class ChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : WorkflowFactory<IBlockchainEventsRehydrationFactory>, IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public ChainWorkflowFactory(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;
		}

		public abstract ICreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreatePresentationTransactionChainWorkflow(CorrelationContext correlationContext);

		public abstract ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChangeKeyTransactionWorkflow(byte changingKeyOrdinal, string note, CorrelationContext correlationContext);

		public abstract IReceiveGossipMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateReceiveGossipMessageWorkflow(IBlockchainGossipMessageSet gossipMessageSet, PeerConnection connection);

		// message workflows
		public abstract ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendElectionsCandidateRegistrationMessageWorkflow(AccountId candidateAccountId, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, CorrelationContext correlationContext);
		public abstract ILoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateLoadWalletWorkflow();
	}
}