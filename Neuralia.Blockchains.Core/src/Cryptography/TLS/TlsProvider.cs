using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.security;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Neuralia.Blockchains.Core.Cryptography.TLS {
	public class TlsProvider {

		public enum HashStrength {
			Sha256,
			Sha512
		}

		public readonly string ALGO;

		public readonly int keyStrength; // 8192?

		public TlsProvider(int keyStrength = 4096, HashStrength hashStrength = HashStrength.Sha512) {
			this.keyStrength = keyStrength;
			int amount = 256;

			if(hashStrength == HashStrength.Sha512) {
				amount = 512;
			}

			this.ALGO = $"SHA{amount}WITHRSA";
		}

		public X509Certificate2 RebuildCertificate(IByteArray data) {
			return new X509Certificate2(data.ToExactByteArray());
		}

		public bool VerifyHash(IByteArray message, IByteArray signature, X509Certificate2 certificate) {

			Sha3_512Hasher hasher = new Sha3_512Hasher();

			IByteArray hash = hasher.Hash(message);

			using(RSA csp = certificate.GetRSAPublicKey()) {
				// verify the hash

				bool result = csp.VerifyHash(hash.ToExactByteArray(), signature.ToExactByteArray(), HashAlgorithmName.SHA512, RSASignaturePadding.Pss);

				hash.Return();

				return result;
			}
		}

		public bool VerifyData(IByteArray message, IByteArray signature, X509Certificate2 certificate) {

			using(RSA csp = certificate.GetRSAPublicKey()) {
				bool result = csp.VerifyData(message.ToExactByteArray(), signature.ToExactByteArray(), HashAlgorithmName.SHA512, RSASignaturePadding.Pss);

				return result;
			}
		}

		public IByteArray Encrypt(IByteArray message, X509Certificate2 certificate) {
			using(RSA csp = certificate.GetRSAPublicKey()) {
				// Sign the hash
				return (ByteArray) csp.Encrypt(message.ToExactByteArray(), RSAEncryptionPadding.OaepSHA512);
			}
		}

		public (X509Certificate2 rootCertificate, X509Certificate2 localCertificate) Build() {

			AsymmetricKeyParameter myCAprivateKey = null;

			//generate a root CA cert and obtain the privateKey
			X509Certificate2 MyRootCAcert = this.GenerateCACertificate("CN=Neuralium", ref myCAprivateKey);

			//generate cert based on the CA cert privateKey
			X509Certificate2 MyCert = this.GenerateSelfSignedCertificate("CN=127.0.01", "CN=Neuralium", myCAprivateKey);

			return (MyRootCAcert, MyCert);
		}

		public X509Certificate2 GenerateSelfSignedCertificate(string subjectName, string issuerName, AsymmetricKeyParameter issuerPrivKey) {
			// Generating Random Numbers
			CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
			SecureRandom random = new SecureRandom(randomGenerator);

			// The Certificate Generator
			X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

			// Serial Number
			BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
			certificateGenerator.SetSerialNumber(serialNumber);

			// Signature Algorithm
			certificateGenerator.SetSignatureAlgorithm(this.ALGO);

			// Issuer and Subject Name
			X509Name subjectDN = new X509Name(subjectName);
			X509Name issuerDN = new X509Name(issuerName);
			certificateGenerator.SetIssuerDN(issuerDN);
			certificateGenerator.SetSubjectDN(subjectDN);

			// Valid For
			DateTime notBefore = DateTime.UtcNow.Date;
			DateTime notAfter = notBefore.AddYears(2);

			certificateGenerator.SetNotBefore(notBefore);
			certificateGenerator.SetNotAfter(notAfter);

			// Subject Public Key
			KeyGenerationParameters keyGenerationParameters = new KeyGenerationParameters(random, this.keyStrength);
			RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
			keyPairGenerator.Init(keyGenerationParameters);
			AsymmetricCipherKeyPair subjectKeyPair = keyPairGenerator.GenerateKeyPair();

			certificateGenerator.SetPublicKey(subjectKeyPair.Public);

			// Generating the Certificate

			// selfsign certificate
			X509Certificate certificate = certificateGenerator.Generate(new Asn1SignatureFactory(this.ALGO, issuerPrivKey, random));

			// correcponding private key
			PrivateKeyInfo info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);

			// merge into X509Certificate2
			X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded());

			Asn1Sequence seq = (Asn1Sequence) Asn1Object.FromByteArray(info.ParsePrivateKey().GetDerEncoded());

			if(seq.Count != 9) {
				//throw new PemException("malformed sequence in RSA private key");
			}

			RsaPrivateKeyStructure rsa = new RsaPrivateKeyStructure(seq);
			RsaPrivateCrtKeyParameters rsaparams = new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent, rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);

#if (NETSTANDARD2_0)

			//TODO: do it with bouncycastle (DotNetUtilities.)
			throw new NotImplementedException();
#elif (NETCOREAPP2_2)
			return x509.CopyWithPrivateKey(DotNetUtilitiesExtensions.ToRSA(rsaparams));
#else
	throw new NotImplementedException();
#endif

		}

		public X509Certificate2 GenerateCACertificate(string subjectName, ref AsymmetricKeyParameter CaPrivateKey) {
			// Generating Random Numbers
			CryptoApiRandomGenerator randomGenerator = new CryptoApiRandomGenerator();
			SecureRandom random = new SecureRandom(randomGenerator);

			// The Certificate Generator
			X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

			// Serial Number
			BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
			certificateGenerator.SetSerialNumber(serialNumber);

			// Issuer and Subject Name
			X509Name subjectDN = new X509Name(subjectName);
			X509Name issuerDN = subjectDN;
			certificateGenerator.SetIssuerDN(issuerDN);
			certificateGenerator.SetSubjectDN(subjectDN);

			// Valid For
			DateTime notBefore = DateTime.UtcNow.Date;
			DateTime notAfter = notBefore.AddYears(2);

			certificateGenerator.SetNotBefore(notBefore);
			certificateGenerator.SetNotAfter(notAfter);

			// Subject Public Key
			KeyGenerationParameters keyGenerationParameters = new KeyGenerationParameters(random, this.keyStrength);
			RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
			keyPairGenerator.Init(keyGenerationParameters);
			AsymmetricCipherKeyPair subjectKeyPair = keyPairGenerator.GenerateKeyPair();

			certificateGenerator.SetPublicKey(subjectKeyPair.Public);

			// Generating the Certificate
			AsymmetricCipherKeyPair issuerKeyPair = subjectKeyPair;

			// selfsign certificate
			X509Certificate certificate = certificateGenerator.Generate(new Asn1SignatureFactory(this.ALGO, issuerKeyPair.Private, random));
			X509Certificate2 x509 = new X509Certificate2(certificate.GetEncoded());

			CaPrivateKey = issuerKeyPair.Private;

			return x509;
		}
	}
}