using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry {
	public interface IAppointmentRequesterResult {

		int Id { get; set; }

		int Index { get; set; }
		DateTime Appointment { get; set; }
		DateTime? RequestedCode { get; set; }
		byte[] ValidatorCode { get; set; }
		int SecretCode { get; set; }
		
		DateTime? Trigger { get; set; }
		DateTime? Completed { get; set; }
		
		byte[] Results { get; set; }

		bool Valid { get; set; }
		bool Sent { get; set; }
	}
}