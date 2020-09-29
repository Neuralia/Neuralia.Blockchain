using System;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public static class KeyFactory {
		public static ICryptographicKey RehydrateKey(IDataRehydrator rehydrator) {

			return RehydrateKey<ICryptographicKey>(rehydrator);
		}

		
		public static T RehydrateKey<T>(IDataRehydrator rehydrator)
			where T : class, ICryptographicKey {

			ComponentVersion<CryptographicKeyType> version = rehydrator.RehydrateRewind<ComponentVersion<CryptographicKeyType>>();

			T cryptographicKey = CreateFromVersion<T>(version);

			cryptographicKey.Rehydrate(rehydrator);

			return cryptographicKey;
		}

		public static T CreateFromVersion<T>(ComponentVersion<CryptographicKeyType> version)
			where T : class, ICryptographicKey {
			if(version.Type == CryptographicKeyTypes.Instance.XMSS) {
				if(version == (1, 0)) {
					return new XmssCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.XMSSMT) {
				if(version == (1, 0)) {
					return new XmssmtCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.NTRUPrime) {
				if(version == (1, 0)) {
					return new NTRUPrimeCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.NTRU) {
				if(version == (1, 0)) {
					return new NTRUCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.MCELIECE) {
				if(version == (1, 0)) {
					return new McElieceCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.ECDSA) {
				if(version == (1, 0)) {
					return new TLSCertificate() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.Secret) {
				if(version == (1, 0)) {
					return new SecretCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.SecretCombo) {
				if(version == (1, 0)) {
					return new SecretComboCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.SecretDouble) {
				if(version == (1, 0)) {
					return new SecretDoubleCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.SecretPenta) {
				if(version == (1, 0)) {
					return new SecretPentaCryptographicKey() as T;
				}
			}
			else if(version.Type == CryptographicKeyTypes.Instance.TripleXMSS) {
				if(version == (1, 0)) {
					return new TripleXmssCryptographicKey() as T;
				}
			}
			
			throw new ApplicationException($"Invalid key type or version provided. {version}");
		}
		
		private static T CreateFromKey<T>(IKey walletKey)
			where T : class, ICryptographicKey {

			return CreateFromVersion<T>(walletKey.Version);
		}

		private static ICryptographicKey CreateFromKey(IKey walletKey) {

			return CreateFromKey<ICryptographicKey>(walletKey);
		}

		public static ICryptographicKey ConvertKey(IKey walletKey) {

			ICryptographicKey cryptographicKey = CreateFromKey(walletKey);
			cryptographicKey.SetFromKey(walletKey);

			return cryptographicKey;
		}
	}
}