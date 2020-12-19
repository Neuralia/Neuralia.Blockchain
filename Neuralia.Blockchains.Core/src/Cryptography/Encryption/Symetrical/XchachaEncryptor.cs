using System;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
	public class XchachaEncryptor : IDisposableExtended {

		private readonly XChaChaPoly1305 xChaChaPoly1305;

		public XchachaEncryptor(int rounds = XChaCha.CHACHA_DEFAULT_ROUNDS) {
			xChaChaPoly1305 = new XChaChaPoly1305(rounds);
		}
		
		public void Encrypt(SafeArrayHandle message, SafeArrayHandle encrypted, SafeArrayHandle nonce, SafeArrayHandle key) {

			xChaChaPoly1305.Encrypt(message, encrypted, nonce, key);
		}
		
		public SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle nonce, SafeArrayHandle key) {

			return xChaChaPoly1305.Encrypt(message, nonce, key);
		}
		
		public void Encrypt(SafeArrayHandle message, SafeArrayHandle encrypted, SafeArrayHandle password, SafeArrayHandle salt, int iterations) {

			(SafeArrayHandle nonce, SafeArrayHandle key) = CryptoUtil.GenerateKeyNonceSet(password, salt, iterations);

			Encrypt(message, encrypted, nonce, key);
		}
		
		public SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle password, SafeArrayHandle salt, int iterations) {

			(SafeArrayHandle nonce, SafeArrayHandle key) = CryptoUtil.GenerateKeyNonceSet(password, salt, iterations);

			return Encrypt(message, nonce, key);
		}
		
		public void Decrypt(SafeArrayHandle encrypted, SafeArrayHandle plain, SafeArrayHandle nonce, SafeArrayHandle key) {

			xChaChaPoly1305.Decrypt(encrypted, plain, nonce, key);
		}
		
		public SafeArrayHandle Decrypt(SafeArrayHandle encrypted, SafeArrayHandle nonce, SafeArrayHandle key) {

			return xChaChaPoly1305.Decrypt(encrypted, nonce, key);
		}
		
		public void Decrypt(SafeArrayHandle encrypted, SafeArrayHandle plain, SafeArrayHandle password, SafeArrayHandle salt, int iterations) {
			
			(SafeArrayHandle nonce, SafeArrayHandle key) = CryptoUtil.GenerateKeyNonceSet(password, salt, iterations);

			Decrypt(encrypted, plain, nonce, key);
		}
		
		public SafeArrayHandle Decrypt(SafeArrayHandle encrypted, SafeArrayHandle password, SafeArrayHandle salt, int iterations) {
			
			(SafeArrayHandle nonce, SafeArrayHandle key) = CryptoUtil.GenerateKeyNonceSet(password, salt, iterations);

			return Decrypt(encrypted, nonce, key);
		}
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
			}

			this.IsDisposed = true;
		}

		~XchachaEncryptor() {
			this.Dispose(false);
		}

	#endregion

	}
}