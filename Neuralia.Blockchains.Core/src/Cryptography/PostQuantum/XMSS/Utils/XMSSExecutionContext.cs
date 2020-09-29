using System;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils {
	public class XMSSExecutionContext : IDisposableExtended {

		public XMSSExecutionContext(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, Func<IHashDigest> digestFactory, Func<IHashDigest> backupDigestFactory, bool enableCaches = false) {
			this.DigestFactory = digestFactory;
			this.BackupDigestFactory = backupDigestFactory;

			this.HashType = hashType;
			this.BackupHashType = backupHashType;
			
			this.DigestPool = new ObjectPool<IHashDigest>(() => this.DigestFactory(), 0, 2);
			this.BackupDigestPool = new ObjectPool<IHashDigest>(() => this.BackupDigestFactory(), 0, 2);

			IHashDigest digest = this.DigestPool.GetObject();
			this.DigestSize = digest.GetDigestSize();
			this.DigestPool.PutObject(digest);
			
			IHashDigest backupDigest = this.BackupDigestPool.GetObject();
			this.BackupDigestSize = backupDigest.GetDigestSize();
			this.BackupDigestPool.PutObject(backupDigest);

			this.OtsHashAddressPool = new ObjectPool<OtsHashAddress>(() => new OtsHashAddress(), 0, 2);
			this.LTreeAddressPool = new ObjectPool<LTreeAddress>(() => new LTreeAddress(), 0, 2);
			this.HashTreeAddressPool = new ObjectPool<HashTreeAddress>(() => new HashTreeAddress(), 0, 2);

			this.EnableCaches = enableCaches;
		}

		public Func<IHashDigest> DigestFactory { get; }
		public Func<IHashDigest> BackupDigestFactory { get; }
		
		public ObjectPool<OtsHashAddress> OtsHashAddressPool { get; }
		public ObjectPool<LTreeAddress> LTreeAddressPool { get; }
		public ObjectPool<HashTreeAddress> HashTreeAddressPool { get; }

		public Enums.KeyHashType HashType { get; }
		public Enums.KeyHashType BackupHashType { get; }

		public bool EnableCaches { get; set; }
		public int DigestSize { get; }
		public int BackupDigestSize { get; }
		public ObjectPool<IHashDigest> DigestPool { get; }
		public ObjectPool<IHashDigest> BackupDigestPool { get; }
		
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
				
				try {
					this.BackupDigestPool.Dispose();
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