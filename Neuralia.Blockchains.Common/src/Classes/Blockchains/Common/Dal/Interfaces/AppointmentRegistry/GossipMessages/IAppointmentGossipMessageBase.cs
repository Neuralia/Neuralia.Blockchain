using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages {
	public interface IAppointmentGossipMessageBase {
		DateTime Appointment { get; set; }

		Guid MessageUuid { get; set; }
	}
}