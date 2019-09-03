using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Org.BouncyCastle.Security;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.Signatures {
	public abstract class SignatureProviderBase : IDisposable2 {

		public SignatureProviderBase() {
		}

		public SignatureProviderBase(IByteArray privateKey, IByteArray publicKey) {

			// make copies

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
			MemoryAllocators.Instance.cryptoAllocator.RecoverLeakedMemory();
			MemoryAllocators.Instance.doubleArrayCryptoAllocator.RecoverLeakedMemory();
			Log.Warning("----------------------------------------------------------------------------------");
		}

		public virtual void Initialize() {
			this.Reset();

		}

		public abstract void Reset();

		protected SecureRandom GetRandom() {

			SecureRandom keyRandom = new SecureRandom();

			using(RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider()) {
				using(ByteArray seed = new ByteArray(4096)) {
					provider.GetBytes(seed.Bytes, seed.Offset, seed.Length);

					keyRandom.SetSeed(seed.ToExactByteArrayCopy());

					return keyRandom;
				}
			}
		}

		public abstract bool Verify(IByteArray message, IByteArray signature, IByteArray publicKey);

		private void Dispose(bool disposing) {

			if(!this.IsDisposed) {
				try {
					if(disposing) {
					}

					this.DisposeAll(disposing);

				} finally {
					this.IsDisposed = true;
				}
			}

		}

		protected virtual void DisposeAll(bool disposing) {

		}

		~SignatureProviderBase() {
			this.Dispose(false);
		}
	}

}