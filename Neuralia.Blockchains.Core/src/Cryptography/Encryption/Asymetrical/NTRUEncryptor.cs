using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.crypto.ntru;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
	public class NtruEncryptor : IDisposable2 {

		protected NTRUEncryptionKeyGenerationParameters parameters;

		public NtruEncryptor() {

			IDigest Digest256Generator() {
				return new Sha3ExternalDigest(256);
			}

			IDigest Digest512Generator() {
				return new Sha3ExternalDigest(512);
			}

			NTRUEncryptionKeyGenerationParameters.NTRUEncryptionKeyGenerationParametersTypes type = NTRUEncryptionKeyGenerationParameters.NTRUEncryptionKeyGenerationParametersTypes.EES1499EP1_EXT;

			this.parameters = NTRUEncryptionKeyGenerationParameters.CreateNTRUEncryptionKeyGenerationParameters(type, Digest256Generator, Digest512Generator);

			this.parameters.fastFp = true;
		}

		protected NTRUEncryptionKeyPairGenerator CreateNtruGenerator() {
			return new NTRUEncryptionKeyPairGenerator(this.parameters);
		}

		public SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle publicKey) {

			return this.Encrypt(message, new NTRUEncryptionPublicKeyParameters(publicKey, this.parameters.EncryptionParameters));
		}

		public SafeArrayHandle Encrypt(SafeArrayHandle message, NTRUEncryptionPublicKeyParameters publicKey) {
			using(NTRUEngine ntru = new NTRUEngine()) {

				NTRUEncryptionKeyPairGenerator ntruGen = this.CreateNtruGenerator();
				ntru.Init(true, publicKey);

				return ntru.ProcessBlock(message);
			}
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.parameters.Dispose();
			}
			this.IsDisposed = true;
		}

		~NtruEncryptor() {
			this.Dispose(false);
		}

	#endregion

	}
}