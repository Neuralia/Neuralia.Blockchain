using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public class Sha3SakuraTree : SakuraTree<Sha3ExternalDigest> {

		private readonly int digestBitLength;

		public Sha3SakuraTree(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : this(512, threadMode) {

		}

		public Sha3SakuraTree(int digestBitLength, Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : base(threadMode) {
			this.digestBitLength = digestBitLength;
		}

		protected override Sha3ExternalDigest DigestFactory() {

			return new Sha3ExternalDigest(this.digestBitLength);

			;
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle hopeBytes, Sha3ExternalDigest hasher) {

			hasher.BlockUpdate(hopeBytes);
			hasher.DoFinalReturn2(out ByteArray hash);

			return (SafeArrayHandle)hash;
		}
	}
}