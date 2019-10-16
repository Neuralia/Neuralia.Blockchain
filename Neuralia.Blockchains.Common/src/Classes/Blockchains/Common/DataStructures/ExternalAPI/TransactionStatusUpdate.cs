
namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	
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