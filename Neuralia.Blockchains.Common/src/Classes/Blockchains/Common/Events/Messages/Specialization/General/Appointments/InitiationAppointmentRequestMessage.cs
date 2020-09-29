using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments {

	public interface IInitiationAppointmentRequestMessage : IAppointmentRequestMessage {

	}

	public abstract class InitiationAppointmentRequestMessage : AppointmentRequestMessage, IInitiationAppointmentRequestMessage {
		
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.INITIATION_APPOINTMENT_REQUESTED, 1, 0);
		}
	}
}