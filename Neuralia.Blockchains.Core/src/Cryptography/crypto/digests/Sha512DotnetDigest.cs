using System.Security.Cryptography;

namespace Neuralia.Blockchains.Core.Cryptography.crypto.digests {

	/// <summary>
	///     Use the much faster .NET implementation
	/// </summary>
	public class Sha512DotnetDigest : ShaDigestBase {

		public Sha512DotnetDigest() {
			this.sha = SHA512.Create();
			this.DigestLength = 64;
		}

		public override string AlgorithmName => "SHA-512";
	}
}