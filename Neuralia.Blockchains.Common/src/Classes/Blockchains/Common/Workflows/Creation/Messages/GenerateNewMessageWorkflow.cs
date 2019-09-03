using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
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

		protected override List<IRoutedTask> EventValidated(IMessageEnvelope envelope) {
			//  we add it to our trusted cache, until it gets confirmed.
			var messageDispatchTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<int>();

			messageDispatchTask.SetAction((blockchainService, subTaskRouter) => {

				blockchainService.DispatchNewMessage(envelope, this.correlationContext);
			}, (subResults, subTaskRouter) => {
				if(subResults.Success) {
					Log.Information("Dispatch of miner registration blockchain message completed");
				} else {
					//TODO: what do we do here?
					Log.Error(subResults.Exception, "Failed to dispatch miner registration blockchain message");
				}
			});

			return new List<IRoutedTask> {messageDispatchTask};
		}

		protected override void ValidationFailed(IMessageEnvelope envelope, ValidationResult results) {
			base.ValidationFailed(envelope, results);

			//TODO: any error message for blockchain messages?
			// if(results is TransactionValidationResult transactionValidationResult) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, transactionValidationResult.ErrorCodes), this.correlationContext);
			// }
		}

		protected override void ExceptionOccured(Exception ex) {
			base.ExceptionOccured(ex);

			// if(ex is EventGenerationException evex && evex.Envelope is IMessageEnvelope envelope) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, null), this.correlationContext);
			// }
		}

		protected override void PerformSanityChecks() {
			base.PerformSanityChecks();

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