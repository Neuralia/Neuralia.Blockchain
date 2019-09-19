using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public class XMSSMTProvider : XMSSProviderBase {

		//TODO: adjust this
		public const int DEFAULT_XMSSMT_TREE_HEIGHT = 5; //20;
		public const int DEFAULT_XMSSMT_TREE_LAYERS = 1; //2;
		public const Enums.KeyHashBits DEFAULT_HASH_BITS = Enums.KeyHashBits.SHA3_512;

		protected XMSSMTEngine xmssmt;

		public XMSSMTProvider() : this(DEFAULT_HASH_BITS, Enums.ThreadMode.Half) {
		}

		public XMSSMTProvider(Enums.KeyHashBits hashBits, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : this(hashBits, DEFAULT_XMSSMT_TREE_HEIGHT, DEFAULT_XMSSMT_TREE_LAYERS, threadMode) {
		}

		public XMSSMTProvider(Enums.KeyHashBits hashBits, int treeHeight, int treeLayers, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : this(hashBits, threadMode, treeHeight, treeLayers) {

		}

		public XMSSMTProvider(Enums.KeyHashBits hashBits, Enums.ThreadMode threadMode = Enums.ThreadMode.Half, int treeHeight = DEFAULT_XMSSMT_TREE_HEIGHT, int treeLayers = DEFAULT_XMSSMT_TREE_LAYERS) : base(hashBits, treeHeight, threadMode) {
			this.TreeLayers = treeLayers;
		}

		public override int MaximumHeight => this.xmssmt?.MaximumIndex ?? 0;
		public int TreeLayers { get; }

		public override void Reset() {
			this.xmssmt?.Dispose();
			this.excutionContext?.Dispose();

			this.excutionContext = this.GetNewExecutionContext();

			//TODO: make modes configurable
			this.xmssmt = new XMSSMTEngine(XMSSOperationModes.Both,  this.threadMode, this.excutionContext, this.TreeHeight, this.TreeLayers);
		}

		public (ByteArray privateKey, ByteArray publicKey) GenerateKeys() {
			(XMSSMTPrivateKey privateKey, XMSSMTPublicKey publicKey) xmssKeys = this.xmssmt.GenerateKeys();

			ByteArray publicKey = xmssKeys.publicKey.SaveKey();
			ByteArray privateKey = xmssKeys.privateKey.SaveKey();

			return (privateKey, publicKey);
		}

		public (ByteArray signature, ByteArray nextPrivateKey) Sign(SafeArrayHandle content, SafeArrayHandle privateKey) {
			return this.Sign(content.Entry, privateKey.Entry);
		}
		

		public (ByteArray signature, ByteArray nextPrivateKey) Sign(ByteArray content, ByteArray privateKey) {
			XMSSMTPrivateKey loadedPrivateKey = new XMSSMTPrivateKey(this.excutionContext);
			loadedPrivateKey.LoadKey(privateKey);

			ByteArray result = this.Sign(content, loadedPrivateKey);

			// export the new private key
			ByteArray nextPrivateKey = loadedPrivateKey.SaveKey();

			loadedPrivateKey.Dispose();

			return (result, nextPrivateKey);
		}

		public ByteArray Sign(ByteArray content, XMSSMTPrivateKey privateKey) {

			Log.Verbose($"Singing message using XMSS^MT (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Tree layers: {this.TreeLayers}, Hash bits: {this.HashBits})");

			ByteArray signature = this.xmssmt.Sign(content, privateKey);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmssmt);

			return signature;
		}

		public override bool Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {

			return this.Verify(message.Entry, signature.Entry, publicKey.Entry);
		}
		
		public bool Verify(ByteArray message, ByteArray signature, ByteArray publicKey) {

			return this.xmssmt.Verify(signature, message, publicKey);
		}

		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);

			if(disposing) {
				this.xmssmt?.Dispose();
				this.excutionContext?.Dispose();
			}

		}

		public override int GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}