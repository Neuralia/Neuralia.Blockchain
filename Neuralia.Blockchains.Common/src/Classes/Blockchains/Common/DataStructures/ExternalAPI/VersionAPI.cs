using MessagePack;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	
	[MessagePackObject(keyAsPropertyName: true)]
	public class VersionAPI {
		public int TransactionType { get; set; }
		public int Major { get; set; }
		public int Minor { get; set; }
	}
}