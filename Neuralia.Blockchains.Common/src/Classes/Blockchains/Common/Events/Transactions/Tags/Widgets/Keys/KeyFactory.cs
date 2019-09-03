using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public static class KeyFactory {
		public static ICryptographicKey RehydrateKey(IDataRehydrator rehydrator) {

			return RehydrateKey<ICryptographicKey>(rehydrator);
		}

		public static T RehydrateKey<T>(IDataRehydrator rehydrator)
			where T : class, ICryptographicKey {

			T cryptographicKey = null;
			Enums.KeyTypes keyType = (Enums.KeyTypes) rehydrator.ReadByte();
			byte version = rehydrator.ReadByte();

			switch(keyType) {
				case Enums.KeyTypes.XMSS:
					if(version == 1) {
						cryptographicKey = new XmssCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.XMSSMT:
					if(version == 1) {
						cryptographicKey = new XmssmtCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.NTRU:
					if(version == 1) {
						cryptographicKey = new NtruCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.Secret:
					if(version == 1) {
						cryptographicKey = new SecretCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.SecretCombo:
					if(version == 1) {
						cryptographicKey = new SecretComboCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.SecretDouble:
					if(version == 1) {
						cryptographicKey = new SecretDoubleCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.SecretPenta:
					if(version == 1) {
						cryptographicKey = new SecretPentaCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.MCELIECE:
					if(version == 1) {
						cryptographicKey = new McElieceCryptographicKey() as T;
					}

					break;

				case Enums.KeyTypes.ECDSA:
					if(version == 1) {
						cryptographicKey = new TLSCertificate() as T;
					}

					break;

				case Enums.KeyTypes.RSA:
					if(version == 1) {
						cryptographicKey = new TLSCertificate() as T;
					}

					break;

				default:

					throw new ApplicationException("Invalid key type provided.");
			}

			byte id = rehydrator.ReadByte();

			cryptographicKey.Rehydrate(id, rehydrator);

			return cryptographicKey;
		}
	}
}