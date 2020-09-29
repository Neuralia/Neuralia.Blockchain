using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public interface ITLSCertificate : ICryptographicKey {
	}

	public class TLSCertificate : CryptographicKey, ITLSCertificate {
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.RSA, 1, 0);
		}

	}
}