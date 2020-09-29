using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {
	/// <summary>
	///     perform hases of arrays.true defaults to Sha3
	/// </summary>
	public class Sha3Hasher : Hasher {
		public Sha3Hasher() : base(new Sha3ExternalDigest()) {

		}

		public Sha3Hasher(int bits) : base(new Sha3ExternalDigest(bits)) {

		}

		public override SafeArrayHandle Hash(SafeArrayHandle message) {
			Sha3ExternalDigest sha3Digest = (Sha3ExternalDigest) this.digest;

			sha3Digest.BlockUpdate(message);
			sha3Digest.DoFinalReturn2(out ByteArray result);

			return (SafeArrayHandle)result;
		}
	}
}