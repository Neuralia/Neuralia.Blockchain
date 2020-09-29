using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash {
	public class POWSha2512Hash : POWHashBase {

		private readonly Sha512DotnetDigest sha2 = new Sha512DotnetDigest();
		
		public override SafeArrayHandle Hash(SafeArrayHandle message) {

			this.sha2.BlockUpdate(message, 0, message.Length);
			this.sha2.DoFinalReturn(out ByteArray hash);

			return (SafeArrayHandle)hash;
		}

		public override int HashType => 512;

		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.sha2?.Dispose();
		}
	}
}