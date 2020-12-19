using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry {
	public interface IAppointmentRequesterResult {

		int Id { get; set; }

		int Index { get; set; }
		DateTime Appointment { get; set; }
		DateTime? RequestedCodeCompleted { get; set; }
		byte[] ValidatorCode { get; set; }
		int SecretCode { get; set; }
		
		DateTime? TriggerCompleted { get; set; }
		DateTime? PuzzleCompleted { get; set; }
		byte[] PuzzleResults { get; set; }
		
		DateTime? THSCompleted { get; set; }
		byte[] THSResults { get; set; }
		
		

		bool Valid { get; set; }
		bool Sent { get; set; }
	}
}