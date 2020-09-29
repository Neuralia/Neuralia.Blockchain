using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface INTRUKey : IKey {

	}

	public interface INTRUCryptographicKey : INTRUKey, ICryptographicKey {
	}

	public class NTRUCryptographicKey : CryptographicKey, INTRUCryptographicKey {

		public NTRUCryptographicKey() {

		}

		public NTRUCryptographicKey(INTRUCryptographicKey other) : base(other) {
		}
		
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
		}

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is INTRUKey ntruWalletKey) {

			}
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.NTRU, 1, 0);
		}

	}
}