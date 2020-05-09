using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Org.BouncyCastle.Crypto.Digests;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {
	/// <summary>
	///     perform hases of arrays.true defaults to Sha3
	/// </summary>
	public class Blake2Hasher : Hasher {

		public Blake2Hasher(int bits) : base(new Blake2bDigest(bits)) {

		}

		public override SafeArrayHandle Hash(SafeArrayHandle message) {
			Blake2bDigest blake2Digest = (Blake2bDigest) this.digest;

			blake2Digest.BlockUpdate(message.Bytes, message.Offset, message.Length);

			ByteArray result = ByteArray.Create(blake2Digest.GetDigestSize());
			blake2Digest.DoFinal(result.Bytes, result.Offset);

			return result;
		}
	}
}