using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class QueryValidatorAppointmentSessionsResult {
		public ValidatorAppointmentSession[] Sessions { get; set; }

		public class ValidatorAppointmentSession {
			public DateTime                Appointment     { get; set; }
			public int                     Window { get; set; }
			public int                     ValidatorWindow { get; set; }
			public ValidatorSessionSlice[] Slices          { get; set; }

			public class ValidatorSessionSlice {
				public int Index { get; set; }
				public byte[] EncryptedSecretCodeBytes { get; set; }
			}
		}
	}
}