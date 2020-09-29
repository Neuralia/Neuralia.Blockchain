using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface IMcElieceKey : IKey {
		McElieceEncryptor.McElieceCipherModes McElieceCipherMode { get; set; }
		int M { get; set; }
		int T { get; set; }
		McElieceEncryptor.McElieceHashModes McElieceHashMode { get; set; }
	}
	
	public interface IMcElieceCryptographicKey : IMcElieceKey, ICryptographicKey {
		
	}

	public class McElieceCryptographicKey : CryptographicKey, IMcElieceCryptographicKey {

		public McElieceEncryptor.McElieceCipherModes McElieceCipherMode { get; set; } = McElieceEncryptor.DEFAULT_CIPHER_MODE;
		public int M { get; set; } = McElieceEncryptor.DEFAULT_M;
		public int T { get; set; } = McElieceEncryptor.DEFAULT_T;
		public McElieceEncryptor.McElieceHashModes McElieceHashMode { get; set; } = McElieceEncryptor.DEFAULT_HASH_MODE;

		public McElieceCryptographicKey() {

		}

		public McElieceCryptographicKey(IMcElieceCryptographicKey other) : base(other) {
		}

		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.McElieceCipherMode);
			dehydrator.Write((byte) this.McElieceHashMode);
			dehydrator.Write(this.M);
			dehydrator.Write(this.T);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.McElieceCipherMode = (McElieceEncryptor.McElieceCipherModes) rehydrator.ReadByte();
			this.McElieceHashMode = (McElieceEncryptor.McElieceHashModes) rehydrator.ReadByte();
			this.M = rehydrator.ReadInt();
			this.T = rehydrator.ReadInt();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte) this.McElieceCipherMode);
			nodeList.Add((byte) this.McElieceHashMode);
			nodeList.Add(this.M);
			nodeList.Add(this.T);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("McElieceCipherMode", this.McElieceCipherMode.ToString());
			jsonDeserializer.SetProperty("McElieceHashMode", this.McElieceHashMode.ToString());
			jsonDeserializer.SetProperty("m", this.M);
			jsonDeserializer.SetProperty("t", this.T);
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.MCELIECE, 1, 0);
		}

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is IMcElieceKey mcElieceWalletKey) {
				this.McElieceCipherMode = mcElieceWalletKey.McElieceCipherMode;
				this.McElieceHashMode = mcElieceWalletKey.McElieceHashMode;
				this.M = mcElieceWalletKey.M;
				this.T = mcElieceWalletKey.T;
			}
		}
	}
}