using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;
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
	public abstract class GenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER,ENVELOPE_TYPE> : EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ENVELOPE_TYPE, IDehydratedTransaction, ITransaction, TransactionType>, IGenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ENVELOPE_TYPE : class, ITransactionEnvelope{
		protected readonly byte expiration;

		protected readonly string note;
		public override string GenerationWorkflowTypeName => "GenerateNewTransactionWorkflow";

		public GenerateNewTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, string note, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.note = note;
			this.expiration = expiration;
		}
		
		
		protected override async Task PreValidateContents(LockContext lockContext) {

			await base.PreValidateContents(lockContext).ConfigureAwait(false);
			

			if(this.BlockchainEvent is IRateLimitedTransaction && this.BlockchainEvent.TransactionId.Scope != 0){

				throw new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ONLY_ONE_TRANSACTION_PER_SCOPE).GenerateException();
			}
		}
		
		protected override async Task PostProcess(LockContext lockContext) {
			await base.PostProcess(lockContext).ConfigureAwait(false);
			
			//await this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertLocalTransaction(envelope, this.note, this.correlationContext, lockContext).ConfigureAwait(false);
		}

		protected override Task SignEnvelope(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PerformTransactionEnvelopeSignature(envelope, lockContext, this.expiration);
		}

		protected override async Task ExceptionOccured(Exception ex) {
			await base.ExceptionOccured(ex).ConfigureAwait(false);

			ENVELOPE_TYPE envelope = null;

			if(ex is AggregateException agex) {
				if(agex.InnerException is EventGenerationException evex2 && evex2.Envelope is ENVELOPE_TYPE envelope2) {
					envelope = envelope2;
				}
			}

			if(ex is EventGenerationException evex && evex.Envelope is ENVELOPE_TYPE envelope3) {
				envelope = envelope3;
			}

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionError(envelope?.Contents.Uuid, null), this.correlationContext);
		}

		protected override async Task PerformSanityChecks(LockContext lockContext) {
			await base.PerformSanityChecks(lockContext).ConfigureAwait(false);
			
		}

		protected DateTime GetTransactionExpiration() {
			return  ((IBlockchainTimeService)this.timeService).GetTransactionExpiration(this.envelope, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		protected override async Task Dispatch(LockContext lockContext) {

			var result = await centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchLocalTransactionAsync(envelope, correlationContext, lockContext).ConfigureAwait(false);

			if(result == ChainNetworkingProvider.DispatchedMethods.Failed) {
				throw new ApplicationException("Failed to dispatch transaction");
			}
		}

		protected override Task WorkflowCompleted(LockContext lockContext) {

			return this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertLocalTransaction(envelope, this.note, WalletTransactionHistory.TransactionStatuses.Dispatched, this.correlationContext, lockContext);
		}
		
		protected override TimeSpan GetEnvelopeExpiration() {
			return TimeSpan.FromHours(this.envelope.Expiration);
		}

		protected override WalletGenerationCache.DispatchEventTypes GetEventType() {
			return Wallet.Account.WalletGenerationCache.DispatchEventTypes.Transaction;
		}
		
		protected override string GetEventSubType() {
			return "transaction";
		}

	}
}