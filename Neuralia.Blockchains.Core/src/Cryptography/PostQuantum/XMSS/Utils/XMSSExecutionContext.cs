using System;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils {
	public class XMSSExecutionContext : IDisposable2 {

		public XMSSExecutionContext(Func<IDigest> digestFactory, SecureRandom random) {
			this.DigestFactory = digestFactory;
			this.DigestPool = new ObjectPool<IDigest>(() => this.DigestFactory());

			IDigest digest = this.DigestPool.GetObject();
			this.DigestSize = digest.GetDigestSize();
			this.DigestPool.PutObject(digest);

			this.OtsHashAddressPool = new ObjectPool<OtsHashAddress>(() => new OtsHashAddress());
			this.LTreeAddressPool = new ObjectPool<LTreeAddress>(() => new LTreeAddress());
			this.HashTreeAddressPool = new ObjectPool<HashTreeAddress>(() => new HashTreeAddress());

			this.Random = random;
		}

		public Func<IDigest> DigestFactory { get; }

		public ObjectPool<OtsHashAddress> OtsHashAddressPool { get; }
		public ObjectPool<LTreeAddress> LTreeAddressPool { get; }
		public ObjectPool<HashTreeAddress> HashTreeAddressPool { get; }

		public int DigestSize { get; }
		public ObjectPool<IDigest> DigestPool { get; }

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