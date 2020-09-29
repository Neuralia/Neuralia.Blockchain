using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	/// <summary>
	///     A random XMSS provider
	/// </summary>
	public class XMSSProvider : XMSSProviderBase {

		//TODO: reset the default values for production :  height 13

		public const int DEFAULT_XMSS_TREE_HEIGHT = 13;
		public const Enums.KeyHashType DEFAULT_HASH_BITS = Enums.KeyHashType.SHA2_512;
		public const Enums.KeyHashType DEFAULT_BACKUP_HASH_BITS = Enums.KeyHashType.SHA3_512;

		protected XMSSEngine xmss;

		public XMSSProvider(Enums.ThreadMode threadMode) : this(DEFAULT_HASH_BITS, DEFAULT_BACKUP_HASH_BITS, threadMode) {
		}

		public XMSSProvider(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, Enums.ThreadMode threadMode) : this(hashType, backupHashType, DEFAULT_XMSS_TREE_HEIGHT, threadMode) {
		}

		public XMSSProvider(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, byte treeHeight, Enums.ThreadMode threadMode) : base(hashType, backupHashType, treeHeight, threadMode) {
		}

		public override int MaximumHeight => this.xmss?.MaximumIndex ?? 0;

		public XMSSSignaturePathCache CreateSignaturePathCache() {
			return new XMSSSignaturePathCache(this.treeHeight, this.ExcutionContext);
		}
		public override void Reset() {
			this.xmss?.Dispose();
			this.ExcutionContext?.Dispose();

			this.ExcutionContext = this.GetNewExecutionContext();

			//TODO: make modes configurable
			this.xmss = new XMSSEngine(XMSSOperationModes.Both, this.threadMode, null, this.ExcutionContext, this.TreeHeight);
		}

		
		public XMSSPrivateKey CreatePrivateKey() {
			return new XMSSPrivateKey(this.ExcutionContext);
		}

		public XMSSPrivateKey LoadPrivateKey(SafeArrayHandle privateKey) {

			XMSSPrivateKey loadedPrivateKey = this.CreatePrivateKey();
			loadedPrivateKey.LoadKey(privateKey);

			return loadedPrivateKey;
		}

		public async Task<(SafeArrayHandle privateKey, SafeArrayHandle publicKey)> GenerateKeys(Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Heuristic) {

			XMSSPublicKey xmssPublicKey = null;
			XMSSPrivateKey xmssPrivateKey = null;

			try {
				(xmssPrivateKey, xmssPublicKey) = await this.xmss.GenerateKeys(progressCallback, cacheMode).ConfigureAwait(false);
				
				return ((SafeArrayHandle)xmssPrivateKey.SaveKey(), (SafeArrayHandle)xmssPublicKey.SaveKey());
			} finally {
				xmssPublicKey?.Dispose();
				xmssPrivateKey?.Dispose();
			}
		}

		public ImmutableList<XMSSNodeId> BuildAuthenticationNodes(int index) {
			return this.xmss.BuildAuthTreeNodesList(index);
		}

		public async Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> Sign(SafeArrayHandle content, SafeArrayHandle privateKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null) {

			using XMSSPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			SafeArrayHandle result = await this.Sign(content, loadedPrivateKey, buildOptimizedSignature, xmssSignaturePathCache).ConfigureAwait(false);

			// export the new private key
			SafeArrayHandle nextPrivateKey = loadedPrivateKey.SaveKey();
			
			return (result, nextPrivateKey);
		}

		public async Task<SafeArrayHandle> Sign(SafeArrayHandle content, XMSSPrivateKey privateKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null) {

			NLog.Default.Verbose($"Singing message using XMSS (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Hash bits: {this.HashType})");

			SafeArrayHandle signature = (SafeArrayHandle)await this.xmss.Sign(content.Entry, privateKey, buildOptimizedSignature, xmssSignaturePathCache).ConfigureAwait(false);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmss);

			return signature;
		}
		
		public override Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {

			return this.Verify(message,signature, publicKey, null);
		}

		public Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey, XMSSSignaturePathCache xmssSignaturePathCache) {

			return this.xmss.Verify(signature.Entry, message.Entry, publicKey.Entry, xmssSignaturePathCache);
		}
		
		public override SafeArrayHandle SetPrivateKeyIndex(int index, SafeArrayHandle privateKey) {
			using XMSSPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			loadedPrivateKey.Index = index;
			
			// export the new private key
			return loadedPrivateKey.SaveKey();
		}

		public SafeArrayHandle UpdateFromSignature(SafeArrayHandle signature, SafeArrayHandle signaturePathCache) {
			
			using XMSSSignature loadedSignature = this.xmss.LoadSignature(signature.Entry);
			
			XMSSSignaturePathCache cache = new XMSSSignaturePathCache();
			cache.Load(signaturePathCache.Entry);
			
			cache.UpdateFromSignature(loadedSignature, this.BuildAuthenticationNodes(loadedSignature.Index));

			return (SafeArrayHandle)cache.Save();
		}
		
		protected override void DisposeAll() {
			base.DisposeAll();

			this.xmss?.Dispose();
			this.ExcutionContext?.Dispose();
		}

		public override int GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}