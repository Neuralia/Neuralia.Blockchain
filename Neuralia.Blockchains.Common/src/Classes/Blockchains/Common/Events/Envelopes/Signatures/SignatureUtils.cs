using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public static class SignatureUtils {
		public static ISecretDoubleCryptographicKey ConvertToSecretKey(ISecretBlockNextAccountSignature source, byte ordinalId) {
			ISecretDoubleCryptographicKey secretCryptographicKey = new SecretDoubleCryptographicKey();

			secretCryptographicKey.NextKeyHashSha2 = source.NextKeyHashSha2;
			secretCryptographicKey.NextKeyHashSha3 = source.NextKeyHashSha3;
			secretCryptographicKey.NonceHash = source.NonceHash;
			secretCryptographicKey.Id = ordinalId;

			secretCryptographicKey.SecondKey.SecurityCategory = source.NextSecondSecurityCategory;
			secretCryptographicKey.SecondKey.Key = source.NextSecondPublicKey;

			return secretCryptographicKey;
		}

		public static IByteArray ConvertToDehydratedKey(ISecretBlockNextAccountSignature source, byte ordinalId) {
			ISecretDoubleCryptographicKey secretKey = ConvertToSecretKey(source, ordinalId);

			return ConvertToDehydratedKey(secretKey);
		}

		public static IByteArray ConvertToDehydratedKey(ISecretDoubleCryptographicKey secretKey) {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			secretKey.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}
	}
}