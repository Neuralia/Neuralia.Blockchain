using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions {
	public interface ICreatePresentationTransactionWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public class CreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER> : GenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER>, ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet;
		private IStandardPresentationTransaction PresentationTransaction => (IStandardPresentationTransaction)this.transaction;
		private Guid? accountUuId;
		
		public CreatePresentationTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, CorrelationContext correlationContext, Guid? accountUuId) : base(centralCoordinator, expiration, null, correlationContext) {
			this.accountPublicationStepSet = new SystemEventGenerator.AccountPublicationStepSet();
			this.accountUuId = accountUuId;

		}

		protected override int Timeout => 60 * 10; // this can be a long process, 10 minutes might be required.

		protected override void PreTransaction() {
			this.transaction = this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationTransaction(this.accountPublicationStepSet, this.correlationContext, this.accountUuId, this.expiration);

		}

		protected override ITransactionEnvelope AssembleEvent() {
			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationEnvelope(this.PresentationTransaction, this.accountPublicationStepSet, this.correlationContext, this.expiration);
		}
		
		protected override void ProcessEnvelope(ITransactionEnvelope envelope) {
			// we already have the transction, no need to do anything here.
		}

		protected override ValidationResult ValidateContents(ITransactionEnvelope envelope) {
			
			ValidationResult result = base.ValidateContents(envelope);

			if(result.Invalid) {
				return result;
			}

			if(envelope.Contents.Uuid.Scope != 0) {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ONLY_ONE_TRANSACTION_PER_SCOPE);
			}

			return new ValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationStarted, this.correlationContext);

			try {
				base.PerformWork(workflow, taskRoutingContext);
			}catch(Exception ex) {

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationError, this.correlationContext);

				throw;
			}
			
			finally {
				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, this.correlationContext);
			}
		}

		protected override void CheckSyncStatus() {
			//  no need to be synced for a presentation transaction
		}

		protected override void ExceptionOccured(Exception ex) {
			base.ExceptionOccured(ex);

			if(ex is EventGenerationException evex && evex.Envelope is ITransactionEnvelope envelope) {
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.CreateErrorMessage(BlockchainSystemEventTypes.Instance.AccountPublicationError, ex.Message), this.correlationContext);
			}
		}

		protected override void CheckAccounyStatus() {
			// now we ensure our account is not presented or repsenting
			Enums.PublicationStatus accountStatus = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount().Status;

			if(accountStatus == Enums.PublicationStatus.Published) {
				throw new EventGenerationException("The account has already been published and cannot be published again");
			}

			if(accountStatus == Enums.PublicationStatus.Dispatched) {
				throw new EventGenerationException("The account has already been dispatched and cannot be published again");
			}

			if(accountStatus == Enums.PublicationStatus.Rejected) {
				throw new EventGenerationException("The account has already been rejected and cannot be published again");
			}
		}

		protected override void EventGenerationCompleted(ITransactionEnvelope envelope) {

			base.EventGenerationCompleted(envelope);

			//ok, now we mark this account as in process of being published

			// now we publish our keys
			IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();

			account.Status = Enums.PublicationStatus.Dispatched;
			account.PresentationTransactionId = envelope.Contents.Uuid;
			account.PresentationTransactionTimeout = this.GetTransactionExpiration();

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet();
		}
	}
}