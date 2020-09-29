using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;


namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
	public class NTRUPrimeEncryptor : IDisposableExtended {

		protected NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes type;

		public NTRUPrimeEncryptor(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes type = NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857) {
			
			this.type = type;
		}

		protected NtruPrimeEngine CreateNTRUPrimeEngine() {
			return new NtruPrimeEngine(type);
		}
		
		public (SafeArrayHandle cypher, SafeArrayHandle session) Encrypt(SafeArrayHandle message, SafeArrayHandle publicKey) {
			using NtruPrimeEngine ntru = this.CreateNTRUPrimeEngine();

			if(message.Length != 64) {
				throw new ArgumentException($"The message must have a length of 64 bytes", nameof(message));
			}
			return ntru.CryptoKemEnc(in message, in publicKey);
		}

		public (SafeArrayHandle publicKey, SafeArrayHandle privateKey) GenerateKeyPair() {
			using NtruPrimeEngine ntru = this.CreateNTRUPrimeEngine();

			return ntru.CryptoKemKeypair();
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cryptedMessage, SafeArrayHandle privateKey) {
			using NtruPrimeEngine ntru = this.CreateNTRUPrimeEngine();
			
			return ntru.CryptoKemDec(in cryptedMessage, in privateKey);
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

		~NTRUPrimeEncryptor() {
			this.Dispose(false);
		}

	#endregion

	}
}