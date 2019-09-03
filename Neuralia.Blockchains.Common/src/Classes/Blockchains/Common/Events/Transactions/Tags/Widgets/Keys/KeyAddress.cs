using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {

	/// <summary>
	///     this class will hold the address of a key inside a block
	/// </summary>
	public class KeyAddress : ISerializableCombo {

		static KeyAddress() {
			LiteDBMappers.RegisterKeyAddress();
		}

		public BlockId AnnouncementBlockId { get; set; } = new BlockId();

		public byte OrdinalId { get; set; }
		public TransactionId DeclarationTransactionId { get; set; } = new TransactionId();

		public AccountId AccountId { get; set; } = new AccountId();

		public int KeyedTransactionIndex { get; set; }

		public void Dehydrate(IDataDehydrator dehydrator) {

			this.AnnouncementBlockId.Dehydrate(dehydrator);

			dehydrator.Write(this.OrdinalId);
			this.AccountId.Dehydrate(dehydrator);

			this.DeclarationTransactionId.Dehydrate(dehydrator);

			AdaptiveInteger1_4 number = new AdaptiveInteger1_4((uint) this.KeyedTransactionIndex);
			number.Dehydrate(dehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.AnnouncementBlockId.Rehydrate(rehydrator);

			this.OrdinalId = rehydrator.ReadByte();
			this.AccountId.Rehydrate(rehydrator);

			this.DeclarationTransactionId.Rehydrate(rehydrator);

			AdaptiveInteger1_4 number = new AdaptiveInteger1_4();
			number.Rehydrate(rehydrator);
			this.KeyedTransactionIndex = (int) number.Value;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.AnnouncementBlockId.GetStructuresArray());

			nodeList.Add(this.OrdinalId);
			nodeList.Add(this.AccountId);

			nodeList.Add(this.DeclarationTransactionId.GetStructuresArray());
			nodeList.Add(this.KeyedTransactionIndex);

			return nodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			//
			jsonDeserializer.SetProperty("AnnouncementBlockId", this.AnnouncementBlockId);
			jsonDeserializer.SetProperty("OrdinalId", this.OrdinalId);
			jsonDeserializer.SetProperty("AccountId", this.AccountId);
			jsonDeserializer.SetProperty("DeclarationTransactionId", this.DeclarationTransactionId);
			jsonDeserializer.SetProperty("KeyedTransactionIndex", this.KeyedTransactionIndex);

		}

		public KeyAddress Copy() {
			KeyAddress newAddress = new KeyAddress();

			newAddress.KeyedTransactionIndex = this.KeyedTransactionIndex;
			newAddress.OrdinalId = this.OrdinalId;
			newAddress.AnnouncementBlockId = new BlockId(this.AnnouncementBlockId.Value);
			newAddress.DeclarationTransactionId = new TransactionId(this.DeclarationTransactionId);
			newAddress.AccountId = new AccountId(this.AccountId);

			return newAddress;
		}
	}
}