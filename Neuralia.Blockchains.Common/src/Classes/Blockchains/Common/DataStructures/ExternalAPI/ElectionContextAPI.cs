namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public class ElectionContextAPI {
		public int Type { get; set; }
		public byte[] ContextBytes { get; set; }
		public long BlockId { get; set; }
		public long MaturityId { get; set; }
		public long PublishId { get; set; }
	}
}