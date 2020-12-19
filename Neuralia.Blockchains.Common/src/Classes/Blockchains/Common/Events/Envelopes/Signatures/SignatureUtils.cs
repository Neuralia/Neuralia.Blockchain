using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public static class SignatureUtils {


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