using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain {
	public interface IGenerateXmssKeyIndexNodeCacheWorkflow : IChainWorkflow {
	}

	public interface IGenerateXmssKeyIndexNodeCacheWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IGenerateXmssKeyIndexNodeCacheWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This workflow will ensure that the wallet is in sync with the chain.
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class GenerateXmssKeyIndexNodeCacheWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IGenerateXmssKeyIndexNodeCacheWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CorrelationContext correlationContext;
		private readonly string accountCode;
		private readonly byte ordinal;
		private readonly long index;
		
		public GenerateXmssKeyIndexNodeCacheWorkflow(string accountCode, byte ordinal, long index, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator) {
			this.correlationContext = correlationContext;
			this.accountCode = accountCode;
			this.ordinal = ordinal;
			this.index = index;
		}

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			taskRoutingContext.SetCorrelationContext(this.correlationContext);

			// let's sleep a little, give it time to pass
			//TODO: do we need this? it could impede with high rate signatures.
			await Hibernate(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
			
			await centralCoordinator.ChainComponentProvider.WalletProviderBase.GenerateXmssKeyIndexNodeCache(accountCode, ordinal, index, lockContext).ConfigureAwait(false);
		}
	}
}