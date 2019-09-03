using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public static class BlockchainDoubleHasher {
		public static bool VerifyGenesisHash(IGenesisBlock genesis, IByteArray data) {

			(IByteArray sha2, IByteArray sha3) hashes = HashingUtils.ExtractCombinedDualHash(data);

			return VerifyGenesisHash(genesis, hashes.sha2, hashes.sha3);
		}

		public static bool VerifyDigestHash(IBlockchainDigest digest, IByteArray data) {

			(IByteArray sha2, IByteArray sha3) hashes = HashingUtils.ExtractCombinedDualHash(data);

			return VerifyDigestHash(digest, hashes.sha2, hashes.sha3);
		}

		public static bool VerifyGenesisHash(IGenesisBlock genesis, IByteArray sha2, IByteArray sha3) {

			return HashingUtils.VerifyCombinedHash(genesis.Hash, sha2, sha3);
		}

		public static bool VerifyDigestHash(IBlockchainDigest digest, IByteArray sha2, IByteArray sha3) {

			return HashingUtils.VerifyCombinedHash(digest.Hash, sha2, sha3);
		}
	}
}