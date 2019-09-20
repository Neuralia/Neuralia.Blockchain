using MessagePack;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	
	[MessagePackObject(keyAsPropertyName: true)]
	public class TransactionStatusUpdate {
		public enum Statuses {
			Confirmed,
			Rejected
		}

		public string TransactionId { get; set; }
		public int Status { get; set; }
		public bool AccountPublish { get; set; }
	}
}