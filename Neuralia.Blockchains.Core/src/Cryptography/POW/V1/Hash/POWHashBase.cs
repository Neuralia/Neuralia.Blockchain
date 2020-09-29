using System;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash {
	public abstract class POWHashBase : IPOWHash {
	
		public abstract SafeArrayHandle Hash(SafeArrayHandle message);
		public abstract int HashType { get; }

	#region Dispose

		public bool IsDisposed { get; private set; }
		

		protected virtual void DisposeAll() {
			
		}
		
		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				try {
					this.DisposeAll();
				} catch(Exception ex) {
					NLog.Default.Error(ex, "failed to dispose");
				}
			}

			this.IsDisposed = true;
		}

		~POWHashBase() {
			this.Dispose(false);
		}

	#endregion
		
	}
}