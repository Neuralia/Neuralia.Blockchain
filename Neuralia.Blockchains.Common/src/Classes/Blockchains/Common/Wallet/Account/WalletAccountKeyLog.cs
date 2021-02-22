using System;
using LiteDB;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccountKeyLog {
		[BsonId(true)]
		ObjectId Id { get; set; }

		byte KeyOrdinalId { get; set; }

		IdKeyUseIndexSet KeyUseIndex { get; set; }

		string EventId { get; set; }
		byte EventType { get; set; }
		DateTime Timestamp { get; set; }

		long? ConfirmationBlockId { get; set; }
	}

	public abstract class WalletAccountKeyLog : IWalletAccountKeyLog {

		[BsonId(true)]
		public ObjectId Id { get; set; }

		public byte KeyOrdinalId { get; set; }
		public IdKeyUseIndexSet KeyUseIndex { get; set; }
		public string EventId { get; set; }

		public byte EventType { get; set; }

		public DateTime Timestamp { get; set; }

		public long? ConfirmationBlockId { get; set; }
	}

	public interface IWalletAccountKeyLogMetadata {
		[BsonId]
		int Id { get; set; }
		
		SafeArrayHandle KeyIndexEncryptionKey { get; set; }
	}

	public abstract class WalletAccountKeyLogMetadata : IWalletAccountKeyLogMetadata {

		[BsonId]
		public int Id { get; set; }

		public SafeArrayHandle KeyIndexEncryptionKey { get; set; }
		
		public Guid KeyIndexEncryptionFileName { get; set; }
	}
}