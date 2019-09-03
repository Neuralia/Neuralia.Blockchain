using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {
	/// <summary>
	///     perform hases of arrays.true defaults to Sha3
	/// </summary>
	public class Sha3Hasher : Hasher {
		public Sha3Hasher() : base(new Sha3ExternalDigest()) {

		}

		public Sha3Hasher(int bits) : base(new Sha3ExternalDigest(bits)) {

		}

		public override IByteArray Hash(IByteArray message) {
			Sha3ExternalDigest sha3Digest = (Sha3ExternalDigest) this.digest;

			sha3Digest.BlockUpdate(message);
			sha3Digest.DoFinalReturn(out IByteArray result);

			return result;
		}
	}
}