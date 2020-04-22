using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	/// <summary>
	///     A random XMSS provider
	/// </summary>
	public class XMSSProvider : XMSSProviderBase {

		//TODO: reset the default values for production :  height 13

		public const int DEFAULT_XMSS_TREE_HEIGHT = 12; //13;
		public const Enums.KeyHashBits DEFAULT_HASH_BITS = Enums.KeyHashBits.SHA2_512;

		protected XMSSEngine xmss;

		public XMSSProvider() : this(DEFAULT_HASH_BITS) {
		}

		public XMSSProvider(Enums.KeyHashBits hashBits, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : this(hashBits, DEFAULT_XMSS_TREE_HEIGHT, threadMode) {
		}

		public XMSSProvider(Enums.KeyHashBits hashBits, int treeHeight, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : base(hashBits, treeHeight, threadMode) {
		}

		public override int MaximumHeight => this.xmss?.MaximumIndex ?? 0;

		public override void Reset() {
			this.xmss?.Dispose();
			this.excutionContext?.Dispose();

			this.excutionContext = this.GetNewExecutionContext();

			//TODO: make modes configurable
			this.xmss = new XMSSEngine(XMSSOperationModes.Both, this.threadMode, null, this.excutionContext, this.TreeHeight);
		}
		
		public XMSSPrivateKey CreatePrivateKey() {
			return new XMSSPrivateKey(this.excutionContext);
		}

		public XMSSPrivateKey LoadPrivateKey(ByteArray privateKey) {

			XMSSPrivateKey loadedPrivateKey = this.CreatePrivateKey();
			loadedPrivateKey.LoadKey(privateKey);

			return loadedPrivateKey;
		}
		
		public async Task<(ByteArray privateKey, ByteArray publicKey)> GenerateKeys(Func<int, Task> progressCallback = null) {
			(XMSSPrivateKey xmssPrivateKey, XMSSPublicKey xmssPublicKey) = await this.xmss.GenerateKeys(progressCallback).ConfigureAwait(false);

			ByteArray publicKey = xmssPublicKey.SaveKey();
			ByteArray privateKey = xmssPrivateKey.SaveKey();

			return (privateKey, publicKey);
		}

		public async Task<(ByteArray signature, ByteArray nextPrivateKey)> Sign(ByteArray content, ByteArray privateKey) {
			
			XMSSPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			ByteArray result = await this.Sign(content, loadedPrivateKey).ConfigureAwait(false);

			// export the new private key
			ByteArray nextPrivateKey = loadedPrivateKey.SaveKey();

			loadedPrivateKey.Dispose();

			return (result, nextPrivateKey);
		}

		public Task<(ByteArray signature, ByteArray nextPrivateKey)> Sign(SafeArrayHandle content, SafeArrayHandle privateKey) {
			return this.Sign(content.Entry, privateKey.Entry);
		}
		
		public async Task<ByteArray> Sign(ByteArray content, XMSSPrivateKey privateKey) {

			Log.Verbose($"Singing message using XMSS (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Hash bits: {this.HashBits})");

			ByteArray signature = await this.xmss.Sign(content, privateKey).ConfigureAwait(false);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmss);

			return signature;
		}

		public override Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {

			return this.Verify(message.Entry, signature.Entry, publicKey.Entry);
		}
		
		
		public Task<bool> Verify(ByteArray message, ByteArray signature, ByteArray publicKey) {
			
			return this.xmss.Verify(signature, message, publicKey);
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.xmss?.Dispose();
			this.excutionContext?.Dispose();
		}

		public override int GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}