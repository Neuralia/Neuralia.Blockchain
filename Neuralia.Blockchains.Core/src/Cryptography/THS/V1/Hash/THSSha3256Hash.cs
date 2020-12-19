using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash {
	public class THSSha3256Hash : THSHashBase {

		private readonly Sha3ExternalDigest sha3 = new Sha3ExternalDigest(256);

		public override int HashType => 256;

		public override SafeArrayHandle Hash(SafeArrayHandle message) {

			this.sha3.BlockUpdate(message, 0, message.Length);
			this.sha3.DoFinalReturn(out ByteArray hash);

			return (SafeArrayHandle) hash;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.sha3?.Dispose();
		}
	}
}