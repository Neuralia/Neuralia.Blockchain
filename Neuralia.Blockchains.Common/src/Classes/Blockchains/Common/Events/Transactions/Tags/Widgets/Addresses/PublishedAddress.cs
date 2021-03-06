using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses {
	public class PublishedAddress : ISerializableCombo {

		static PublishedAddress() {
			LiteDBMappers.RegisterPublishedAddress();
		}

		public BlockId AnnouncementBlockId { get; set; } = new BlockId();
		public TransactionId DeclarationTransactionId { get; set; } = new TransactionId();
		public AccountId AccountId { get; set; } = new AccountId();
		public int IndexedTransactionIndex { get; set; }

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			this.AnnouncementBlockId.Dehydrate(dehydrator);

			this.AccountId.Dehydrate(dehydrator);

			this.DeclarationTransactionId.Dehydrate(dehydrator);

			AdaptiveInteger1_4 number = new AdaptiveInteger1_4((uint) this.IndexedTransactionIndex);
			number.Dehydrate(dehydrator);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.AnnouncementBlockId.Rehydrate(rehydrator);

			this.AccountId.Rehydrate(rehydrator);

			this.DeclarationTransactionId.Rehydrate(rehydrator);

			AdaptiveInteger1_4 number = new AdaptiveInteger1_4();
			number.Rehydrate(rehydrator);
			this.IndexedTransactionIndex = (int) number.Value;
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.AnnouncementBlockId.GetStructuresArray());

			nodeList.Add(this.AccountId);

			nodeList.Add(this.DeclarationTransactionId.GetStructuresArray());
			nodeList.Add(this.IndexedTransactionIndex);

			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("AnnouncementBlockId", this.AnnouncementBlockId);
			jsonDeserializer.SetProperty("AccountId", this.AccountId);
			jsonDeserializer.SetProperty("DeclarationTransactionId", this.DeclarationTransactionId);
			jsonDeserializer.SetProperty("IndexedTransactionIndex", this.IndexedTransactionIndex);

		}

		public PublishedAddress Clone() {
			PublishedAddress newAddress = new PublishedAddress();

			this.Copy(newAddress);

			return newAddress;
		}

		protected virtual void Copy(PublishedAddress newAddress) {
			newAddress.IndexedTransactionIndex = this.IndexedTransactionIndex;
			newAddress.AnnouncementBlockId = new BlockId(this.AnnouncementBlockId.Value);
			newAddress.DeclarationTransactionId = new TransactionId(this.DeclarationTransactionId);
			newAddress.AccountId = new AccountId(this.AccountId);
		}
	}
}