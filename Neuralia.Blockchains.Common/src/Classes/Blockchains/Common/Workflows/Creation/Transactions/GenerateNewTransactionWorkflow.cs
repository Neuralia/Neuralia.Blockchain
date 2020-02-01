using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
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
		protected ITransaction transaction;
		protected readonly byte expiration;
		public GenerateNewTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, string note, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.note = note;
			this.expiration = expiration;
		}

		protected override void EventGenerationCompleted(ITransactionEnvelope envelope) {
			
			this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertLocalTransaction(envelope, this.note, this.correlationContext);
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {
			
			try {
				base.PerformWork(workflow, taskRoutingContext);
				
				Log.Information("Insertion of transaction into blockchain completed");
			} catch(Exception ex) {
				string message = "Failed to insert transaction into blockchain";
				Log.Error(ex, message);
				
				throw;
			}
			
		}

		protected override void ProcessEnvelope(ITransactionEnvelope envelope) {
			this.transaction = envelope.Contents.RehydratedTransaction;
		}

		protected override void ValidationFailed(ITransactionEnvelope envelope, ValidationResult results) {
			base.ValidationFailed(envelope, results);
		}

		protected override void ExceptionOccured(Exception ex) {
			base.ExceptionOccured(ex);

			ITransactionEnvelope envelope = null;

			if(ex is AggregateException agex) {
				if(agex.InnerException is EventGenerationException evex2 && evex2.Envelope is ITransactionEnvelope envelope2) {
					envelope = envelope2;
				}
			}
			if(ex is EventGenerationException evex && evex.Envelope is ITransactionEnvelope envelope3) {
				envelope = envelope3;
			}
			
			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.TransactionError(envelope?.Contents.Uuid, null), this.correlationContext);
		}

		protected override void PerformSanityChecks() {
			base.PerformSanityChecks();

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(!this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.MinimumDispatchPeerCountAchieved) {

				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					throw new EventGenerationException("Failed to create transaction. We are not connected to any peers on the p2p network");
				}

				throw new EventGenerationException($"Failed to create transaction. We do not have enough peers. we need a minimum of {chainConfiguration.MinimumDispatchPeerCount}");
			}

		}

		protected DateTime GetTransactionExpiration() {
			return this.envelope.GetExpirationTime(this.centralCoordinator.BlockchainServiceSet.TimeService, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}
	}
}