using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain {
	public interface ILoadWalletWorkflow : IChainWorkflow {
	}

	public interface ILoadWalletWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ILoadWalletWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This workflow will ensure that the wallet is in sync with the chain.
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class LoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ILoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CorrelationContext correlationContext;
		private readonly string passphrase;
		
		public LoadWalletWorkflow(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext, string passphrase = null) : base(centralCoordinator) {
			this.correlationContext = correlationContext;
			this.passphrase = passphrase;
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {
			
			taskRoutingContext.SetCorrelationContext(this.correlationContext);
			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadWallet(this.correlationContext, this.passphrase);
		}
	}
}