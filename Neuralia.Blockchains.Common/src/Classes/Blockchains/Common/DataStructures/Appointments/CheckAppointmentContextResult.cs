using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class CheckAppointmentContextResult {
		public DateTime Appointment { get; set; }
		public int Window { get; set; }
		public int EngineVersion { get; set; }
		public byte[] POwRuleSet { get; set; }
		public byte[] PuzzleBytes { get; set; }
		public byte[] SecretPackageBytes { get; set; }
	}
}