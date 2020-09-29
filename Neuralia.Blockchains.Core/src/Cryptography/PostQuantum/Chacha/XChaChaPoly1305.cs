using System;
using System.Diagnostics.CodeAnalysis;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Org.BouncyCastle.Asn1.Cms;

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
		
		public SafeArrayHandle Encrypt(SafeArrayHandle plaintext, SafeArrayHandle nonce, SafeArrayHandle key) {
			
			SafeArrayHandle ciphertext = this.CreateEncryptedBuffer(plaintext.Length);
			this.chacha.Encrypt(plaintext, nonce, key, ciphertext, plaintext.Length);
			this.poly1305.ComputeMac(ciphertext, key, plaintext.Length);
			return ciphertext;
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle ciphertext, SafeArrayHandle nonce, SafeArrayHandle key) {
						
			SafeArrayHandle plaintext = this.CreateDecryptedBuffer(ciphertext.Length);
			this.chacha.Decrypt(ciphertext, nonce, key, plaintext, plaintext.Length);
	
			Poly1305.VerifyMac(ciphertext, key, ciphertext.Memory.Slice(plaintext.Length, Poly1305.MAC_SIZE), plaintext.Length);
			
			return plaintext;
		}

		public SafeArrayHandle CreateEncryptedBuffer(int plainLength) {
			return SafeArrayHandle.Create(plainLength + Poly1305.MAC_SIZE);
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