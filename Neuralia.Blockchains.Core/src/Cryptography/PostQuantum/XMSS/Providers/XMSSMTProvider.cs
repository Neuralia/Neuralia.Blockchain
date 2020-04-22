using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public class XMSSMTProvider : XMSSProviderBase {

		//TODO: adjust this
		public const int DEFAULT_XMSSMT_TREE_HEIGHT = 6*2; //20;
		public const int DEFAULT_XMSSMT_TREE_LAYERS = 2; //2;
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

		public async Task<(ByteArray privateKey, ByteArray publicKey)> GenerateKeys(bool buildCache = true, Func<int, int, int, Task> progressCallback = null) {
			(XMSSMTPrivateKey xmssmtPrivateKey, XMSSMTPublicKey xmssmtPublicKey) = await this.xmssmt.GenerateKeys(buildCache, progressCallback).ConfigureAwait(false);

			ByteArray publicKey = xmssmtPublicKey.SaveKey();
			ByteArray privateKey = xmssmtPrivateKey.SaveKey();
			
			return (privateKey, publicKey);
		}

		public Task<(ByteArray signature, ByteArray nextPrivateKey)> Sign(SafeArrayHandle content, SafeArrayHandle privateKey) {
			return this.Sign(content.Entry, privateKey.Entry);
		}

		public XMSSMTPrivateKey CreatePrivateKey() {
			return new XMSSMTPrivateKey(this.excutionContext);
		}
		

		public XMSSMTPrivateKey LoadPrivateKey(ByteArray privateKey) {
			
			XMSSMTPrivateKey loadedPrivateKey = this.CreatePrivateKey();
			loadedPrivateKey.LoadKey(privateKey);

			return loadedPrivateKey;
		}
		
		public async Task<(ByteArray signature, ByteArray nextPrivateKey)> Sign(ByteArray content, ByteArray privateKey) {
			
			XMSSMTPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			ByteArray result = await this.Sign(content, loadedPrivateKey).ConfigureAwait(false);

			// export the new private key
			ByteArray nextPrivateKey = loadedPrivateKey.SaveKey();

			loadedPrivateKey.Dispose();

			return (result, nextPrivateKey);
		}

		public async Task<ByteArray> Sign(ByteArray content, XMSSMTPrivateKey privateKey) {

			Log.Verbose($"Singing message using XMSS^MT (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Tree layers: {this.TreeLayers}, Hash bits: {this.HashBits})");

			ByteArray signature = await this.xmssmt.Sign(content, privateKey).ConfigureAwait(false);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmssmt);

			return signature;
		}

		public override Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {

			return this.Verify(message.Entry, signature.Entry, publicKey.Entry);
		}
		
		public Task<bool> Verify(ByteArray message, ByteArray signature, ByteArray publicKey) {
			
			return this.xmssmt.Verify(signature, message, publicKey);
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.xmssmt?.Dispose();
			this.excutionContext?.Dispose();
		}

		public override int GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}