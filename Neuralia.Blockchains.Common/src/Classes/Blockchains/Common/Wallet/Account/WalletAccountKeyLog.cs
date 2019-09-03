using System;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccountKeyLog {
		[BsonId(true)]
		ObjectId Id { get; set; }

		byte KeyOrdinalId { get; set; }

		KeyUseIndexSet KeyUseIndex { get; set; }

		string EventId { get; set; }
		byte EventType { get; set; }
		DateTime Timestamp { get; set; }

		long? ConfirmationBlockId { get; set; }
	}

	public abstract class WalletAccountKeyLog : IWalletAccountKeyLog {

		[BsonId(true)]
		public ObjectId Id { get; set; }

		public byte KeyOrdinalId { get; set; }
		public KeyUseIndexSet KeyUseIndex { get; set; }
		public string EventId { get; set; }

		public byte EventType { get; set; }

		public DateTime Timestamp { get; set; }

		public long? ConfirmationBlockId { get; set; }
	}
}