using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;

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
		private Guid? accountUuid;
		
		public CreatePresentationTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, CorrelationContext correlationContext, Guid? accountUuid) : base(centralCoordinator, expiration, null, correlationContext) {
			this.accountPublicationStepSet = new SystemEventGenerator.AccountPublicationStepSet();
			this.accountUuid = accountUuid;

		}

		protected override int Timeout => 60 * 10; // this can be a long process, 10 minutes might be required.

		protected override async Task PreTransaction(LockContext lockContext) {
			this.transaction = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationTransaction(this.accountPublicationStepSet, this.correlationContext, this.accountUuid, lockContext, this.expiration).ConfigureAwait(false);

		}

		protected override Task<ITransactionEnvelope> AssembleEvent(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationEnvelope(this.PresentationTransaction, this.accountPublicationStepSet, this.correlationContext, lockContext, this.expiration);
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

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationStarted, this.correlationContext);

			try {
				await base.PerformWork(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
			}catch(Exception ex) {

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationError, this.correlationContext);

				throw;
			}
			
			finally {
				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, this.correlationContext);
			}
		}

		protected override Task CheckSyncStatus(LockContext lockContext) {
			//  no need to be synced for a presentation transaction
			return Task.CompletedTask;
		}

		protected override async Task ExceptionOccured(Exception ex) {
			await base.ExceptionOccured(ex).ConfigureAwait(false);

			if(ex is EventGenerationException evex && evex.Envelope is ITransactionEnvelope envelope) {
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.CreateErrorMessage(BlockchainSystemEventTypes.Instance.AccountPublicationError, ex.Message), this.correlationContext);
			}
		}

		protected override async Task CheckAccounyStatus(LockContext lockContext) {
			// now we ensure our account is not presented or repsenting
			Enums.PublicationStatus accountStatus = (await centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

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

		protected override async Task EventGenerationCompleted(ITransactionEnvelope envelope, LockContext lockContext) {

			await base.EventGenerationCompleted(envelope, lockContext).ConfigureAwait(false);

			//ok, now we mark this account as in process of being published

			// now we publish our keys
			IWalletAccount account = await centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			account.Status = Enums.PublicationStatus.Dispatched;
			account.PresentationTransactionId = envelope.Contents.Uuid;
			account.PresentationTransactionTimeout = this.GetTransactionExpiration();

			await centralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet(lockContext).ConfigureAwait(false);
		}
	}
}