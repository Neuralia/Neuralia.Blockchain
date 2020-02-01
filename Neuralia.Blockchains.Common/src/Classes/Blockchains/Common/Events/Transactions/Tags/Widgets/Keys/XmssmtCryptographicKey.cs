using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface IXmssmtCryptographicKey : IXmssCryptographicKey {
		byte TreeLayer { get; set; }
	}

	public class XmssmtCryptographicKey : XmssCryptographicKey, IXmssmtCryptographicKey {

		public XmssmtCryptographicKey() {
			this.BitSize = XMSSMTProvider.DEFAULT_HASH_BITS;
			this.TreeHeight = XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT;
			this.TreeLayer = XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS;
		}

		public byte TreeLayer { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.TreeLayer);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.TreeLayer = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeLayer);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TreeLayer", this.TreeLayer);
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.XMSSMT;
		}
		
		public override  void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is IXmssMTWalletKey xmssMtWalletKey) {
				this.TreeLayer = (byte)xmssMtWalletKey.TreeLayers;
			}
		}
	}
}