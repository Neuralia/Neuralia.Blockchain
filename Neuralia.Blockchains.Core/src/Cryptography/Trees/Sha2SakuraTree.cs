using System.Security.Cryptography;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public class Sha2SakuraTree : SakuraTree {
		private readonly HashAlgorithm sha2;

		public Sha2SakuraTree() : this(512) {

		}

		public Sha2SakuraTree(int digestBitLength) {
			if(digestBitLength == 256) {
				this.sha2 = SHA256.Create();
			}

			if(digestBitLength == 512) {
				this.sha2 = SHA512.Create();
			}
		}

		protected override IByteArray GenerateHash(IByteArray entry) {
			return (ByteArray) this.sha2.ComputeHash(entry.Bytes, entry.Offset, entry.Length);
		}
	}
}