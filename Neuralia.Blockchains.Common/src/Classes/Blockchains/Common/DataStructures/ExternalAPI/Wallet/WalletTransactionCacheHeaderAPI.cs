using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet {
	public class WalletTransactionHistoryHeaderAPI {
		public string TransactionId { get; set; }

		public string Sender { get; set; }
		public string Recipient { get; set; }

		public DateTime Timestamp { get; set; }
		public object Version { get; set; }
		public byte Status { get; set; }
		public string Note { get; set; }
		public bool Local { get; set; }

		public override string ToString() {
			return $"TransactionId: {this.TransactionId}, Local: {this.Local}, Sender: {this.Sender}, Recipient: {this.Recipient}, Timestamp: {this.Timestamp}, Status: {this.Status}, Note: \"{this.Note}\"";
		}
	}

	public class WalletTransactionHistoryDetailsAPI {
		public string TransactionId { get; set; }

		public string Sender { get; set; }
		public string Recipient { get; set; }

		public DateTime Timestamp { get; set; }
		public string Contents { get; set; }
		public object Version { get; set; }
		public byte Status { get; set; }
		public string Note { get; set; }
		public bool Local { get; set; }

		public override string ToString() {
			return $"TransactionId: {this.TransactionId}, Local: {this.Local}, Sender: {this.Sender}, Recipient: {this.Recipient}, Timestamp: {this.Timestamp}, Status: {this.Status}, Note: \"{this.Note}\"";
		}
	}
}