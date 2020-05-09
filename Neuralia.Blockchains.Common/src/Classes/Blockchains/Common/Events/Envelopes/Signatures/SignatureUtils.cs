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

		public static IXmssCryptographicKey ConvertToXmssMTKey(IXmssBlockNextAccountSignature source, byte ordinalId) {
			IXmssCryptographicKey secretCryptographicKey = new XmssCryptographicKey();

			secretCryptographicKey.TreeHeight = source.TreeHeight;
			secretCryptographicKey.BitSize = source.HashBits;
			secretCryptographicKey.Key.Entry = source.PublicKey.Entry;

			return secretCryptographicKey;
		}

		public static SafeArrayHandle ConvertToDehydratedKey(ISecretBlockNextAccountSignature source, byte ordinalId) {
			ISecretDoubleCryptographicKey secretKey = ConvertToSecretKey(source, ordinalId);

			return ConvertToDehydratedKey(secretKey);
		}

		public static SafeArrayHandle ConvertToDehydratedKey(ISecretDoubleCryptographicKey secretKey) {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			secretKey.Dehydrate(dehydrator);

			return dehydrator.ToArray();

		}

		public static SafeArrayHandle ConvertToDehydratedKey(IXmssCryptographicKey secretKey) {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			secretKey.Dehydrate(dehydrator);

			return dehydrator.ToArray();

		}
	}
}