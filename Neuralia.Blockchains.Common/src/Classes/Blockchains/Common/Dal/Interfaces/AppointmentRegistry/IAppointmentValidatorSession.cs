using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries {
	public interface IAppointmentValidatorSession {

		DateTime Appointment { get; set; }

		int Window { get; set; }

		Guid ValidatorHash { get; set; }

		string Indices { get; set; }
		
		string SecretCodes { get; set; }
		
		DateTime Dispatch { get; set; }
	}
}