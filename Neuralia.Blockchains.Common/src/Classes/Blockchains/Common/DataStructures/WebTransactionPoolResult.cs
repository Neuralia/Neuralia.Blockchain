using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	public class WebTransactionPoolResult {
		public string TransactionId { get; set; }

		public decimal Tip { get; set; }
		
		public int TransactionType { get; set; }

		public DateTime Expiration { get; set; }

		public long BlockId { get; set; }

		public int Size { get; set; }
	}
}