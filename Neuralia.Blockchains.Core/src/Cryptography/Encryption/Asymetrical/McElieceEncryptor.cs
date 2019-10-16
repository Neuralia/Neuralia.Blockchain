using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using org.bouncycastle.pqc.crypto.mceliece;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
	public class McElieceEncryptor : IDisposable2 {

		public enum McElieceCipherModes : byte {
			McEliece = 1,
			Pointcheval = 2,
			Fujisaki = 3,
			KobaraImai = 4
		}

		public enum McElieceHashModes : byte {
			Sha256 = 1,
			Sha512 = 2,
			Sha3_256 = 3,
			Sha3_512 = 4
		}

		public const int DEFAULT_M = 13;
		public const int DEFAULT_T = 160;
		public const McElieceHashModes DEFAULT_HASH_MODE = McElieceHashModes.Sha3_512;
		public const McElieceCipherModes DEFAULT_CIPHER_MODE = McElieceCipherModes.KobaraImai;

		public SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle publicKey, McElieceCipherModes cipherMode) {

			McElieceCCA2PublicKeyParameters pub = new McElieceCCA2PublicKeyParameters();
			pub.Rehydrate(DataSerializationFactory.CreateRehydrator(publicKey));

			ParametersWithRandom parameters = new ParametersWithRandom(pub, new SecureRandom());

			IMcElieceCipher mcElieceCipher = this.GetCypher(cipherMode);
			mcElieceCipher.init(true, parameters);

			return mcElieceCipher.messageEncrypt(message);
		}

		protected IMcElieceCipher GetCypher(McElieceCipherModes cipherMode) {
			switch(cipherMode) {
				case McElieceCipherModes.McEliece:

					return new McElieceCipher();

				case McElieceCipherModes.Pointcheval:

					return new McEliecePointchevalCipher(DigestCreator);

				case McElieceCipherModes.Fujisaki:

					return new McElieceFujisakiCipher(DigestCreator);

				case McElieceCipherModes.KobaraImai:

					return new McElieceKobaraImaiCipher(DigestCreator);

				default:
					throw new NotSupportedException();
			}
		}

		public static IDigest DigestCreator(string algo) {
			switch(algo) {
				case Utils.SHA2_256:
					return new Sha256DotnetDigest();
				case Utils.SHA2_512:
					return new Sha512DotnetDigest();
				case Utils.SHA3_256:
					return new Sha3ExternalDigest(256);
				case Utils.SHA3_512:
					return new Sha3ExternalDigest(512);
				default:
					throw new ArgumentException();
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

			}
			this.IsDisposed = true;
		}

		~McElieceEncryptor() {
			this.Dispose(false);
		}

	#endregion

	}
}