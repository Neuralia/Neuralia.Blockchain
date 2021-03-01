using System;
using System.Diagnostics.CodeAnalysis;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public class XChaChaPoly1305 : IDisposableExtended{

		private readonly IPoly1305 poly1305;
		private readonly IXChaCha chacha;
		
		public XChaChaPoly1305(int rounds = XChaCha.CHACHA_DEFAULT_ROUNDS) {
			this.chacha = ChachaFactory.CreateXChacha(rounds);
			this.poly1305 = ChachaFactory.Create1305();
		}

		public void SetRounds(int rounds) {
			this.chacha.SetRounds(rounds);
		}

		public void Encrypt(SafeArrayHandle plaintext, SafeArrayHandle ciphertext, SafeArrayHandle nonce, SafeArrayHandle key, int? length = null) {
			
			int len = length.HasValue ? length.Value : plaintext.Length;
			this.chacha.Encrypt(plaintext, nonce, key, ciphertext, len);
			this.poly1305.ComputeMac(ciphertext, key, len);
		}

		public SafeArrayHandle Encrypt(SafeArrayHandle plaintext, SafeArrayHandle nonce, SafeArrayHandle key, int? length = null, int prefix = 0) {
			
			int len = length.HasValue ? length.Value : plaintext.Length;
			SafeArrayHandle ciphertext = this.CreateEncryptedBuffer(len, prefix);
			this.Encrypt(plaintext, ciphertext, nonce, key, len);

			return ciphertext;
		}

		public void Decrypt(SafeArrayHandle ciphertext, SafeArrayHandle plaintext, SafeArrayHandle nonce, SafeArrayHandle key, int? length = null) {
				
			int len = length.HasValue ? length.Value : plaintext.Length;
			this.chacha.Decrypt(ciphertext, nonce, key, plaintext, len);
	
			Poly1305.VerifyMac(ciphertext, key, ciphertext.Memory.Slice(len, Poly1305.MAC_SIZE), len);
		}
		
		public SafeArrayHandle Decrypt(SafeArrayHandle ciphertext, SafeArrayHandle nonce, SafeArrayHandle key, int? length = null) {
				
			int len = length.HasValue ? length.Value : ciphertext.Length;
			SafeArrayHandle plaintext = this.CreateDecryptedBuffer(len);
			this.Decrypt(ciphertext, plaintext, nonce, key, plaintext.Length);
			
			return plaintext;
		}

		public SafeArrayHandle CreateEncryptedBuffer(int plainLength, int prefix = 0) {
			var buffer = SafeArrayHandle.Create(plainLength + Poly1305.MAC_SIZE + prefix);

			if(prefix != 0) {
				buffer.Entry.IncreaseOffset(prefix);
			}
			return buffer;
		}

		public SafeArrayHandle CreateDecryptedBuffer(int cypherLength) {
			return SafeArrayHandle.Create(cypherLength - Poly1305.MAC_SIZE);
		}
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		[SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {

				if(this.poly1305 is IDisposable disposable) {
					disposable.Dispose();
				}
				this.chacha.Dispose();
			}

			this.IsDisposed = true;
		}

		~XChaChaPoly1305() {
			this.Dispose(false);
		}

	#endregion
	}
}