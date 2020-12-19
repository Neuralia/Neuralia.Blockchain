using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks {

	public interface IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

//		ValidationTask<IValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateValidationTask<K>();
		BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateBlockchainTask<K>();
	}

	public abstract class WorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowTaskFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public WorkflowTaskFactory(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
		}

		// generic tasks

		// public ValidationTask<IValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateValidationTask<K>() {
		// 	return new ValidationTask<IValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>();
		// }

		public BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateBlockchainTask<K>() {
			return new BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>();
		}
	}
}