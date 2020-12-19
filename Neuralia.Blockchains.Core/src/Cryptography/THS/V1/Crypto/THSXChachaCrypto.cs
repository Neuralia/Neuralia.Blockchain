using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto {
	public abstract class THSXChachaCrypto : THSCryptoBase {

		private readonly int rounds;

		public THSXChachaCrypto(int rounds) {
			this.rounds = rounds;
		}

		public override void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted) {

			using SafeArrayHandle key = SafeArrayHandle.Create(XChaCha.KEY_SIZE_IN_BYTES);
			using SafeArrayHandle nonce = SafeArrayHandle.Create(XChaCha.NONCE_SIZE_IN_BYTES);

			key.CopyFrom(message.Entry, message.Length - XChaCha.KEY_SIZE_IN_BYTES, 0, XChaCha.KEY_SIZE_IN_BYTES);
			nonce.CopyFrom(message.Entry, message.Length - XChaCha.KEY_SIZE_IN_BYTES - XChaCha.NONCE_SIZE_IN_BYTES, 0, XChaCha.NONCE_SIZE_IN_BYTES);

			using(XChaCha csEncrypt = new XChaCha(this.rounds)) {
				csEncrypt.Encrypt(message, nonce, key, encrypted);
			}

		}
	}
}