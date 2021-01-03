using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries {
	public interface IAppointmentValidatorSession {

		DateTime Appointment { get; set; }

		int Window { get; set; }
		
		int RequesterCount  { get; set; }

		Guid ValidatorHash { get; set; }

		byte[] Indices { get; set; }
		
		byte[] SecretCodes { get; set; }
		
		DateTime Dispatch { get; set; }
	}
}