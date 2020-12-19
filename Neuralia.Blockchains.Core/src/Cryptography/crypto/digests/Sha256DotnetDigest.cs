using System.Security.Cryptography;

namespace Neuralia.Blockchains.Core.Cryptography.crypto.digests {

	/// <summary>
	///     Use the much faster .NET implementation
	/// </summary>
	public class Sha256DotnetDigest : ShaDigestBase {

		public Sha256DotnetDigest() {
			this.sha = SHA256.Create();
			this.DigestLength = 32;
		}

		public override string AlgorithmName => "SHA-256";
	}
}