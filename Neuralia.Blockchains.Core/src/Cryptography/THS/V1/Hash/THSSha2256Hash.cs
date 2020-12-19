using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash {
	public class THSSha2256Hash : THSHashBase {

		private readonly Sha256DotnetDigest sha2 = new Sha256DotnetDigest();

		public override int HashType => 256;

		public override SafeArrayHandle Hash(SafeArrayHandle message) {

			this.sha2.BlockUpdate(message, 0, message.Length);
			this.sha2.DoFinalReturn(out ByteArray hash);

			return (SafeArrayHandle) hash;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.sha2?.Dispose();
		}
	}
}