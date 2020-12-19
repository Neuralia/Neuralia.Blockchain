using System;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto {
	public abstract class THSCryptoBase : ITHSCrypto {

		public abstract void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted);

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

		~THSCryptoBase() {
			this.Dispose(false);
		}

	#endregion

	}
}