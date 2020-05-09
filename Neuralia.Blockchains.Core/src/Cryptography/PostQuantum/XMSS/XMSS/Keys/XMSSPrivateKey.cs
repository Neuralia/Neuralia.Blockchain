using System;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys {

	public class XMSSPrivateKey : XMSSKey {

		protected readonly XMSSExecutionContext XmssExecutionContext;

		public XMSSPrivateKey(XMSSExecutionContext xmssExecutionContext) {
			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize);

			this.NoncePart = new KeyNoncePart(0) {NodeCache = new XMSSNodeCache()};
			this.NoncePart.Nonces = new XMSSNonceSet();
			this.NoncePart.NodeCache = new XMSSNodeCache();
		}

		public XMSSPrivateKey(XMSSPrivateKey other, XMSSExecutionContext xmssExecutionContext) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize);

			this.HeaderPart.Index = other.HeaderPart.Index;
			this.HeaderPart.Height = other.HeaderPart.Height;
			this.HeaderPart.LeafCount = other.HeaderPart.LeafCount;

			this.HeaderPart.PublicSeed = other.HeaderPart.PublicSeed?.Clone();

			this.SecretPart.SecretPrf = other.SecretPart.SecretPrf?.Clone();
			this.SecretPart.Root = other.SecretPart.Root?.Clone();
			this.SecretPart.SecretSeed = other.SecretPart.SecretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(other.NoncePart.Nonces.Nonces);
			this.NoncePart.NodeCache = new XMSSNodeCache(this.HeaderPart.Height, xmssExecutionContext.DigestSize);
		}

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSPrivateKey(int heigth, ByteArray publicSeed, ByteArray secretSeed, ByteArray secretPrf, XMSSNonceSet nonces, XMSSExecutionContext xmssExecutionContext, XMSSNodeCache xmssNodeCache = null, int index = 0, ByteArray root = null) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize);

			this.HeaderPart.LeafCount = 1 << heigth;
			this.HeaderPart.Index = index;
			this.HeaderPart.Height = (byte) heigth;

			this.HeaderPart.PublicSeed = publicSeed?.Clone();

			this.SecretPart.SecretPrf = secretPrf?.Clone();
			this.SecretPart.Root = root?.Clone();
			this.SecretPart.SecretSeed = secretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(nonces.Nonces);
			this.NoncePart.NodeCache = xmssNodeCache ?? new XMSSNodeCache(this.Height, xmssExecutionContext.DigestSize);
		}

		// versioning information

		public byte Height {
			get => this.HeaderPart.Height;
			set => this.HeaderPart.Height = value;
		}

		public int LeafCount {
			get => this.HeaderPart.LeafCount;
			set => this.HeaderPart.LeafCount = value;
		}

		/// <summary>
		///     private key index to use (0 based)
		/// </summary>
		public int Index {
			get => this.HeaderPart.Index;
			set => this.HeaderPart.Index = value;
		}

		public ByteArray PublicSeed {
			get => this.HeaderPart.PublicSeed;
			set => this.HeaderPart.PublicSeed = value;
		}

		/// <summary>
		///     private key index to use (1 based)
		/// </summary>
		public long IndexOne => this.Index + 1;

		public ByteArray SecretSeed {
			get => this.SecretPart.SecretSeed;
			set => this.SecretPart.SecretSeed = value;
		}

		public ByteArray Root {
			get => this.SecretPart.Root;
			set => this.SecretPart.Root = value;
		}

		public ByteArray SecretPrf {
			get => this.SecretPart.SecretPrf;
			set => this.SecretPart.SecretPrf = value;
		}

		public XMSSNonceSet Nonces {
			get => this.NoncePart.Nonces;
			set => this.NoncePart.Nonces = value;
		}

		public XMSSNodeCache NodeCache {
			get => this.NoncePart.NodeCache;
			set => this.NoncePart.NodeCache = value;
		}

		public KeyHeaderPart HeaderPart { get; set; }
		public KeySecretPart SecretPart { get; set; }
		public KeyNoncePart NoncePart { get; set; }

		public int Nonce1 => this.Nonces[this.Index].nonce1;
		public int Nonce2 => this.Nonces[this.Index].nonce2;

		protected override void DisposeAll() {
			base.DisposeAll();

			this.SecretPart.Dispose();
		}

		public override void LoadKey(ByteArray keyBytes) {

			BrotliCompression compression = new BrotliCompression();
			using SafeArrayHandle deflatedPrivateKey = compression.Decompress(keyBytes);

			base.LoadKey(deflatedPrivateKey.Entry);
		}

		public override ByteArray SaveKey() {
			using ByteArray privateKey = base.SaveKey();

			BrotliCompression compression = new BrotliCompression();
			SafeArrayHandle compressedPrivateKey = compression.Compress(privateKey);

			return compressedPrivateKey.Entry;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			this.RehydrateParts(rehydrator, rehydrator, rehydrator);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			this.DehydrateParts(dehydrator, dehydrator, dehydrator);
		}

		public void RehydrateParts(IDataRehydrator rehydratorHeader, IDataRehydrator rehydratorSecrets, IDataRehydrator rehydratorNonces) {
			if(rehydratorHeader != null) {
				this.HeaderPart.Rehydrate(rehydratorHeader);

				base.Rehydrate(rehydratorHeader);
			}

			if(rehydratorSecrets != null) {
				this.SecretPart.Rehydrate(rehydratorSecrets);
			}

			if(rehydratorNonces != null) {
				this.NoncePart.LeafCount = this.HeaderPart.LeafCount;
				this.NoncePart.Rehydrate(rehydratorNonces);
			}
		}

		public void DehydrateParts(IDataDehydrator dehydratorHeader, IDataDehydrator dehydratorSecrets, IDataDehydrator dehydratorNonces) {
			if(dehydratorHeader != null) {
				this.HeaderPart.Dehydrate(dehydratorHeader);

				base.Dehydrate(dehydratorHeader);
			}

			if(dehydratorSecrets != null) {
				this.SecretPart.Dehydrate(dehydratorSecrets);
			}

			if(dehydratorNonces != null) {
				this.NoncePart.Dehydrate(dehydratorNonces);
			}
		}

		public void ClearNodeCache() {
			this.NodeCache = null;
		}

		public void IncrementIndex(XMSSEngine engine) {
			engine.CleanAuthTree(this);

			this.HeaderPart.Index += 1;
		}

		public void SetIndex(int index) {
			this.HeaderPart.Index = index;
		}

		public class KeyHeaderPart : IBinarySerializable {

			public readonly byte Major = 1;
			public readonly byte Minor = 0;
			public readonly byte Revision = 0;

			public KeyHeaderPart(int digestSize) {
				this.DigestSize = digestSize;
			}

			private int DigestSize { get; }

			public byte Height { get; set; }

			public int LeafCount { get; set; }

			/// <summary>
			///     private key index to use (0 based)
			/// </summary>
			public int Index { get; set; }

			/// <summary>
			///     private key index to use (1 based)
			/// </summary>
			public int IndexOne => this.Index + 1;

			public ByteArray PublicSeed { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {

				int major = rehydrator.ReadByte();
				int minor = rehydrator.ReadByte();
				int revision = rehydrator.ReadByte();

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				this.Index = (int) adaptiveLong.Value;
				this.Height = rehydrator.ReadByte();
				adaptiveLong.Rehydrate(rehydrator);
				this.LeafCount = (int) adaptiveLong.Value;

				this.PublicSeed = rehydrator.ReadArray(this.DigestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.Major);
				dehydrator.Write(this.Minor);
				dehydrator.Write(this.Revision);

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Value = this.Index;
				adaptiveLong.Dehydrate(dehydrator);

				dehydrator.Write(this.Height);

				adaptiveLong.Value = this.LeafCount;
				adaptiveLong.Dehydrate(dehydrator);

				dehydrator.WriteRawArray(this.PublicSeed);
			}
		}

		public class KeySecretPart : IBinarySerializable, IDisposableExtended {

			public KeySecretPart(int digestSize) {
				this.DigestSize = digestSize;
			}

			private int DigestSize { get; }

			public ByteArray SecretSeed { get; set; }
			public ByteArray Root { get; set; }
			public ByteArray SecretPrf { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				this.SecretSeed = rehydrator.ReadArray(this.DigestSize);
				this.SecretPrf = rehydrator.ReadArray(this.DigestSize);
				this.Root = rehydrator.ReadArray(this.DigestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.WriteRawArray(this.SecretSeed);
				dehydrator.WriteRawArray(this.SecretPrf);
				dehydrator.WriteRawArray(this.Root);
			}

		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {
					this.DisposeAll();
				}

				this.IsDisposed = true;
			}

			protected virtual void DisposeAll() {
				this.Root?.Dispose();
				this.SecretPrf?.Dispose();
				this.SecretSeed?.Dispose();
			}

			~KeySecretPart() {
				this.Dispose(false);
			}

		#endregion

		}

		public class KeyNoncePart : IBinarySerializable {
			public KeyNoncePart(int leafCount) {
				this.LeafCount = leafCount;
			}

			public int LeafCount { get; set; }

			public XMSSNonceSet Nonces { get; set; }
			public XMSSNodeCache NodeCache { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				this.NodeCache.Rehydrate(rehydrator);

				this.Nonces.Rehydrate(rehydrator, this.LeafCount);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				this.NodeCache.Dehydrate(dehydrator);

				this.Nonces.Dehydrate(dehydrator);
			}
		}
	}

}