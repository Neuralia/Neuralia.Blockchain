using Neuralia.Blockchains.Core.Cryptography.crypto.digests;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {
	public class Sha512Hasher : Hasher {
		public Sha512Hasher() : base(new Sha512DotnetDigest()) {

		}
	}
}