using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions {

	public interface IGenerateNewTransactionWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IEventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This is the base class for all transaction generating workflows
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public abstract class GenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER> : EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER, ITransactionEnvelope>, IGenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly string note;

		public GenerateNewTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, string note, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.note = note;
		}

		protected override List<IRoutedTask> EventValidated(ITransactionEnvelope envelope) {
			var transactionInsertTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<int>();

			transactionInsertTask.SetAction((blockchainService, subTaskRouter) => {

				blockchainService.InsertLocalTransaction(envelope, this.note, this.correlationContext);
			}, (subresults, subTaskRouter) => {
				if(subresults.Success) {
					Log.Information("Insertion of transaction into blockchain completed");

					//TODO: do we need a transaction created event? we already sent a "TransactionSent" event inside the InsertLocalTransaction method.
				} else {
					string message = "Failed to insert transaction into blockchain";
					Log.Error(subresults.Exception, message);

					if(subresults.Exception is EventValidationException eventValidationException) {

						this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, eventValidationException.Result.ErrorCodes));
					}

				}
			});

			this.RoutedTaskReceiver.DispatchTaskSync(transactionInsertTask);

			return new List<IRoutedTask>();
		}

		protected override void ValidationFailed(ITransactionEnvelope envelope, ValidationResult results) {
			base.ValidationFailed(envelope, results);

			if(results is TransactionValidationResult transactionValidationResult) {
				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, transactionValidationResult.ErrorCodes), this.correlationContext);
			}
		}

		protected override void ExceptionOccured(Exception ex) {
			base.ExceptionOccured(ex);

			if(ex is EventGenerationException evex && evex.Envelope is ITransactionEnvelope envelope) {
				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, null), this.correlationContext);
			}
		}

		protected override void PerformSanityChecks() {
			base.PerformSanityChecks();

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount < chainConfiguration.MinimumDispatchPeerCount) {

				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					throw new EventGenerationException("Failed to create transaction. We are not connected to any peers on the p2p network");
				}

				throw new EventGenerationException($"Failed to create transaction. We do not have enough peers. we need a minimum of {chainConfiguration.MinimumDispatchPeerCount}");
			}

		}
	}
}