using System;
using System.Text;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {
	public interface IWalletKeyHistory {
		[BsonId]
		public ObjectId DbId { get; set; }
		
		Guid Id { get; set; }

		long DecommissionedTime { get; set; }

		Guid AccountUuid { get; set; }
		long KeySequenceId { get; set; }
		int Ordinal { get; set; }
		byte[] Key { get; set; }

		long AnnouncementBlockId { get; set; }
		TransactionId DeclarationTransactionId { get; set; }
		void Copy(IWalletKey key);
	}

	public abstract class WalletKeyHistory : IWalletKeyHistory {

		[BsonId]
		public ObjectId DbId { get; set; } = ObjectId.NewObjectId();
		
		public Guid Id { get; set; } = Guid.NewGuid();

		public long DecommissionedTime { get; set; } = DateTime.UtcNow.Ticks;

		public Guid AccountUuid { get; set; }
		public int Ordinal { get; set; }
		public long KeySequenceId { get; set; }
		public byte[] Key { get; set; }
		public long AnnouncementBlockId { get; set; }
		public TransactionId DeclarationTransactionId { get; set; }

		public virtual void Copy(IWalletKey key) {

			this.Id = key.Id;
			this.AccountUuid = key.AccountUuid;
			this.AnnouncementBlockId = key.AnnouncementBlockId.Value;
			this.Ordinal = key.KeyAddress.OrdinalId;
			this.DeclarationTransactionId = key.KeyAddress.DeclarationTransactionId.Clone;
			this.KeySequenceId = key.KeySequenceId;

			string keyDeserialized = JsonSerializer.Serialize(key, JsonUtils.CreateSerializerSettings());
			SafeArrayHandle bytes = Compressors.GeneralPurposeCompressor.Compress(ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(keyDeserialized)));
			this.Key = bytes.ToExactByteArrayCopy();
			bytes.Return();
		}
	}
}