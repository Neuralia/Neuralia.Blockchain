using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys {

	public class XMSSPrivateKey : XMSSKey {

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;
		public readonly byte Revision = 0;

		protected readonly XMSSExecutionContext XmssExecutionContext;

		public XMSSPrivateKey(XMSSExecutionContext xmssExecutionContext) {
			this.XmssExecutionContext = xmssExecutionContext;
			this.NodeCache = new XMSSNodeCache();
		}

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSPrivateKey(int heigth, ByteArray publicSeed, ByteArray privateSeed, ByteArray secretPrf, XMSSNonceSet nonces, XMSSExecutionContext xmssExecutionContext, XMSSNodeCache xmssNodeCache = null, int index = 0, ByteArray root = null) {
			this.Index = index;
			this.Height = (byte) heigth;
			this.LeafCount = 1 << heigth;
			this.XmssExecutionContext = xmssExecutionContext;

			this.PublicSeed = publicSeed?.Clone();
			this.SecretSeed = privateSeed.Clone();
			this.SecretPrf = secretPrf?.Clone();
			this.Root = root?.Clone();

			this.Nonces = nonces;

			this.NodeCache = xmssNodeCache ?? new XMSSNodeCache(this.Height, xmssExecutionContext.DigestSize);
		}

		public XMSSNodeCache NodeCache { get; }
		public XMSSNonceSet Nonces { get; } = new XMSSNonceSet();

		public byte Height { get; set; }

		public int MaximumIndex => 1 << this.Height;

		public int LeafCount { get; set; }

		/// <summary>
		///     private key index to use (0 based)
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		///     private key index to use (1 based)
		/// </summary>
		public int IndexOne => this.Index + 1;

		public ByteArray PublicSeed { get; private set; }
		public ByteArray SecretSeed { get; private set; }
		public ByteArray SecretPrf { get; private set; }

		public int Nonce1 => this.Nonces[this.Index].nonce1;
		public int Nonce2 => this.Nonces[this.Index].nonce2;

		public ByteArray Root { get; set; }

		public void IncrementIndex(XMSSEngine engine) {
			engine.CleanAuthTree(this);

			this.Index += 1;
		}

		public void SetIndex(int index) {
			this.Index = index;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.PublicSeed?.Dispose();
			this.Root?.Dispose();
			this.SecretPrf?.Dispose();
			this.SecretSeed?.Dispose();
			this.NodeCache.Dispose();
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();
			int revision = rehydrator.ReadByte();

			base.Rehydrate(rehydrator);

			int n = this.XmssExecutionContext.DigestSize;

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			this.Index = (int) adaptiveLong.Value;
			this.Height = rehydrator.ReadByte();
			adaptiveLong.Rehydrate(rehydrator);
			this.LeafCount = (int) adaptiveLong.Value;

			this.PublicSeed = rehydrator.ReadArray(n);
			this.SecretSeed = rehydrator.ReadArray(n);
			this.SecretPrf = rehydrator.ReadArray(n);
			this.Root = rehydrator.ReadArray(n);

			this.NodeCache.Rehydrate(rehydrator);

			this.Nonces.Rehydrate(rehydrator, this.LeafCount);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);

			base.Dehydrate(dehydrator);

			int n = this.XmssExecutionContext.DigestSize;

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = this.Index;
			adaptiveLong.Dehydrate(dehydrator);

			dehydrator.Write(this.Height);

			adaptiveLong.Value = this.LeafCount;
			adaptiveLong.Dehydrate(dehydrator);

			dehydrator.WriteRawArray(this.PublicSeed);
			dehydrator.WriteRawArray(this.SecretSeed);
			dehydrator.WriteRawArray(this.SecretPrf);
			dehydrator.WriteRawArray(this.Root);

			this.NodeCache.Dehydrate(dehydrator);

			this.Nonces.Dehydrate(dehydrator);

		}
	}

}