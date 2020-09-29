using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption {
	public static class CryptoUtil {
		public static (SafeArrayHandle nonce, SafeArrayHandle key) GenerateKeyNonceSet(SafeArrayHandle password, SafeArrayHandle salt, int rounds = 5000) {
			SafeArrayHandle nonce = null;
			SafeArrayHandle key = null;
			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), rounds, HashAlgorithmName.SHA512)) {
					
				key = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(XChaCha.KEY_SIZE_IN_BYTES));
				nonce = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(XChaCha.NONCE_SIZE_IN_BYTES));
			}
			
			return (nonce, key);
		}
	}
}