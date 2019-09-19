using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {

	public class XMSSMTPrivateKey : XMSSMTKey {

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;
		public readonly byte Revision = 0;

		protected readonly XMSSExecutionContext XmssExecutionContext;

		public XMSSMTPrivateKey(XMSSExecutionContext xmssExecutionContext) {
			this.XmssExecutionContext = xmssExecutionContext;
			this.NodeCache = new XMSSMTNodeCache();
		}

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSMTPrivateKey(int heigth, int layer, ByteArray publicSeed, ByteArray secretSeed, ByteArray secretPrf, XMSSMTNonceSet nonces, XMSSExecutionContext xmssExecutionContext, long index = 0, ByteArray root = null) {

			this.Index = index;
			this.Height = (byte) heigth;
			this.Layers = (byte) layer;
			this.LeafCount = 1 << heigth;
			this.XmssExecutionContext = xmssExecutionContext;

			this.PublicSeed = publicSeed?.Clone();
			this.SecretPrf = secretPrf?.Clone();
			this.Root = root?.Clone();
			this.SecretSeed = secretSeed?.Clone();

			this.Nonces = nonces;

			this.NodeCache = new XMSSMTNodeCache(this.Height, this.Layers, xmssExecutionContext.DigestSize);
		}

		public byte Height { get; set; }

		public byte Layers { get; set; }

		protected int LeafCount { get; set; }

		/// <summary>
		///     private key index to use (0 based)
		/// </summary>
		public long Index { get; protected set; }

		/// <summary>
		///     private key index to use (1 based)
		/// </summary>
		public long IndexOne => this.Index + 1;

		public XMSSMTNonceSet Nonces { get; } = new XMSSMTNonceSet();
		public XMSSMTNodeCache NodeCache { get; }

		public ByteArray PublicSeed { get; private set; }
		public ByteArray SecretSeed { get; private set; }
		public ByteArray Root { get; set; }
		public ByteArray SecretPrf { get; private set; }

		protected override void DisposeAll() {
			base.DisposeAll();

			this.PublicSeed?.Dispose();
			this.Root?.Dispose();
			this.SecretPrf?.Dispose();
			this.SecretSeed?.Dispose();
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();
			int revision = rehydrator.ReadByte();

			base.Rehydrate(rehydrator);

			int n = this.XmssExecutionContext.DigestSize;

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			this.Index = (int) adaptiveLong.Value;
			this.Height = rehydrator.ReadByte();
			this.Layers = rehydrator.ReadByte();
			adaptiveLong.Rehydrate(rehydrator);
			this.LeafCount = (int) adaptiveLong.Value;

			this.PublicSeed = rehydrator.ReadArray(n);
			this.SecretSeed = rehydrator.ReadArray(n);
			this.SecretPrf = rehydrator.ReadArray(n);
			this.Root = rehydrator.ReadArray(n);

			this.NodeCache.Rehydrate(rehydrator);

			this.Nonces.Rehydrate(rehydrator, this.LeafCount);
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);

			base.Dehydrate(dehydrator);

			int n = this.XmssExecutionContext.DigestSize;

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = this.Index;
			adaptiveLong.Dehydrate(dehydrator);

			dehydrator.Write(this.Height);
			dehydrator.Write(this.Layers);

			adaptiveLong.Value = this.LeafCount;
			adaptiveLong.Dehydrate(dehydrator);

			dehydrator.WriteRawArray(this.PublicSeed);
			dehydrator.WriteRawArray(this.SecretSeed);
			dehydrator.WriteRawArray(this.SecretPrf);
			dehydrator.WriteRawArray(this.Root);

			this.NodeCache.Dehydrate(dehydrator);

			this.Nonces.Dehydrate(dehydrator);
		}

		public void IncrementIndex(XMSSMTEngine engine) {
			engine.CleanAuthTree(this);

			this.Index += 1;
		}
	}
}