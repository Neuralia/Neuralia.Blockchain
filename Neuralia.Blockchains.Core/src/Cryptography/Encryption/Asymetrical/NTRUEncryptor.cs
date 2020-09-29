// using System;
// using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
// using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
// using Neuralia.Blockchains.Tools;
// using Neuralia.Blockchains.Tools.Data;
// using Neuralia.Blockchains.Tools.Serialization;
// using Neuralia.BouncyCastle.extra.pqc.crypto.ntru;
// using Org.BouncyCastle.Crypto;
//
// namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
// 	public class NTRUEncryptor : IDisposableExtended {
//
// 		protected NTRUEncryptionKeyGenerationParameters parameters;
//
// 		public NTRUEncryptor() {
//
// 			IHashDigest Digest256Generator() {
// 				return new Sha3ExternalDigest(256);
// 			}
//
// 			IHashDigest Digest512Generator() {
// 				return new Sha3ExternalDigest(512);
// 			}
//
// 			NTRUEncryptionKeyGenerationParameters.NTRUEncryptionKeyGenerationParametersTypes type = NTRUEncryptionKeyGenerationParameters.NTRUEncryptionKeyGenerationParametersTypes.EES1499EP1_EXT;
//
// 			this.parameters = NTRUEncryptionKeyGenerationParameters.CreateNTRUEncryptionKeyGenerationParameters(type, Digest256Generator, Digest512Generator);
//
// 			this.parameters.fastFp = true;
// 		}
//
// 		protected NTRUEncryptionKeyPairGenerator CreateNTRUGenerator() {
// 			return new NTRUEncryptionKeyPairGenerator(this.parameters);
// 		}
//
// 		public SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle publicKey) {
//
// 			return this.Encrypt(message, new NTRUEncryptionPublicKeyParameters(publicKey, this.parameters.EncryptionParameters));
// 		}
//
// 		public SafeArrayHandle Encrypt(SafeArrayHandle message, NTRUEncryptionPublicKeyParameters publicKey) {
// 			using(NTRUEngine ntru = new NTRUEngine()) {
//
// 				ntru.Init(true, publicKey);
//
// 				return ntru.ProcessBlock(message);
// 			}
// 		}
//
// 		public NTRUAsymmetricCipherKeyPair GenerateKeyPair() {
// 			NTRUEncryptionKeyPairGenerator ntruGen = this.CreateNTRUGenerator();
//
// 			return ntruGen.GenerateKeyPair();
// 		}
//
// 		public SafeArrayHandle Decrypt(SafeArrayHandle cryptedMessage, SafeArrayHandle privateKey) {
//
// 			return this.Decrypt(cryptedMessage, new NTRUEncryptionPrivateKeyParameters(privateKey, this.parameters.EncryptionParameters));
// 		}
//
// 		public SafeArrayHandle Decrypt(SafeArrayHandle cryptedMessage, NTRUEncryptionPrivateKeyParameters privateKey) {
// 			using(NTRUEngine ntru = new NTRUEngine()) {
//
// 				NTRUEncryptionKeyPairGenerator ntruGen = this.CreateNTRUGenerator();
// 				ntru.Init(false, privateKey);
//
// 				return ntru.ProcessBlock(cryptedMessage);
// 			}
// 		}
//
//
// 	#region disposable
//
// 		public bool IsDisposed { get; private set; }
//
// 		public void Dispose() {
// 			this.Dispose(true);
// 			GC.SuppressFinalize(this);
// 		}
//
// 		protected virtual void Dispose(bool disposing) {
//
// 			if(disposing && !this.IsDisposed) {
// 				this.parameters.Dispose();
// 			}
//
// 			this.IsDisposed = true;
// 		}
//
// 		~NTRUEncryptor() {
// 			this.Dispose(false);
// 		}
//
// 	#endregion
//
// 	}
// }