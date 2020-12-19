using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries {
	public interface IAppointmentPeerEntryBase {
		Guid RequesterId { get; set; }
		DateTime Appointment { get; set; }
		Guid MessageUuid { get; set; }
	}
}