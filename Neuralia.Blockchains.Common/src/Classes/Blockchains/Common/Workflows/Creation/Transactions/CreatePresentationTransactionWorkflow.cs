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
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

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
		private bool dispatchUseWeb;
		private bool dispatchUseGossip;
		
		protected const string PREPROCESS_WALLET_TASK_NAME = "preprocess_wallet";

		public CreatePresentationTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, CorrelationContext correlationContext, string accountCode) : base(centralCoordinator, expiration, null, correlationContext) {
			this.accountPublicationStepSet = new SystemEventGenerator.AccountPublicationStepSet();
			this.accountCode = accountCode;
			
			this.ExecutionMode = Workflow.ExecutingMode.Single;
			
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			this.dispatchUseWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			this.dispatchUseGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
		}
		
		protected const string THS_SIGNATURE_ENVELOPE_TASK_NAME = "ths";

		private IStandardPresentationTransaction PresentationTransaction => (IStandardPresentationTransaction) this.BlockchainEvent;

		protected override int Timeout => 60 * 10; // this can be a long process, 10 minutes might be required.

		protected override Task PreProcess(LockContext lockContext) {
			this.startingMode = this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode;
			this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.Presenting;
			
			return Task.CompletedTask;
		}

		protected override Task Finally(LockContext lockContext) {
			this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = this.exception==null? Enums.OperationStatus.None: this.startingMode;
			return base.Finally(lockContext);
		}

		protected override async Task<IPresentationTransactionEnvelope> AssembleEvent(LockContext lockContext) {
			var presentationTransaction = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationTransaction(this.accountPublicationStepSet, this.correlationContext, this.accountCode, lockContext, null).ConfigureAwait(false);
			return await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GeneratePresentationEnvelope(presentationTransaction, this.accountPublicationStepSet, this.publishInfo, this.correlationContext, lockContext).ConfigureAwait(false);
		}

		protected override void AddTaskProcessEnvelope() {
			
			base.AddTaskProcessEnvelope();

			this.AddWalletTransactionTask(PREPROCESS_WALLET_TASK_NAME, this.PreProcessWallet, null, null);
			
			this.AddTaskTHSSignatureEnvelope();
		}

		protected override void AddTaskWorkflowCompleted() {
			this.AddWalletTransactionTask(WORKFLOW_COMPLETED_TASK_NAME,this.WorkflowCompleted);
		}
		
		protected virtual void AddTaskTHSSignatureEnvelope() {
			this.AddSingleTask(THS_SIGNATURE_ENVELOPE_TASK_NAME,this.PerformTHSSignatureTask, new []{new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = lc => {

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
			
			if(this.PresentationTransaction.AccountType == Enums.AccountTypes.User && (!this.envelope.ConfirmationCode.HasValue || this.envelope.ConfirmationCode.Value == 0)) {
				throw new EventGenerationException("A user account presentation must have a valid confirmation code");
			}
			
			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(this.PresentationTransaction.AccountType == Enums.AccountTypes.Server && account.AccountAppointment != null && account.AccountAppointment.AppointmentStatus != Enums.AppointmentStatus.None && ( !this.envelope.ConfirmationCode.HasValue || this.envelope.ConfirmationCode.Value == 0)) {
				throw new EventGenerationException("A Server account presentation must have a valid confirmation code if it was part of an appointment");
			}
		}
		
		protected override async Task ValidateContents(LockContext lockContext) {
			
			Func<IChainValidationProvider, LockContext, Task<ValidationResult>> verificationCallback = async (provider, lc) => {
				var result2 = new ValidationResult(ValidationResult.ValidationResults.Invalid);

				await provider.ValidateEnvelopedContent(this.envelope, false, validationResult => {
					result2 = validationResult;
				}, lc, ChainValidationProvider.ValidationModes.Self).ConfigureAwait(false);

				return result2;
			};
			
			var result = new ValidationResult(ValidationResult.ValidationResults.Invalid);
			
			try {
				
				if(this.SkipTHS) {
					result = await this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.DisableTHS(verificationCallback, lockContext).ConfigureAwait(false);
				} else {
					result = await verificationCallback(this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase, lockContext).ConfigureAwait(false);
				}
				
				// here we do a check without the THS. too long for a wallet transaction
				if(result.Invalid) {

					throw result.GenerateException();
				}

			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to validate event");

				throw;
			}
		}

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			await centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationStarted, correlationContext).ConfigureAwait(false);

			try {
				await base.PerformWork(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
			} catch(Exception ex) {

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationError, this.correlationContext).ConfigureAwait(false);

				throw;
			} finally {
				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, this.correlationContext).ConfigureAwait(false);
			}
		}

		protected virtual async Task PreProcessWallet(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
		
			// lets make sure to mark the wallet that it is in the process of dispatching, so it doesnt restart
			account.Status = Enums.PublicationStatus.Dispatching;
		}

		protected override Task CheckSyncStatus(LockContext lockContext) {
			//  no need to be synced for a presentation transaction
			return Task.CompletedTask;
		}

		protected override async Task ExceptionOccured(Exception ex) {
			await base.ExceptionOccured(ex).ConfigureAwait(false);

			if(ex is EventGenerationException evex && evex.Envelope is IPresentationTransactionEnvelope envelope) {
				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.CreateErrorMessage(BlockchainSystemEventTypes.Instance.AccountPublicationError, ex.Message), this.correlationContext).ConfigureAwait(false);
			}
		}

		protected override async Task CheckAccountStatus(LockContext lockContext) {
			// now we ensure our account is not presented or repsenting

			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
			Enums.PublicationStatus accountStatus = account.Status;

			if(accountStatus == Enums.PublicationStatus.Dispatching) {
				// ok, this is a retry so we can continue
				return;
			}

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

			if(!this.publishInfo.CanPublish) {
				
				throw new EventGenerationException("The account can not be published; no usable publish target");
			}
		}

		private bool SkipTHS => this.PresentationTransaction.AccountType != Enums.AccountTypes.Server && (this.PresentationTransaction.AccountType == Enums.AccountTypes.User && (this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableWebRegUserPresentationTHS && this.dispatchUseWeb));
		protected Task PerformTHSSignatureTask(LockContext lockContext) {

			if(!this.SkipTHS) {
				return this.PerformTHSSignature(lockContext);
			} 
			
			return Task.CompletedTask;
		}

		protected Task PerformTHSSignature(LockContext lockContext) {

			//ok, we have a server account and node confirmation code, it is time to prepare our THS signature
			THSRulesSetDescriptor descriptor = THSRulesSet.PresentationDefaultRulesSetDescriptor;
			
			if(this.PresentationTransaction.IsServer) {
				descriptor = THSRulesSet.ServerPresentationDefaultRulesSetDescriptor;
			}

			if(TestingUtil.Testing) {
				descriptor = THSRulesSet.TestRulesetDescriptor;
			}

			ClosureWrapper<DateTime> lastUpdate = DateTime.Now;
			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PerformTHSSignature(this.envelope, this.CancelToken, descriptor, async (currentNonce, currentRound) => {
				
				if(lastUpdate.Value.AddMinutes(3) < DateTime.Now) {
					// lets update our expiration markers
					try {
						await centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token, lc) => {

							// lets update our expiration notice
							return UpdateGenerationCacheTimeouts(lc);
						}, lockContext, Timeout).ConfigureAwait(false);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Debug(ex, "error while perform THS");
					}

					lastUpdate.Value = DateTime.Now;
				}
			});
		}
		
		protected override void SetEntryCacheTimeouts(LockContext lockContext) {
			this.WalletGenerationCache.NextRetry = DateTimeEx.CurrentTime.AddMinutes(10);
			this.WalletGenerationCache.Expiration = DateTimeEx.CurrentTime + this.GetEnvelopeExpiration() + TimeSpan.FromDays(1);
		}
		
		protected override async Task Dispatch(LockContext lockContext) {
			
			var result = await this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchLocalTransactionAsync(this.envelope, this.correlationContext, lockContext, false).ConfigureAwait(false);

			bool sent = false;

			if(result != ChainNetworkingProvider.DispatchedMethods.Failed) {
				sent = true;
			}
			else if((!this.dispatchUseWeb && this.dispatchUseGossip) || (this.dispatchUseWeb && this.dispatchUseGossip)) {
				// ok, we will try with gossip, but this requires a lazy loaded THS

				if(this.envelope.THSEnvelopeSignature.Solution.IsEmpty) {
					//TODO: we should save the updated message so we dont have to resign in case of error.
					await this.PerformTHSSignature(lockContext).ConfigureAwait(false);

					// now verify

					var verificationResult = new ValidationResult(ValidationResult.ValidationResults.Invalid);

					await this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(this.envelope, false, validationResult => {
						verificationResult = validationResult;
					}, lockContext, ChainValidationProvider.ValidationModes.Self).ConfigureAwait(false);

					if(verificationResult.Invalid) {
						throw verificationResult.GenerateException();
					}
				}

				result = await this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchLocalTransactionAsync(this.envelope, this.correlationContext, lockContext, true).ConfigureAwait(false);

				if (result != ChainNetworkingProvider.DispatchedMethods.Failed) {
					sent = true;
				}
			}
			
			if(!sent) {
				throw new ApplicationException("Failed to dispatch transaction");
			}
		}
		
		protected virtual async Task WorkflowCompleted(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {

			await base.WorkflowCompleted(lockContext).ConfigureAwait(false);
			try {

				//ok, now we mark this account as in process of being published
		
				// now we publish our keys
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
		
				account.Status = Enums.PublicationStatus.Dispatched;
				account.PresentationTransactionId = this.envelope.Contents.Uuid;
				account.PresentationTransactionTimeout = this.GetTransactionExpiration();

				//
				// this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.Presenting;
				
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, this.correlationContext);
				this.CentralCoordinator.Log.Information("Generation of presentation transaction completed");

			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to dispatch presentation transaction");
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