using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions {
	public interface ICreatePresentationTransactionWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public class CreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : GenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, IPresentationTransactionEnvelope>, ICreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet;
		private readonly string accountCode;
		private Enums.OperationStatus startingMode;
		private AccountCanPublishAPI publishInfo;
		
		public CreatePresentationTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, CorrelationContext correlationContext, string accountCode) : base(centralCoordinator, expiration, null, correlationContext) {
			this.accountPublicationStepSet = new SystemEventGenerator.AccountPublicationStepSet();
			this.accountCode = accountCode;
		}

		protected const string POW_SIGNATURE_ENVELOPE_TASK_NAME = "pow";

		private IStandardPresentationTransaction PresentationTransaction => (IStandardPresentationTransaction) this.BlockchainEvent;

		protected override int Timeout => 60 * 10; // this can be a long process, 10 minutes might be required.

		protected override async Task PreProcess(LockContext lockContext) {
			this.startingMode = this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode;
			this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.Presenting;
		}

		protected override Task Finally(LockContext lockContext) {
			this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = this.exception==null? Enums.OperationStatus.None: this.startingMode;
			return base.Finally(lockContext);
		}

		protected override async Task<IPresentationTransactionEnvelope> AssembleEvent(LockContext lockContext) {
			var presentationTransaction = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationTransaction(this.accountPublicationStepSet, this.correlationContext, this.accountCode, lockContext, null).ConfigureAwait(false);
			return await centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationEnvelope(presentationTransaction, accountPublicationStepSet, publishInfo, correlationContext, lockContext).ConfigureAwait(false);
		}

		protected override void AddTaskProcessEnvelope() {
			
			base.AddTaskProcessEnvelope();

			this.AddTaskPOWSignatureEnvelope();
		}
		
		protected override void AddTaskWorkflowCompleted() {
			this.AddWalletTransactionTask(WORKFLOW_COMPLETED_TASK_NAME,this.WorkflowCompleted);
		}
		
		protected virtual void AddTaskPOWSignatureEnvelope() {
			this.AddSingleTask(POW_SIGNATURE_ENVELOPE_TASK_NAME,this.PerformPOWSignature, new []{new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = lc => {

					this.WalletGenerationCache.SubStep = "processing";
					return Task.CompletedTask;
				}
			}}, new []{new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = lc => {

				this.WalletGenerationCache.SubStep = "completed";
				return Task.CompletedTask;
			}}, new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.UpdateCache}});
		}
		
		protected override async Task PreValidateContents(LockContext lockContext) {

			await base.PreValidateContents(lockContext).ConfigureAwait(false);
			
			if(this.PresentationTransaction.AccountType == Enums.AccountTypes.User && (!envelope.ConfirmationCode.HasValue || envelope.ConfirmationCode.Value == 0)) {
				throw new EventGenerationException("A user account presentation must have a valid confirmation code");
			}
			
			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(this.PresentationTransaction.AccountType == Enums.AccountTypes.Server && account.AccountAppointment != null && account.AccountAppointment.AppointmentStatus != Enums.AppointmentStatus.None && ( !envelope.ConfirmationCode.HasValue || envelope.ConfirmationCode.Value == 0)) {
				throw new EventGenerationException("A Server account presentation must have a valid confirmation code if it was part of an appointment");
			}
		}
		
		protected override async Task ValidateContents(LockContext lockContext) {

			var result = new ValidationResult(ValidationResult.ValidationResults.Invalid);
			try {
				// here we do a check without the POW. too long for a wallet transaction
				result = await centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.DisablePOW(async (provider, lc) => {
					var result2 = new ValidationResult(ValidationResult.ValidationResults.Invalid);
					await provider.ValidateEnvelopedContent(this.envelope, false, validationResult => {
						result2 = validationResult;
					}, lc, ChainValidationProvider.ValidationModes.Self).ConfigureAwait(false);

					return result2;
				}, lockContext).ConfigureAwait(false);
				
				if(result.Invalid) {

					throw result.GenerateException();
				}

			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to validate event");

				throw;
			}
		}

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationStarted, this.correlationContext);

			try {
				await base.PerformWork(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
			} catch(Exception ex) {

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationError, this.correlationContext);

				throw;
			} finally {
				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, this.correlationContext);
			}
		}

		protected override Task CheckSyncStatus(LockContext lockContext) {
			//  no need to be synced for a presentation transaction
			return Task.CompletedTask;
		}

		protected override async Task ExceptionOccured(Exception ex) {
			await base.ExceptionOccured(ex).ConfigureAwait(false);

			if(ex is EventGenerationException evex && evex.Envelope is IPresentationTransactionEnvelope envelope) {
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.CreateErrorMessage(BlockchainSystemEventTypes.Instance.AccountPublicationError, ex.Message), this.correlationContext);
			}
		}

		protected override async Task CheckAccountStatus(LockContext lockContext) {
			// now we ensure our account is not presented or repsenting

			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
			Enums.PublicationStatus accountStatus = account.Status;

			if(accountStatus == Enums.PublicationStatus.Published) {
				throw new EventGenerationException("The account has already been published and cannot be published again");
			}

			if(accountStatus == Enums.PublicationStatus.Dispatched) {
				throw new EventGenerationException("The account has already been dispatched and cannot be published again");
			}

			if(accountStatus == Enums.PublicationStatus.Rejected) {
				throw new EventGenerationException("The account has already been rejected and cannot be published again");
			}

			this.publishInfo = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.APICanPublishAccount(account.AccountCode, lockContext).ConfigureAwait(false);

			if(!publishInfo.CanPublish) {
				throw new EventGenerationException("The account can not be published; no usable publish target");
			}
		}

		protected async Task PerformPOWSignature(LockContext lockContext) {
			
			if(this.PresentationTransaction.AccountType == Enums.AccountTypes.Server && (!envelope.ConfirmationCode.HasValue || envelope.ConfirmationCode.Value == 0)) {
				// ok, we have a server account and node confirmation code, it is time to prepare our POW signature
				await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PerformPowSignature(envelope).ConfigureAwait(false);
			}
		}

		protected virtual async Task WorkflowCompleted(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {

			await base.WorkflowCompleted(lockContext).ConfigureAwait(false);
			try {

				//ok, now we mark this account as in process of being published
		
				// now we publish our keys
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
		
				account.Status = Enums.PublicationStatus.Dispatched;
				account.PresentationTransactionId = envelope.Contents.Uuid;
				account.PresentationTransactionTimeout = this.GetTransactionExpiration();

				//
				// this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.Presenting;
				
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, this.correlationContext);
				NLog.Default.Information("Generation of presentation transaction completed");

			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to dispatch presentation transaction");
			}
		}

		protected override string GetEventSubType() {
			return "presentation";
		}
		
		protected override Task<IWalletGenerationCache> LoadExistingGenerationCacheEntry(LockContext lockContext) {

			if(this.WalletGenerationCache != null) {
				return Task.FromResult(this.WalletGenerationCache);
			}
			return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetGenerationCacheEntry(this.GetEventType(), this.GetEventSubType(),lockContext);
		}
	}
}