using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments {

	
	public interface ISendAppointmentRequestMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewSignedMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     Prepare and dispatch a miner registration message on the blockchain as a gossip message
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class SendAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : GenerateNewSignedMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISendAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected const string CONFIRM_CHANGE_WALLET_TASK_NAME = "confirm_change";

		private readonly int preferredRegion;
		public SendAppointmentRequestMessageWorkflow(int preferredRegion, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.preferredRegion = preferredRegion;
		}
		
		protected override void AddTaskDispatch() {
			base.AddTaskDispatch();
			
			this.AddWalletTransactionTask(CONFIRM_CHANGE_WALLET_TASK_NAME,this.ConfirmChange, null, null, async () => {
				
				await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.UpdateOperatingMode(null).ConfigureAwait(false);
				
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, this.correlationContext);
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentRequestSent, this.correlationContext);

			});
		}
		
		protected override async Task PreProcess(LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);
			
			var account  = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			account.AccountAppointment = new WalletAccount.AccountAppointmentDetails();
		}
		
		protected override async Task CheckAccountStatus(LockContext lockContext) {
			
			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
			Enums.PublicationStatus accountStatus = account.Status;

			if(accountStatus == Enums.PublicationStatus.New) {
				throw new EventGenerationException("The account is new and cannot send an appointment request.");
			}
			
			if(accountStatus == Enums.PublicationStatus.Rejected) {
				throw new EventGenerationException("The account is rejected and cannot send an appointment request.");
			}
			
			if(accountStatus == Enums.PublicationStatus.Dispatched) {
				throw new EventGenerationException("The account is dispatched and cannot send an appointment request.");
			}
			
			if(this.PreDispatch == false && account.AccountAppointment != null) {
				if(AppointmentUtils.AppointmentVerificationExpired(account.AccountAppointment)) {
					
					// in these cases, we allow a reset.
					account.AccountAppointment = null;
					this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.None;
				} else {
					throw new EventGenerationException("The account is in the process of an appointment and cannot send an appointment request.");
				}
			}
		}
		
		protected override Task<ISignedMessageEnvelope> AssembleEvent(LockContext lockContext) {

			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateAppointmentRequestMessage(this.preferredRegion, lockContext);
		}

		protected override ChainNetworkingProvider.MessageDispatchTypes MessageDispatchType => ChainNetworkingProvider.MessageDispatchTypes.AppointmentRequest;

		protected async Task ConfirmChange(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {

			this.CentralCoordinator.Log.Information("Dispatch of appointment blockchain message completed");

			var appointmentRequestMessage = (IAppointmentRequestMessage) envelope.Contents.RehydratedEvent;

			var account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(account.AccountAppointment == null) {
				account.AccountAppointment = new WalletAccount.AccountAppointmentDetails();
			}
			
			account.AccountAppointment.RequesterId = appointmentRequestMessage.RequesterId;
			account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentRequested;
			account.AccountAppointment.Region = this.preferredRegion;
			account.AccountAppointment.AppointmentRequestTimeStamp = DateTimeEx.CurrentTime;
			this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentMode = account.AccountAppointment.AppointmentStatus;
		}

		protected override async Task ExceptionOccured(Exception ex)
		{
			await base.ExceptionOccured(ex).ConfigureAwait(false);

			this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentRequestFailed, this.correlationContext);
		}
	}
}