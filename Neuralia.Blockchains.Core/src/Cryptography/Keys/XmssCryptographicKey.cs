using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface IXmssKey : IKey {
		Enums.KeyHashType HashType { get; set; }
		Enums.KeyHashType BackupHashType { get; set; }
		byte TreeHeight { get; set; }
	}
	
	public interface IXmssCryptographicKey : IXmssKey, ICryptographicKey {
		int UseIndex { get; set; }
	}

	public class XmssCryptographicKey : CryptographicKey, IXmssCryptographicKey {

		public XmssCryptographicKey() {
			this.HashType = Enums.KeyHashType.SHA3_256;
			this.BackupHashType = Enums.KeyHashType.SHA2_256;
			this.TreeHeight = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;
		}

		public XmssCryptographicKey(IXmssCryptographicKey other) : base(other) {
		}

		public Enums.KeyHashType HashType { get; set; }
		public Enums.KeyHashType BackupHashType { get; set; }
		public byte TreeHeight { get; set; }
		public int UseIndex { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Value = this.UseIndex;
			tool.Dehydrate(dehydrator);
			
			dehydrator.Write((byte) this.HashType);
			dehydrator.Write((byte) this.BackupHashType);
			dehydrator.Write(this.TreeHeight);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Rehydrate(rehydrator);
			this.UseIndex = (int) tool.Value;
			this.HashType = (Enums.KeyHashType) rehydrator.ReadByte();
			this.BackupHashType = (Enums.KeyHashType) rehydrator.ReadByte();
			this.TreeHeight = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.UseIndex);
			nodeList.Add(this.HashType);
			nodeList.Add(this.BackupHashType);
			nodeList.Add(this.TreeHeight);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("UseIndex", this.UseIndex);
			jsonDeserializer.SetProperty("HashType", this.HashType.ToString());
			jsonDeserializer.SetProperty("BackupHashType", this.BackupHashType.ToString());
			jsonDeserializer.SetProperty("TreeHeight", this.TreeHeight);
		}

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is IXmssKey xmssWalletKey) {
				this.HashType = xmssWalletKey.HashType;
				this.BackupHashType = xmssWalletKey.BackupHashType;
				this.TreeHeight = (byte) xmssWalletKey.TreeHeight;
			}
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.XMSS, 1, 0);
		}

	}
}