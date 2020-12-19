using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;

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
		
		public XMSSProvider(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, byte treeHeight, Enums.ThreadMode threadMode, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) : base(hashType, backupHashType, treeHeight, threadMode, noncesExponent) {
			
		}

		public override long MaximumHeight => this.xmss?.MaximumIndex ?? 0;

		public XMSSSignaturePathCache CreateSignaturePathCache() {
			return new XMSSSignaturePathCache(this.treeHeight, this.ExcutionContext);
		}
		
		public void SetSaveToDisk(bool saveToDisk, string workingFolderPath = null, bool clearWorkingFolder = true) {
			this.xmss.SaveToDisk = saveToDisk;
			this.xmss.WorkingFolderPath = workingFolderPath;
			this.xmss.ClearWorkingFolder = clearWorkingFolder;
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

		public XMSSSignature LoadSignature(SafeArrayHandle signature, XMSSSignaturePathCache xmssSignaturePathCache = null) {
			return this.xmss.LoadSignature(signature.Entry, xmssSignaturePathCache);
		}

		public async Task<(SafeArrayHandle privateKey, SafeArrayHandle publicKey)> GenerateKeys(int? seedSize =  null, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY) {

			XMSSPublicKey xmssPublicKey = null;
			XMSSPrivateKey xmssPrivateKey = null;

			try {
				(xmssPrivateKey, xmssPublicKey) = await this.xmss.GenerateKeys(seedSize, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);
				
				return ((SafeArrayHandle)xmssPrivateKey.SaveKey(), (SafeArrayHandle)xmssPublicKey.SaveKey());
			} finally {
				xmssPublicKey?.Dispose();
				xmssPrivateKey?.Dispose();
			}
		}

		public XMSSNodeId[] BuildAuthenticationNodes(long index) {
			return this.xmss.BuildAuthTreeNodesList(index);
		}

		/// <summary>
		/// this method allows us to build a cache of uncached nodes required to perform a signature. This is very useful to preload the nodes required to perform a signature
		/// </summary>
		public async Task<SafeArrayHandle> GenerateIndexNodeCache(SafeArrayHandle privateKey, Func<int, int ,int, Task> progressCallback = null) {

			using XMSSPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			var cache = await this.xmss.GenerateIndexNodeCache(loadedPrivateKey ,progressCallback).ConfigureAwait(false);

			return cache.Save();
		}
		
		public async Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> Sign(SafeArrayHandle content, SafeArrayHandle privateKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Func<int, int ,int, Task> progressCallback = null) {

			using XMSSPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			XMSSNodeCache cache = null;
			if(extraNodeCache != null) {
				cache = new XMSSNodeCache();
				cache.Load(extraNodeCache);
			}
			SafeArrayHandle result = await this.Sign(content, loadedPrivateKey, buildOptimizedSignature, xmssSignaturePathCache, extraNodeCache: cache, progressCallback:progressCallback).ConfigureAwait(false);

			// export the new private key
			SafeArrayHandle nextPrivateKey = loadedPrivateKey.SaveKey();
			
			return (result, nextPrivateKey);
		}

		public async Task<SafeArrayHandle> Sign(SafeArrayHandle content, XMSSPrivateKey privateKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, XMSSNodeCache extraNodeCache = null, Func<int, int ,int, Task> progressCallback = null) {

			NLog.Default.Verbose($"Singing message using XMSS (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Hash bits: {this.HashType})");

			SafeArrayHandle signature = (SafeArrayHandle)await this.xmss.Sign(content.Entry, privateKey, buildOptimizedSignature, xmssSignaturePathCache, extraNodeCache: extraNodeCache, progressCallback: progressCallback).ConfigureAwait(false);

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

		public override long GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}