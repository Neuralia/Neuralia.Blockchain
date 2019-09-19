using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public class Sha3SakuraTree : SakuraTree , IDisposable2{
		

		private readonly Sha3ExternalDigest digest;
		
		public Sha3SakuraTree() : this(512) {
			
		}

		public Sha3SakuraTree(int digestBitLength) {
			this.digest = new Sha3ExternalDigest(digestBitLength);
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle hopeBytes) {
			
			this.digest.BlockUpdate(hopeBytes);
			this.digest.DoFinalReturn(out SafeArrayHandle hash);

			return hash;
		}
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			

			if(disposing && !this.IsDisposed) {

				this.digest?.Dispose();
			}
			
			this.IsDisposed = true;
		}

		~Sha3SakuraTree() {
			this.Dispose(false);
		}

	#endregion
	}
}