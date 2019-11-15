using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Org.BouncyCastle.Security;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.Signatures {
	public abstract class SignatureProviderBase : IDisposableExtended {

		public SignatureProviderBase() {
		}

		public bool IsDisposed { get; protected set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     ALERT!!!!  this is a ULTRA dangerous method. only call with extreme caution!!
		/// </summary>
		public static void ClearMemoryAllocators() {
			Log.Warning("Recovering the memory allocators. This is very dangerous, use with EXTREME care!!");
			ByteArray.RecoverLeakedMemory();
			Log.Warning("----------------------------------------------------------------------------------");
		}

		public virtual void Initialize() {
			this.Reset();

		}

		public abstract void Reset();

		protected SecureRandom GetRandom() {

			SecureRandom keyRandom = new SecureRandom();

			using(RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider()) {
				using(ByteArray seed = ByteArray.Create(4096)) {
					provider.GetBytes(seed.Bytes, seed.Offset, seed.Length);

					keyRandom.SetSeed(seed.ToExactByteArrayCopy());

					return keyRandom;
				}
			}
		}

		public abstract bool Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey);

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