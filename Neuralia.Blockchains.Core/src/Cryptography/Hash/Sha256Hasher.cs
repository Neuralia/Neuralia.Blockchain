using Neuralia.Blockchains.Core.Cryptography.crypto.digests;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {
	public class Sha256Hasher : Hasher {
		public Sha256Hasher() : base(new Sha256DotnetDigest()) {

		}
	}
}