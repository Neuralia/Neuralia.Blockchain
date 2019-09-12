using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public class BlockchainHashingUtils {

		public static (IByteArray sha2, IByteArray sha3) HashSecretKey(ISecretWalletKey secretWalletKey) {

			return HashingUtils.HashSecretKey(secretWalletKey.PublicKey);
		}

		public static (IByteArray sha2, IByteArray sha3, int nonceHash) HashSecretComboKey(ISecretComboWalletKey secretWalletKey) {

			return HashingUtils.HashSecretComboKey(secretWalletKey.PublicKey, secretWalletKey.PromisedNonce1, secretWalletKey.PromisedNonce2);
		}

		public static IByteArray GenerateBlockHash(IBlock block, IByteArray previousBlockHash) {

			return HashingUtils.Hasher3.Hash(block.GetStructuresArray(previousBlockHash));
		}
	}
}