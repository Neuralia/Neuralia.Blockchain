using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface IMcElieceWalletKey : IWalletKey, IMcElieceKey {
		
	}

	public class McElieceWalletKey : WalletKey, IMcElieceWalletKey {
		
		public McElieceWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.MCELIECE, 1,0);
		}

		public McElieceEncryptor.McElieceCipherModes McElieceCipherMode { get; set; } = McElieceEncryptor.DEFAULT_CIPHER_MODE;
		public int M { get; set; } = McElieceEncryptor.DEFAULT_M;
		public int T { get; set; } = McElieceEncryptor.DEFAULT_T;
		public McElieceEncryptor.McElieceHashModes McElieceHashMode { get; set; } = McElieceEncryptor.DEFAULT_HASH_MODE;

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte) this.McElieceCipherMode);
			nodeList.Add(this.M);
			nodeList.Add(this.T);
			nodeList.Add((byte) this.McElieceHashMode);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Value = this.M;
			entry.Dehydrate(dehydrator);

			entry.Value = this.T;
			entry.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.McElieceCipherMode);
			dehydrator.Write((byte) this.McElieceHashMode);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.M = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.T = (int) entry.Value;

			this.McElieceCipherMode = rehydrator.ReadByteEnum<McElieceEncryptor.McElieceCipherModes>();
			this.McElieceHashMode = rehydrator.ReadByteEnum<McElieceEncryptor.McElieceHashModes>();
		}
	}
}