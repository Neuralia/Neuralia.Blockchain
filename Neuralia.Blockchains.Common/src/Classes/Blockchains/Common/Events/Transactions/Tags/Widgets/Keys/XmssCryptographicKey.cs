using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface IXmssCryptographicKey : ICryptographicKey {
		byte BitSize { get; set; }
		byte TreeHeight { get; set; }
	}

	public class XmssCryptographicKey : CryptographicKey, IXmssCryptographicKey {

		public XmssCryptographicKey() {
			this.BitSize = (byte) Enums.KeyHashBits.SHA3_256;
			this.TreeHeight = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;
		}

		public byte BitSize { get; set; }
		public byte TreeHeight { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.BitSize);
			dehydrator.Write(this.TreeHeight);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.BitSize = rehydrator.ReadByte();
			this.TreeHeight = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BitSize);
			nodeList.Add(this.TreeHeight);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("BitSize", this.BitSize);
			jsonDeserializer.SetProperty("TreeHeight", this.TreeHeight);
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.XMSS;
		}
	}
}