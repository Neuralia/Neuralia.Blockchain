using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface ISphincsCryptographicKey : ICryptographicKey {
	}

	public class SphincsCryptographicKey : CryptographicKey, ISphincsCryptographicKey {
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SPHINCS, 1, 0);
		}

	}
}