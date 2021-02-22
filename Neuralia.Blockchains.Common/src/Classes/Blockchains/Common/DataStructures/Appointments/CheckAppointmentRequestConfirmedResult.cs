using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class CheckAppointmentRequestConfirmedResult {
		public long Appointment         { get; set; }
		public int      Preparation         { get; set; }
		public int      Finalization        { get; set; }
		public int     Index               { get; set; }
		public byte[]   SecretAppointmentId { get; set; }
	}
	
	public class CheckAppointmentRequestConfirmedResult2 {
		public long Appointment         { get; set; }
		public int      Preparation         { get; set; }
		public int      Finalization        { get; set; }
		public int     Index               { get; set; }
		public byte[]   SecretAppointmentId { get; set; }
	}
}