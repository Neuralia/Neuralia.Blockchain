using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories {
	public interface IClientChainWorkflowFactory<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		IClientChainSyncWorkflow CreateChainSynchWorkflow(IFileSystem fileSystem);
		ISyncWalletWorkflow CreateSyncWalletWorkflow();
	}

	public abstract class ClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IClientChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public ClientChainWorkflowFactory(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
		}

		public abstract IClientChainSyncWorkflow CreateChainSynchWorkflow(IFileSystem fileSystem);

		public abstract ISyncWalletWorkflow CreateSyncWalletWorkflow();
	}
}