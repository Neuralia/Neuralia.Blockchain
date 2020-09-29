namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	public class DistilledAppointmentContext {
		public int Window { get; set; }
		public int EngineVersion { get; set; }
		public byte[] PuzzleBytes { get; set; }
		public byte[] PackageBytes { get; set; }
		public byte[] POWRuleSet { get; set; }
	}
}