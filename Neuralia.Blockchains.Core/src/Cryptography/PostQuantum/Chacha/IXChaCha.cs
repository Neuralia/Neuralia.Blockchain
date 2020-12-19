using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public interface IXChaCha : IDisposableExtended {
		void Encrypt(SafeArrayHandle plaintext, SafeArrayHandle nonce, SafeArrayHandle key, SafeArrayHandle ciphertext, int? length = null);
		void Decrypt(SafeArrayHandle ciphertext, SafeArrayHandle nonce, SafeArrayHandle key, SafeArrayHandle plaintext, int? length = null);
		void SetRounds(int rounds);
	}
}