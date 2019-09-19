using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public class BlockchainHashingUtils {

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3) HashSecretKey(ISecretWalletKey secretWalletKey) {

			return HashingUtils.HashSecretKey(secretWalletKey.PublicKey);
		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) HashSecretComboKey(ISecretComboWalletKey secretWalletKey) {

			return HashingUtils.HashSecretComboKey(secretWalletKey.PublicKey, secretWalletKey.PromisedNonce1, secretWalletKey.PromisedNonce2);
		}

		public static SafeArrayHandle GenerateBlockHash(IBlock block, SafeArrayHandle previousBlockHash) {

			using(HashNodeList structures = block.GetStructuresArray(previousBlockHash)) {
				return HashingUtils.Hash3(structures);
			}
		}
		
		public static SafeArrayHandle GenerateGenesisBlockHash(IGenesisBlock genesisBlock) {

			using(HashNodeList structures = genesisBlock.GetStructuresArray(SafeArrayHandle.Create())) {
				return HashingUtils.Hash3(structures);
			}
		}
	}
}