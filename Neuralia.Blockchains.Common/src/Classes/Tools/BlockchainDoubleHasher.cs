using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public static class BlockchainDoubleHasher {
		public static bool VerifyGenesisHash(IGenesisBlock genesis, SafeArrayHandle data) {

			(SafeArrayHandle sha2, SafeArrayHandle sha3) hashes = HashingUtils.ExtractCombinedDualHash(data);

			return VerifyGenesisHash(genesis, hashes.sha2, hashes.sha3);
		}

		public static bool VerifyDigestHash(IBlockchainDigest digest, SafeArrayHandle data) {

			(SafeArrayHandle sha2, SafeArrayHandle sha3) hashes = HashingUtils.ExtractCombinedDualHash(data);

			return VerifyDigestHash(digest, hashes.sha2, hashes.sha3);
		}

		public static  (SafeArrayHandle sha2, SafeArrayHandle sha3) GetCombinedHash(IGenesisBlock genesis, SafeArrayHandle sha2, SafeArrayHandle sha3) {

			return HashingUtils.GetCombinedHash(genesis.Hash, sha2, sha3);
		}
		
		public static bool VerifyGenesisHash(IGenesisBlock genesis, SafeArrayHandle sha2, SafeArrayHandle sha3) {

			return HashingUtils.VerifyCombinedHash(genesis.Hash, sha2, sha3);
		}

		public static bool VerifyGenesisHash(IGenesisBlock genesis, SafeArrayHandle sha2, SafeArrayHandle sha3, SafeArrayHandle genesisSha2, SafeArrayHandle genesisSha3) {

			return HashingUtils.VerifyCombinedHash(genesis.Hash, sha2, sha3, genesisSha2, genesisSha3);
		}
		
		public static bool VerifyDigestHash(IBlockchainDigest digest, SafeArrayHandle sha2, SafeArrayHandle sha3) {

			return HashingUtils.VerifyCombinedHash(digest.Hash, sha2, sha3);
		}
	}
}