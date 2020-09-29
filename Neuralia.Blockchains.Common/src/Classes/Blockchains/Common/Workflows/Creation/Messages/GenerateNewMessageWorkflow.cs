using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages {

	public interface IGenerateNewMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ENVELOPE_TYPE> : IEventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where ENVELOPE_TYPE : class, IMessageEnvelope{
	}

	/// <summary>
	///     This is the base class for all transaction generating workflows
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public abstract class GenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER,ENVELOPE_TYPE> : EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ENVELOPE_TYPE, IDehydratedBlockchainMessage, IBlockchainMessage, BlockchainMessageType>, IGenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER,ENVELOPE_TYPE>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ENVELOPE_TYPE : class, IMessageEnvelope {
		
		
		public GenerateNewMessageWorkflow(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {

		}

		
		protected virtual ChainNetworkingProvider.MessageDispatchTypes MessageDispatchType => ChainNetworkingProvider.MessageDispatchTypes.GeneralMessage;
		
		protected override Task Dispatch(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchNewMessage(this.envelope, this.correlationContext, this.MessageDispatchType);
		}
		
		protected override void ValidationFailed(ENVELOPE_TYPE envelope, ValidationResult results) {
			base.ValidationFailed(envelope, results);

			//TODO: any error message for blockchain messages?
			// if(results is TransactionValidationResult transactionValidationResult) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, transactionValidationResult.ErrorCodes), this.correlationContext);
			// }
		}

		protected override Task ExceptionOccured(Exception ex) {
			return base.ExceptionOccured(ex);

			// if(ex is EventGenerationException evex && evex.Envelope is ENVELOPE_TYPE envelope) {
			// 	this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope.Contents.Uuid, null), this.correlationContext);
			// }
		}

		protected override async Task PerformSanityChecks(LockContext lockContext) {
			await base.PerformSanityChecks(lockContext).ConfigureAwait(false);

			
		}
		protected override Task SignEnvelope(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {
			return Task.CompletedTask;
		}

		protected override TimeSpan GetEnvelopeExpiration() {
			return TimeSpan.FromHours(3);
		}
		
		protected override WalletGenerationCache.DispatchEventTypes GetEventType() {
			return Wallet.Account.WalletGenerationCache.DispatchEventTypes.Message;
		}
		
		protected override string GetEventSubType() {
			return "message";
		}
	}
}