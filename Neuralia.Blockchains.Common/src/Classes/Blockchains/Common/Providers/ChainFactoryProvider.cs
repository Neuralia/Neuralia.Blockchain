using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactoryBase { get; }
		IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainTypeCreationFactoryBase { get; }
		IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> WorkflowFactoryBase { get; }
		IMainChainMessageFactory MessageFactoryBase { get; }
		IClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ClientWorkflowFactoryBase { get; }
		IServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ServerWorkflowFactoryBase { get; }
		IGossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GossipWorkflowFactoryBase { get; }
		IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> TaskFactoryBase { get; }
		IBlockchainEventsRehydrationFactory BlockchainEventsRehydrationFactoryBase { get; }
	}

	public interface IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, TRANSACTION_BLOCK_FACTORY, MESSAGE_FACTORY, TASK_FACTORY, TYPE_CREATION_FACTORY, WORKFLOW_FACTORY, CLIENT_WORKFLOW_FACTORY, SERVER_WORKFLOW_FACTORY, GOSSIP_WORKFLOW_FACTORY, CHAIN_DAL_CREATION_FACTORY> : IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_DAL_CREATION_FACTORY : IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where MESSAGE_FACTORY : IMainChainMessageFactory
		where TASK_FACTORY : IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where TYPE_CREATION_FACTORY : IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WORKFLOW_FACTORY : IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CLIENT_WORKFLOW_FACTORY : IClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where SERVER_WORKFLOW_FACTORY : IServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where GOSSIP_WORKFLOW_FACTORY : IGossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where TRANSACTION_BLOCK_FACTORY : IBlockchainEventsRehydrationFactory {

		CHAIN_DAL_CREATION_FACTORY ChainDalCreationFactory { get; }
		TYPE_CREATION_FACTORY ChainTypeCreationFactory { get; }
		WORKFLOW_FACTORY WorkflowFactory { get; }
		MESSAGE_FACTORY MessageFactory { get; }
		CLIENT_WORKFLOW_FACTORY ClientWorkflowFactory { get; }
		SERVER_WORKFLOW_FACTORY ServerWorkflowFactory { get; }
		GOSSIP_WORKFLOW_FACTORY GossipWorkflowFactory { get; }
		TASK_FACTORY TaskFactory { get; }
		TRANSACTION_BLOCK_FACTORY BlockchainEventsRehydrationFactory { get; }
	}

	/// <summary>
	///     The main bucket holding all our chain factory types
	/// </summary>
	/// <typeparam name="TRANSACTION_BLOCK_FACTORY"></typeparam>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	/// <typeparam name="TASK_FACTORY"></typeparam>
	/// <typeparam name="TYPE_CREATION_FACTORY"></typeparam>
	/// <typeparam name="WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="CLIENT_WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="SERVER_WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="GOSSIP_WORKFLOW_FACTORY"></typeparam>
	public abstract class ChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, TRANSACTION_BLOCK_FACTORY, MESSAGE_FACTORY, TASK_FACTORY, TYPE_CREATION_FACTORY, WORKFLOW_FACTORY, CLIENT_WORKFLOW_FACTORY, SERVER_WORKFLOW_FACTORY, GOSSIP_WORKFLOW_FACTORY, CHAIN_DAL_CREATION_FACTORY> : ChainProvider, IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, TRANSACTION_BLOCK_FACTORY, MESSAGE_FACTORY, TASK_FACTORY, TYPE_CREATION_FACTORY, WORKFLOW_FACTORY, CLIENT_WORKFLOW_FACTORY, SERVER_WORKFLOW_FACTORY, GOSSIP_WORKFLOW_FACTORY, CHAIN_DAL_CREATION_FACTORY>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where MESSAGE_FACTORY : IMainChainMessageFactory
		where CHAIN_DAL_CREATION_FACTORY : IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where TASK_FACTORY : IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where TYPE_CREATION_FACTORY : IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WORKFLOW_FACTORY : IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CLIENT_WORKFLOW_FACTORY : IClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where SERVER_WORKFLOW_FACTORY : IServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where GOSSIP_WORKFLOW_FACTORY : IGossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where TRANSACTION_BLOCK_FACTORY : IBlockchainEventsRehydrationFactory {

		public ChainFactoryProvider(CHAIN_DAL_CREATION_FACTORY chainDalCreationFactory, TYPE_CREATION_FACTORY chainTypeCreationFactory, WORKFLOW_FACTORY workflowFactory, MESSAGE_FACTORY messageFactory, CLIENT_WORKFLOW_FACTORY clientWorkflowFactory, SERVER_WORKFLOW_FACTORY serverWorkflowFactory, GOSSIP_WORKFLOW_FACTORY gossipWorkflowFactory, TASK_FACTORY taskFactory, TRANSACTION_BLOCK_FACTORY blockchainEventsRehydrationFactory) {

			this.ChainDalCreationFactory = chainDalCreationFactory;
			this.ChainTypeCreationFactory = chainTypeCreationFactory;
			this.WorkflowFactory = workflowFactory;
			this.MessageFactory = messageFactory;
			this.ClientWorkflowFactory = clientWorkflowFactory;
			this.ServerWorkflowFactory = serverWorkflowFactory;
			this.GossipWorkflowFactory = gossipWorkflowFactory;
			this.TaskFactory = taskFactory;
			this.BlockchainEventsRehydrationFactory = blockchainEventsRehydrationFactory;

		}

		public IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainTypeCreationFactoryBase => this.ChainTypeCreationFactory;
		public IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactoryBase => this.ChainDalCreationFactory;
		public IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> WorkflowFactoryBase => this.WorkflowFactory;
		public IMainChainMessageFactory MessageFactoryBase => this.MessageFactory;
		public IClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ClientWorkflowFactoryBase => this.ClientWorkflowFactory;
		public IServerChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ServerWorkflowFactoryBase => this.ServerWorkflowFactory;
		public IGossipChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GossipWorkflowFactoryBase => this.GossipWorkflowFactory;

		public IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> TaskFactoryBase => this.TaskFactory;
		public IBlockchainEventsRehydrationFactory BlockchainEventsRehydrationFactoryBase => this.BlockchainEventsRehydrationFactory;

		public CHAIN_DAL_CREATION_FACTORY ChainDalCreationFactory { get; }
		public TYPE_CREATION_FACTORY ChainTypeCreationFactory { get; }
		public WORKFLOW_FACTORY WorkflowFactory { get; }
		public MESSAGE_FACTORY MessageFactory { get; }
		public CLIENT_WORKFLOW_FACTORY ClientWorkflowFactory { get; }
		public SERVER_WORKFLOW_FACTORY ServerWorkflowFactory { get; }
		public GOSSIP_WORKFLOW_FACTORY GossipWorkflowFactory { get; }
		public TASK_FACTORY TaskFactory { get; }
		public TRANSACTION_BLOCK_FACTORY BlockchainEventsRehydrationFactory { get; }
	}
}