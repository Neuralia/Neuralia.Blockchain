using System;
using Neuralia.Blockchains.Core.Cryptography.SHA3;

namespace Neuralia.Blockchains.Core.Cryptography.crypto.digests {
	public class Sha3ExternalDigest : ShaDigestBase {
		private readonly int bitLength;

		public Sha3ExternalDigest() : this(256) {

		}

		public Sha3ExternalDigest(int bitLength) {
			this.bitLength = bitLength;

			switch(bitLength) {
				case 256:
					this.sha = new SHA3256Managed();

					break;
				case 512:
					this.sha = new SHA3512Managed();

					break;
				default:

					throw new ApplicationException("Invalid bit length");
			}

			this.Sha3.UseKeccakPadding = false;
		}

		private SHA3.SHA3 Sha3 => (SHA3.SHA3) this.sha;

		public override string AlgorithmName => $"SHA3-{this.bitLength}";

		public override int GetDigestSize() {
			return this.sha.HashSize >> 3;
		}

		public override int GetByteLength() {
			throw new NotImplementedException();
		}
	}
}