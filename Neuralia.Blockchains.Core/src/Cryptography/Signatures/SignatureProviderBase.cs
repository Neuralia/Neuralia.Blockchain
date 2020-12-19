using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.BouncyCastle.extra.Security;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Signatures {
	public abstract class SignatureProviderBase : IDisposableExtended {

		public bool IsDisposed { get; protected set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     ALERT!!!!  this is a ULTRA dangerous method. only call with extreme caution!!
		/// </summary>
		public static void ClearMemoryAllocators() {
			NLog.Default.Warning("Recovering the memory allocators. This is very dangerous, use with EXTREME care!!");
			ByteArray.RecoverLeakedMemory();
			NLog.Default.Warning("----------------------------------------------------------------------------------");
		}

		public virtual void Initialize() {
			this.Reset();

		}

		public abstract void Reset();

		protected SecureRandom GetRandom() {

			return new BetterSecureRandom();
		}

		public abstract Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey);

		private void Dispose(bool disposing) {

			if(!this.IsDisposed && disposing) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {

		}

		~SignatureProviderBase() {
			this.Dispose(false);
		}
	}

}