using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface IXmssCryptographicKey : ICryptographicKey {
		Enums.KeyHashBits BitSize { get; set; }
		byte TreeHeight { get; set; }
	}

	public class XmssCryptographicKey : CryptographicKey, IXmssCryptographicKey {

		public XmssCryptographicKey() {
			this.BitSize = Enums.KeyHashBits.SHA3_256;
			this.TreeHeight = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;
		}

		public Enums.KeyHashBits BitSize { get; set; }
		public byte TreeHeight { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.BitSize);
			dehydrator.Write(this.TreeHeight);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.BitSize = (Enums.KeyHashBits) rehydrator.ReadByte();
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
			jsonDeserializer.SetProperty("BitSize", this.BitSize.ToString());
			jsonDeserializer.SetProperty("TreeHeight", this.TreeHeight);
		}

		public override void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is IXmssWalletKey xmssWalletKey) {
				this.BitSize = xmssWalletKey.HashBits;
				this.TreeHeight = (byte) xmssWalletKey.TreeHeight;
			}
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.XMSS;
		}
	}
}