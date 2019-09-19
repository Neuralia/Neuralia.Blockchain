using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	
	public class Sha2SakuraTree : SakuraTree, IDisposable2 {

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

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry) {
			return (ByteArray) this.sha2.ComputeHash(entry.Bytes, entry.Offset, entry.Length);
		}
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			

			if(disposing && !this.IsDisposed) {

				this.sha2?.Dispose();
			}
			
			this.IsDisposed = true;
		}

		~Sha2SakuraTree() {
			this.Dispose(false);
		}

	#endregion
	}
}