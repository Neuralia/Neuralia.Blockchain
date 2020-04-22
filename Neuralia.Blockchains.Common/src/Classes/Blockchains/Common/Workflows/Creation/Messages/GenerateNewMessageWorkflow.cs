using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages {

	public interface IGenerateNewMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IEventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This is the base class for all transaction generating workflows
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public abstract class GenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER> : EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER, IMessageEnvelope>, IGenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public GenerateNewMessageWorkflow(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {

		}

		protected override async Task EventGenerationCompleted(IMessageEnvelope envelope, LockContext lockContext) {

			//  we add it to our trusted cache, until it gets confirmed.

			try {
				await this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchNewMessage(envelope, this.correlationContext).ConfigureAwait(false);
				Log.Information("Dispatch of miner registration blockchain message completed");
			} catch(Exception ex) {
				Log.Error(ex, "Failed to dispatch miner registration blockchain message");
			}
		}

		protected override void ValidationFailed(IMessageEnvelope envelope, ValidationResult results) {
			base.ValidationFailed(envelope, results);

			//TODO: any error message for blockchain messages?
			// if(results is TransactionValidationResult transactionValidationResult) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, transactionValidationResult.ErrorCodes), this.correlationContext);
			// }
		}

		protected override Task ExceptionOccured(Exception ex) {
			return base.ExceptionOccured(ex);

			// if(ex is EventGenerationException evex && evex.Envelope is IMessageEnvelope envelope) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, null), this.correlationContext);
			// }
		}

		protected override async Task PerformSanityChecks(LockContext lockContext) {
			await base.PerformSanityChecks(lockContext).ConfigureAwait(false);

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount < chainConfiguration.MinimumDispatchPeerCount) {

				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					throw new EventGenerationException("Failed to create message. We are not connected to any peers on the p2p network");
				}

				throw new EventGenerationException($"Failed to create message. We do not have enough peers. we need a minimum of {chainConfiguration.MinimumDispatchPeerCount}");
			}
		}
	}
}