using System.Security.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {

	public class Sha2SakuraTree : SakuraTree<HashAlgorithm> {

		private readonly int digestBitLength;

		public Sha2SakuraTree(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : this(512, threadMode) {

		}

		public Sha2SakuraTree(int digestBitLength, Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : base(threadMode) {
			this.digestBitLength = digestBitLength;
		}

		protected override HashAlgorithm DigestFactory() {
			if(this.digestBitLength == 256) {
				return SHA256.Create();
			}

			if(this.digestBitLength == 512) {
				return SHA512.Create();
			}

			return null;
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry, HashAlgorithm hasher) {
			return SafeArrayHandle.WrapAndOwn(hasher.ComputeHash(entry.Bytes, entry.Offset, entry.Length));
		}
	}
}