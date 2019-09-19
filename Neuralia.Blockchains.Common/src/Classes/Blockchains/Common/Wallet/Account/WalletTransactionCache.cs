using System;
using LiteDB;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletTransactionCache {
		[BsonId]
		string TransactionId { get; set; }

		DateTime Timestamp { get; set; }
		SafeArrayHandle Transaction { get;  }
		string Version { get; set; }
		byte Status { get; set; }
		long GossipMessageHash { get; set; }
		DateTime Expiration { get; set; }
	}

	/// <summary>
	///     Here we save transactions in full detail to maintain useful state
	/// </summary>
	public abstract class WalletTransactionCache : IWalletTransactionCache {

		public enum TransactionStatuses : byte {
			New = 0,
			Dispatched = 1,
			Confirmed = 2,
			Rejected = 3
		}

		[BsonId]
		public string TransactionId { get; set; }

		public DateTime Timestamp { get; set; }
		public SafeArrayHandle Transaction { get; } = SafeArrayHandle.Create();
		public string Version { get; set; }
		public byte Status { get; set; }
		public long GossipMessageHash { get; set; }
		public DateTime Expiration { get; set; }
	}
}