using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class CheckAppointmentRequestConfirmedResult {
		public DateTime Appointment         { get; set; }
		public int      Preparation         { get; set; }
		public int      Finalization        { get; set; }
		public long     Index               { get; set; }
		public byte[]   SecretAppointmentId { get; set; }
	}
}