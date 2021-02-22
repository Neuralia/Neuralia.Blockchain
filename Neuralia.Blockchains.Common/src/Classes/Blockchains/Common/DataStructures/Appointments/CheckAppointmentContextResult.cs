using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments {
	public class CheckAppointmentContextResult {
		public DateTime Appointment { get; set; }
		public int Window { get; set; }
		public int EngineVersion { get; set; }
		public string POwRuleSet { get; set; }
		public string PuzzleBytes { get; set; }
		public string SecretPackageBytes { get; set; }
	}
	
	public class CheckAppointmentContextResult2 {
		public DateTime Appointment { get; set; }
		public int Window { get; set; }
		public int EngineVersion { get; set; }
		public string POwRuleSet { get; set; }
		public string PuzzleBytes { get; set; }
		public string SecretPackageBytes { get; set; }
	}
}