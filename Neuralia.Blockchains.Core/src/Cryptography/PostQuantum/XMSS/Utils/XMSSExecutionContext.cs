using System;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils {
	public class XMSSExecutionContext : IDisposable2 {

		public XMSSExecutionContext(Func<IDigest> digestFactory, SecureRandom random) {
			this.DigestFactory = digestFactory;
			this.DigestPool = new MemoryBlockPool<IDigest>(() => this.DigestFactory());

			IDigest digest = this.DigestPool.GetObject();
			this.DigestSize = digest.GetDigestSize();
			this.DigestPool.PutObject(digest);

			this.OtsHashAddressPool = new MemoryBlockPool<OtsHashAddress>(() => new OtsHashAddress());
			this.LTreeAddressPool = new MemoryBlockPool<LTreeAddress>(() => new LTreeAddress());
			this.HashTreeAddressPool = new MemoryBlockPool<HashTreeAddress>(() => new HashTreeAddress());

			this.Random = random;
		}

		public Func<IDigest> DigestFactory { get; }

		public MemoryBlockPool<OtsHashAddress> OtsHashAddressPool { get; }
		public MemoryBlockPool<LTreeAddress> LTreeAddressPool { get; }
		public MemoryBlockPool<HashTreeAddress> HashTreeAddressPool { get; }

		public int DigestSize { get; }
		public MemoryBlockPool<IDigest> DigestPool { get; }

		public SecureRandom Random { get; }

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					this.OtsHashAddressPool.Dispose();
				} catch {
				}

				try {
					this.LTreeAddressPool.Dispose();
				} catch {
				}

				try {
					this.HashTreeAddressPool.Dispose();
				} catch {
				}

				try {
					this.DigestPool.Dispose();
				} catch {
				}

			}

			this.IsDisposed = true;
		}

		~XMSSExecutionContext() {
			this.Dispose(false);
		}

	#endregion

	}
}