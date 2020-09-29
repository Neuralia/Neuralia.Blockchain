using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments {

	
	public interface ISendInitiationAppointmentRequestMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, IInitiationAppointmentMessageEnvelope>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This is the base class for all transaction generating workflows
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public abstract class SendInitiationAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : GenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, IInitiationAppointmentMessageEnvelope>, ISendInitiationAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>{

		protected const string CONFIRM_CHANGE_WALLET_TASK_NAME = "confirm_change";
		protected const string AUTHENTICATION_SIGNATURE_TASK_NAME = "authentication_signature";
		protected const string POW_SIGNATURE_ENVELOPE_TASK_NAME = "pow";

		protected SafeArrayHandle keyBytes;
		protected SafeArrayHandle publicKey;
		
		private InitiationAppointmentRequestMessage message;
		private readonly int preferredRegion;
		public SendInitiationAppointmentRequestMessageWorkflow(int preferredRegion, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.preferredRegion = preferredRegion;
		}
		
		protected override void AddTaskProcessEnvelope() {
			
			base.AddTaskProcessEnvelope();
			
			this.AddTaskPOWSignatureEnvelope();
		}
		
		protected override void AddTaskAssembleEvent() {
			
			this.AddTaskGenerateIdentitySignatureKey();
			
			base.AddTaskAssembleEvent();
		}
		
		protected override void AddTaskDispatch() {
			base.AddTaskDispatch();
			
			this.AddWalletTransactionTask(CONFIRM_CHANGE_WALLET_TASK_NAME,this.ConfirmChange, null, null, async () => {
				
				await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.UpdateOperatingMode(null).ConfigureAwait(false);
				
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, this.correlationContext);
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentRequestSent, this.correlationContext);

			});
		}
		
		
		protected override async Task<IInitiationAppointmentMessageEnvelope> AssembleEvent(LockContext lockContext) {

			var envelope = await centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateInitiationAppointmentRequestMessage(preferredRegion, this.publicKey, lockContext).ConfigureAwait(false);
			this.message = envelope.Contents.RehydratedEvent as InitiationAppointmentRequestMessage;
			return envelope;
		}

		protected override async Task PreProcess(LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);
			
			var account  = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			account.AccountAppointment = new WalletAccount.AccountAppointmentDetails();
		}


		protected override async Task CheckAccountStatus(LockContext lockContext) {
			
			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
			Enums.PublicationStatus accountStatus = account.Status;
			
			if(accountStatus != Enums.PublicationStatus.New) {
				throw new EventGenerationException("The account is not new and cannot send an initiation appointment request.");
			}
			
			if(this.PreDispatch == false && account.AccountAppointment != null) {
				if((account.AccountAppointment.AppointmentConfirmationCodeExpiration.HasValue && account.AccountAppointment.AppointmentConfirmationCodeExpiration.Value < DateTimeEx.CurrentTime) ||
				   (account.AccountAppointment.AppointmentVerificationTime.HasValue && account.AccountAppointment.AppointmentVerificationTime.Value < DateTimeEx.CurrentTime && !account.AccountAppointment.AppointmentConfirmationCode.HasValue)) {
					
					// in these cases, we allow a reset.
					account.AccountAppointment = null;
				} else {
					throw new EventGenerationException("The account is in the process of an appointment and cannot send an appointment request.");
				}
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
		
		protected virtual void AddTaskPOWSignatureEnvelope() {
			this.AddSingleTask(POW_SIGNATURE_ENVELOPE_TASK_NAME,this.PerformPOWSignature, new []{new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.UpdateCache, ActionCallback = lc => {

					this.WalletGenerationCache.SubStep = "processing";
					return Task.CompletedTask;
				}
			}}, new []{new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.SetEntry}, new WorkflowTask.ActionSet(){ActionType = WorkflowTask.ActionSet.ActionTypes.UpdateCache, ActionCallback = lc => {

				this.WalletGenerationCache.SubStep = "completed";
				return Task.CompletedTask;
			}}});
		}
		
		protected async Task PerformPOWSignature(LockContext lockContext) {
			
			// time to prepare our POW signature
			await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PerformPowSignature(envelope).ConfigureAwait(false);
		}

		protected virtual void AddTaskGenerateIdentitySignatureKey() {
			this.AddSingleTask(AUTHENTICATION_SIGNATURE_TASK_NAME, this.PerformIdentitySignatureKeyGeneration);
		}

		/// <summary>
		/// In order to prevent session hijacking, we need to create a key we can later use to sign the presentation message and demonstrate that we are the original sender, and not a hijacker.
		/// </summary>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		protected async Task PerformIdentitySignatureKeyGeneration(LockContext lockContext) {

			IXmssWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CreateXmssKey("identify", AppointmentUtils.IdentityKeyHeight, AppointmentUtils.IdentityKeyHash, AppointmentUtils.IdentityBackupKeyHash, 0.8f, 0.9f, pct => {
				NLog.Default.Verbose($"Creating identifying key: {pct*100}%");
				
				return Task.CompletedTask;
				
			}).ConfigureAwait(false);

			this.publicKey = key.PublicKey.Clone();
			using var dehydrator = DataSerializationFactory.CreateDehydrator();
			key.Dehydrate(dehydrator);

			this.keyBytes = (SafeArrayHandle)dehydrator.ToArray().Release();
		}
		
		
		protected async Task ConfirmChange(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {

			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(account.AccountAppointment == null) {
				account.AccountAppointment = new WalletAccount.AccountAppointmentDetails();
			}
			
			account.AccountAppointment.RequesterId = this.message.RequesterId;
			account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentRequested;
			account.AccountAppointment.AppointmentRequestTimeStamp = DateTimeEx.CurrentTime;
			account.AccountAppointment.IdentitySignatureKey = keyBytes.ToExactByteArrayCopy();
			NLog.Default.Information("Generation of initiation appointment blockchain message completed");
			
		}

		protected override ChainNetworkingProvider.MessageDispatchTypes MessageDispatchType => ChainNetworkingProvider.MessageDispatchTypes.AppointmentInitiationRequest;

		protected override async Task ExceptionOccured(Exception ex) {
			await base.ExceptionOccured(ex).ConfigureAwait(false);
			
			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

				var account  = await provider.GetActiveAccount(lc).ConfigureAwait(false);

				account.AccountAppointment = null;
				
			}, null, this.Timeout).ConfigureAwait(false);
		}
	}
}