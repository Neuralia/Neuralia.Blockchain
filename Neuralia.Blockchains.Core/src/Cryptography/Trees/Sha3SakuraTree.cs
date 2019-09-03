using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public class Sha3SakuraTree : SakuraTree {
		private readonly int digestBitLength;

		public Sha3SakuraTree() : this(512) {

		}

		public Sha3SakuraTree(int digestBitLength) {
			this.digestBitLength = digestBitLength;
		}

		protected override IByteArray GenerateHash(IByteArray hopeBytes) {

			using(Sha3ExternalDigest digest = new Sha3ExternalDigest(this.digestBitLength)) {

				digest.BlockUpdate(hopeBytes);
				digest.DoFinalReturn(out IByteArray hash);

				return hash;
			}
		}
	}
}