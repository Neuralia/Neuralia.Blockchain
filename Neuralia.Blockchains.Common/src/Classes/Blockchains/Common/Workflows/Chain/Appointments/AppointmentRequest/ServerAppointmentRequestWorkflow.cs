using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest {
	
	public interface IServerAppointmentRequestWorkflow : IServerWorkflow<IBlockchainEventsRehydrationFactory> {
	}

	public abstract class ServerAppointmentRequestWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : ServerChainWorkflow<CHAIN_SYNC_TRIGGER, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IServerAppointmentRequestWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_SYNC_TRIGGER : AppointmentRequestTrigger
		where SERVER_TRIGGER_REPLY : AppointmentRequestServerReply{


		protected override async Task PerformWork(LockContext lockContext) {
			this.CheckShouldCancel();
			
			IAppointmentRequestMessageFactory appointmentRequestMessageFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.GetAppointmentRequestMessageFactory();

			// ok, we just received a trigger, lets examine it
			BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverHandshake = (BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY>) appointmentRequestMessageFactory.CreateAppointmentRequestWorkflowTriggerServerReplySet(this.triggerMessage.Header);

			var requesterId = this.triggerMessage.Message.RequesterId;
			var requesterIndex = this.triggerMessage.Message.RequesterIndex;
			var appointment = this.triggerMessage.Message.Appointment;
			
			if(this.triggerMessage.Message.Mode == Enums.AppointmentRequestModes.RequestConfirmation && requesterId.HasValue) {
				serverHandshake.Message.Message = await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentRequestConfirmationMessage(requesterId.Value, appointment, lockContext).ConfigureAwait(false);
			}
			else if(this.triggerMessage.Message.Mode == Enums.AppointmentRequestModes.VerificationConfirmation && requesterId.HasValue && appointment.HasValue) {
				serverHandshake.Message.Message = await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentVerificationConfirmationMessage(requesterId.Value, appointment.Value, lockContext).ConfigureAwait(false);

			}
			else if(this.triggerMessage.Message.Mode == Enums.AppointmentRequestModes.Context && requesterIndex.HasValue && appointment.HasValue) {
				serverHandshake.Message.Message = await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentContextGossipMessage(requesterIndex.Value, appointment.Value, lockContext).ConfigureAwait(false);

			}
			else if(this.triggerMessage.Message.Mode == Enums.AppointmentRequestModes.Trigger && appointment.HasValue) {
				serverHandshake.Message.Message = await centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentTriggerGossipMessage(appointment.Value, lockContext).ConfigureAwait(false);
			} else {
				return;
			}

			if(!this.Send(serverHandshake)) {
				this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");
			}
		}

		protected ServerAppointmentRequestWorkflow(BlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> triggerMessage, PeerConnection peerConnectionn, CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, triggerMessage, peerConnectionn) {
		}

	}
}