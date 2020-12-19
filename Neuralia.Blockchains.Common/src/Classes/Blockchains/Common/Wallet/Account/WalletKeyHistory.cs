using System;
using System.Text;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {
	public interface IWalletKeyHistory {
		[BsonId]
		public ObjectId DbId { get; set; }

		Guid Id { get; set; }

		KeyAddress KeyAddress { get; set; }
		long DecommissionedTime { get; set; }

		string AccountCode { get; set; }
		byte[] Key { get; set; }

		long AnnouncementBlockId { get; set; }
		TransactionId DeclarationTransactionId { get; set; }
		void Copy(IWalletKey key);
	}

	public abstract class WalletKeyHistory : IWalletKeyHistory {

		[BsonId]
		public ObjectId DbId { get; set; } = ObjectId.NewObjectId();

		public Guid Id { get; set; } = Guid.NewGuid();

		public long DecommissionedTime { get; set; } = DateTimeEx.CurrentTime.Ticks;

		public string AccountCode { get; set; }
		public KeyAddress KeyAddress { get; set; }
		public byte[] Key { get; set; }
		public long AnnouncementBlockId { get; set; }
		public TransactionId DeclarationTransactionId { get; set; }

		public virtual void Copy(IWalletKey key) {

			this.Id = key.Id;
			this.AccountCode = key.AccountCode;
			this.AnnouncementBlockId = key.AnnouncementBlockId.Value;
			this.DeclarationTransactionId = key.KeyAddress.DeclarationTransactionId.Clone;
			this.KeyAddress = key.KeyAddress.Clone();

			var dehydrator = DataSerializationFactory.CreateDehydrator();
			key.Dehydrate(dehydrator);
			using var keyBytes = dehydrator.ToArray();
			
			using SafeArrayHandle bytes = Compressors.GeneralPurposeCompressor.Compress(keyBytes);
			this.Key = bytes.ToExactByteArrayCopy();
		}
	}
}