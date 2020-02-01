using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses {

	/// <summary>
	///     this class will hold the address of a key inside a block
	/// </summary>
	public class KeyAddress : PublishedAddress {

		static KeyAddress() {
			LiteDBMappers.RegisterKeyAddress();
		}

		public byte OrdinalId { get; set; }
		
		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.Write(this.OrdinalId);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.OrdinalId = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();
			
			nodeList.Add(this.OrdinalId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
			jsonDeserializer.SetProperty("OrdinalId", this.OrdinalId);
		}

		public new KeyAddress Clone() {
			KeyAddress newAddress = new KeyAddress();

			this.Copy(newAddress);

			return newAddress;
			
		}

		protected override void Copy(PublishedAddress newAddress) {

			if(newAddress is KeyAddress keyAddress) {
				keyAddress.OrdinalId = this.OrdinalId;
			}
			base.Copy(newAddress);
		}
	}
}