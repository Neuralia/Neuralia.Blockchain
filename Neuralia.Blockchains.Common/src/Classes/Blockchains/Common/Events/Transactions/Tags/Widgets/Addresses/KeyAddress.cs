using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses {

	/// <summary>
	///     this class will hold the address of a key inside a block
	/// </summary>
	public class KeyAddress : PublishedAddress {

		static KeyAddress() {
			LiteDBMappers.RegisterKeyAddress();
		}

		public byte OrdinalId {
			get => this.KeyUseIndex.Ordinal;
			set => this.KeyUseIndex.Ordinal = value;
		}

		public IdKeyUseIndexSet KeyUseIndex { get; set; } = new IdKeyUseIndexSet();

		//public byte OrdinalId { get; set; }

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			this.KeyUseIndex.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.KeyUseIndex.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.KeyUseIndex);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
			jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex);
		}

		public new KeyAddress Clone() {
			KeyAddress newAddress = new KeyAddress();

			this.Copy(newAddress);

			return newAddress;

		}

		protected override void Copy(PublishedAddress newAddress) {

			if(newAddress is KeyAddress keyAddress) {
				keyAddress.KeyUseIndex = this.KeyUseIndex.Clone2();
			}

			base.Copy(newAddress);
		}
	}
}