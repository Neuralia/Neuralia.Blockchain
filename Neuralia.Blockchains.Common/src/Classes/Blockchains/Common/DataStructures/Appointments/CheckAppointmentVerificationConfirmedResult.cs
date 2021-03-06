using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class CheckAppointmentVerificationConfirmedResult {
		public DateTime Appointment { get; set; }
		public int VerificationSpan { get; set; }
		public byte[] ConfirmationCorrelationCode { get; set; }
	}
	
	public class CheckAppointmentVerificationConfirmedResult2 {
		public long Appointment { get; set; }
		public int VerificationSpan { get; set; }
		public byte[] ConfirmationCorrelationCode { get; set; }
	}
}