using System;
using LiteDB;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletTransactionHistory {
		[BsonId]
		string TransactionId { get; set; }

		DateTime Timestamp { get; set; }

		string Recipient { get; set; }
		string Version { get; set; }

		string Contents { get; set; }

		byte Status { get; set; }
		string Note { get; set; }
		bool Local { get; set; }
	}

	/// <summary>
	///     Here we save generic history of transactions. Contrary to the transaction cache, this is not meant for active use
	///     and is only a history for convenience
	/// </summary>
	public abstract class WalletTransactionHistory : IWalletTransactionHistory {

		public enum TransactionStatuses : byte {
			New = 0,
			Dispatched = 1,
			Confirmed = 2,
			Rejected = 3
		}

		[BsonId]
		public string TransactionId { get; set; }

		public DateTime Timestamp { get; set; }
		public string Recipient { get; set; }

		public string Version { get; set; }

		public string Contents { get; set; }

		public byte Status { get; set; }

		public string Note { get; set; }

		/// <summary>
		///     true if it is our own transaction. otherwise it target's us
		/// </summary>
		public bool Local { get; set; }
	}
}