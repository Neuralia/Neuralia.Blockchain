using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	
	public interface IXmssmtKey : IXmssKey {

		byte TreeLayers { get; set; }
	}
	
	public interface IXmssmtCryptographicKey : IXmssmtKey, IXmssCryptographicKey {
		byte TreeLayers { get; set; }
	}

	public class XmssmtCryptographicKey : XmssCryptographicKey, IXmssmtCryptographicKey {

		public XmssmtCryptographicKey() {
			this.HashType = XMSSMTProvider.DEFAULT_HASH_BITS;
			this.BackupHashType = XMSSMTProvider.DEFAULT_HASH_BITS;
			this.TreeHeight = XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT;
			this.TreeLayers = XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS;
		}

		public XmssmtCryptographicKey(IXmssmtCryptographicKey other) : base(other) {
		}
		
		public byte TreeLayers { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.TreeLayers);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.TreeLayers = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeLayers);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TreeLayer", this.TreeLayers);
		}

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);
			
			if(walletKey is IXmssmtKey xmssMTWalletKey) {

				this.TreeLayers = xmssMTWalletKey.TreeLayers;
			}
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.XMSSMT, 1, 0);
		}

	}
}