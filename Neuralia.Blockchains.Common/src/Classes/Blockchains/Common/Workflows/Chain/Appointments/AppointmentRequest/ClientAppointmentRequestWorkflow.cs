using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest {

	public interface IClientAppointmentRequestWorkflow : INetworkChainWorkflow{
		SafeArrayHandle Result { get; }
	}
	public interface IClientAppointmentRequestWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> , IClientAppointmentRequestWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>{

	}
	
	public abstract class ClientAppointmentRequestWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, APPOINTMENT_REQUEST_TRIGGER, SERVER_TRIGGER_REPLY> : ClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IClientAppointmentRequestWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where APPOINTMENT_REQUEST_TRIGGER : AppointmentRequestTrigger, new()
		where SERVER_TRIGGER_REPLY : AppointmentRequestServerReply, new(){
		
		public readonly  Enums.AppointmentRequestModes mode;
		private readonly BlockchainType                chainType;
		private readonly Guid?                         requesterId;
		private readonly int?                          requesterIndex;
		private readonly DateTime?                     appointment;
		
		public SafeArrayHandle Result { get; private set; }

		public ClientAppointmentRequestWorkflow(Guid? requesterId, int? requesterIndex, DateTime? appointment, Enums.AppointmentRequestModes mode, BlockchainType chainType, CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {
			this.mode = mode;
			this.chainType = chainType;
			this.requesterId = requesterId;
			this.requesterIndex = requesterIndex;
			this.appointment = appointment;
		}

		protected override async Task PerformWork(LockContext lockContext) {
			this.CheckShouldCancel();
			
			var messageFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.GetAppointmentRequestMessageFactory();
			var appointmentRequestTrigger = (BlockchainTriggerMessageSet<APPOINTMENT_REQUEST_TRIGGER>)messageFactory.CreateAppointmentRequestWorkflowTriggerSet(this.CorrelationId);

			appointmentRequestTrigger.Message.Mode = this.mode;
			appointmentRequestTrigger.Message.ChainType = this.chainType;
			appointmentRequestTrigger.Message.RequesterId = this.requesterId;
			appointmentRequestTrigger.Message.RequesterIndex = this.requesterIndex;
			appointmentRequestTrigger.Message.Appointment = this.appointment;
			
			if(this.mode == Enums.AppointmentRequestModes.RequestConfirmation) {
				
			}
			else if(this.mode == Enums.AppointmentRequestModes.VerificationConfirmation) {
				
			}
			else if(this.mode == Enums.AppointmentRequestModes.Context) {
				
			}
			else if(this.mode == Enums.AppointmentRequestModes.Trigger) {
				
			}

			foreach(var peerConnection in this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AllConnectionsList.ToArray()) {
				try {
					this.CentralCoordinator.Log.Verbose($"Sending peer list request to peer {peerConnection.ScopedAdjustedIp}");

					if(!this.SendMessage(peerConnection, appointmentRequestTrigger)) {
						this.CentralCoordinator.Log.Verbose($"Connection with peer  {peerConnection.ScopedAdjustedIp} was terminated");

						return;
					}

					BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverAppointmentRequest = (BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY>) this.WaitSingleNetworkMessage<SERVER_TRIGGER_REPLY, TargettedMessageSet<SERVER_TRIGGER_REPLY, IBlockchainEventsRehydrationFactory>, IBlockchainEventsRehydrationFactory>();

					if(serverAppointmentRequest.Message.Message != null && !serverAppointmentRequest.Message.Message.IsZero) {
						this.Result = serverAppointmentRequest.Message.Message;
						break;
					}
				} catch(Exception ) {
					this.CentralCoordinator.Log.Verbose($"Failed to request appointment info for peer  {peerConnection.ScopedAdjustedIp}.");
				}
			}
		}
	}
}