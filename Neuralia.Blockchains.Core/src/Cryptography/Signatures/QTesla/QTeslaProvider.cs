using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Neuralia.BouncyCastle.extra.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Signatures.QTesla {
	public class QTeslaProvider : SignatureProviderBase {

		private readonly QTESLASecurityCategory.SecurityCategories securityCategory;

		public QTeslaProvider(QTESLASecurityCategory.SecurityCategories securityCategory) {
			this.securityCategory = securityCategory;
		}

		public QTeslaProvider(byte securityCategory) : this((QTESLASecurityCategory.SecurityCategories) securityCategory) {

		}

		public override void Reset() {

		}

		public (SafeArrayHandle privateKey, SafeArrayHandle publicKey) GenerateKeys() {

			(SafeArrayHandle privateKey, SafeArrayHandle publicKey) results = default;

			//TODO: fix this issue in qTesla! see test to reproduce:  (it seems to loop infinitely, eventually int overflows into negative size)
			// SecureRandom random = DeterministicRandomSourceProvider.GetDeterministicRandom();
			//
			// for(int i = 0; i < 10000; i++) {
			//
			// 	QTESLAKeyGenerationParameters parameters = new QTESLAKeyGenerationParameters(QTESLASecurityCategory.SecurityCategories.PROVABLY_SECURE_III, random);
			// 	QTESLAKeyPairGenerator gen = new QTESLAKeyPairGenerator();
			// 	gen.init(parameters);
			// 	gen.generateKeyPair();
			// 	Console.WriteLine(i);
			//
			// }

			// repeat because sometimes it bugs in sample for Provable SECURITY III (in: Sample.polynomialGaussSamplerIIIP)
			Repeater.Repeat(() => {

				QTESLAKeyPairGenerator kpGen = new QTESLAKeyPairGenerator();

				kpGen.init(new QTESLAKeyGenerationParameters(this.securityCategory, new BetterSecureRandom()));

				AsymmetricCipherKeyPair kp = kpGen.generateKeyPair();

				results = (((QTESLAPrivateKeyParameters) kp.Private).Dehydrate(), ((QTESLAPublicKeyParameters) kp.Public).Dehydrate());
			});

			return results;
		}

		public SafeArrayHandle Sign(SafeArrayHandle content, SafeArrayHandle privateKey) {

			QTESLASigner signer = new QTESLASigner();

			signer.init(true, new ParametersWithRandom(QTESLAPrivateKeyParameters.Rehydrate(privateKey), new BetterSecureRandom()));

			return ByteArray.WrapAndOwn(signer.generateSignature(content.ToExactByteArray()));
		}

		public override bool Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {
			QTESLASigner signer = new QTESLASigner();

			signer.init(false, QTESLAPublicKeyParameters.Rehydrate(publicKey));

			return signer.verifySignature(message.ToExactByteArray(), signature.ToExactByteArray());

		}
	}
}