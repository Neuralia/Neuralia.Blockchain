using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public static class SignatureUtils {
		public static ISecretDoubleCryptographicKey ConvertToSecretKey(ISecretBlockNextAccountSignature source, byte ordinalId) {
			ISecretDoubleCryptographicKey secretCryptographicKey = new SecretDoubleCryptographicKey();

			secretCryptographicKey.NextKeyHashSha2.Entry = source.NextKeyHashSha2.Entry;
			secretCryptographicKey.NextKeyHashSha3.Entry = source.NextKeyHashSha3.Entry;
			secretCryptographicKey.NonceHash = source.NonceHash;
			secretCryptographicKey.Id = ordinalId;

			secretCryptographicKey.SecondKey.SecurityCategory = source.NextSecondSecurityCategory;
			secretCryptographicKey.SecondKey.Key.Entry = source.NextSecondPublicKey.Entry;
			secretCryptographicKey.SecondKey.Id = ordinalId;

			return secretCryptographicKey;
		}

		public static SafeArrayHandle ConvertToDehydratedKey(ISecretBlockNextAccountSignature source, byte ordinalId) {
			ISecretDoubleCryptographicKey secretKey = ConvertToSecretKey(source, ordinalId);

			return ConvertToDehydratedKey(secretKey);
		}

		public static SafeArrayHandle ConvertToDehydratedKey(ISecretDoubleCryptographicKey secretKey) {

			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {
				if(!secretKey.NextKeyHashSha2.HasData || !secretKey.NextKeyHashSha3.HasData || !secretKey.SecondKey.Key.HasData) {
					int fdsds = 0;

					fdsds += 2;
					
					

				}
				secretKey.Dehydrate(dehydrator);
				return dehydrator.ToArray();
			}
		}
	}
}