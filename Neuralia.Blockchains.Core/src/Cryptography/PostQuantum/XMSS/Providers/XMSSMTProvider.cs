using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public class XMSSMTProvider : XMSSProviderBase {

		//TODO: adjust this
		public const int DEFAULT_XMSSMT_TREE_HEIGHT = 6 * 2; //20;
		public const int DEFAULT_XMSSMT_TREE_LAYERS = 2; //2;
		public const Enums.KeyHashType DEFAULT_HASH_BITS = Enums.KeyHashType.SHA3_512;
		public const Enums.KeyHashType DEFAULT_BACKUP_HASH_BITS = Enums.KeyHashType.SHA2_512;
		
		protected XMSSMTEngine xmssmt;

		public XMSSMTProvider(Enums.ThreadMode threadMode, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) : this(DEFAULT_HASH_BITS, DEFAULT_BACKUP_HASH_BITS, threadMode, noncesExponent) {
		}

		public XMSSMTProvider(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, Enums.ThreadMode threadMode , byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) : this(hashType, backupHashType, DEFAULT_XMSSMT_TREE_HEIGHT, DEFAULT_XMSSMT_TREE_LAYERS, threadMode, noncesExponent) {
		}

		public XMSSMTProvider(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType , byte treeHeight , byte treeLayers , Enums.ThreadMode threadMode, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) : base(hashType, backupHashType, treeHeight, threadMode, noncesExponent) {
			this.TreeLayers = treeLayers;
		}

		public override long MaximumHeight => this.xmssmt?.MaximumIndex ?? 0;
		public byte TreeLayers { get; }

		public void SetSaveToDisk(bool saveToDisk, string workingFolderPath = null, bool clearWorkingFolder = true, bool saveXmssToDisk = false, string workingXmssFolderPath = null, bool clearXmssWorkingFolder = true) {
			this.xmssmt.SaveToDisk = saveToDisk;
			this.xmssmt.WorkingFolderPath = workingFolderPath;
			this.xmssmt.ClearWorkingFolder = clearWorkingFolder;

			this.xmssmt.SetXmssSaveToDisk(saveXmssToDisk, workingXmssFolderPath, clearXmssWorkingFolder);
		}
		
		public override void Reset() {
			this.xmssmt?.Dispose();
			this.ExcutionContext?.Dispose();

			this.ExcutionContext = this.GetNewExecutionContext();

			//TODO: make modes configurable
			this.xmssmt = new XMSSMTEngine(XMSSOperationModes.Both, this.threadMode, this.ExcutionContext, this.TreeHeight, this.TreeLayers, this.NoncesExponent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="seedSize"></param>
		/// <param name="buildCache"></param>
		/// <param name="progressCallback">layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers</param>
		/// <param name="cacheMode"></param>
		/// <param name="cacheLevels"></param>
		/// <param name="noncesExponent"></param>
		/// <returns></returns>
		public async Task<(SafeArrayHandle privateKey, SafeArrayHandle publicKey)> GenerateKeys(int? seedSize =  null, bool buildCache = true, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY) {
			
			XMSSMTPublicKey xmssmtPublicKey = null;
			XMSSMTPrivateKey xmssmtPrivateKey = null;

			try {
				(xmssmtPrivateKey, xmssmtPublicKey) = await this.xmssmt.GenerateKeys(seedSize, buildCache, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);
				
				return (xmssmtPrivateKey.SaveKey(), xmssmtPublicKey.SaveKey());
			} finally {
				xmssmtPublicKey?.Dispose();
				xmssmtPrivateKey?.Dispose();
			}
		}

		public XMSSMTPrivateKey CreatePrivateKey() {
			return new XMSSMTPrivateKey(this.ExcutionContext);
		}

		public XMSSMTPrivateKey LoadPrivateKey(SafeArrayHandle privateKey) {

			XMSSMTPrivateKey loadedPrivateKey = this.CreatePrivateKey();
			loadedPrivateKey.LoadKey(privateKey);

			return loadedPrivateKey;
		}

		public async Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> Sign(SafeArrayHandle content, SafeArrayHandle privateKey) {

			using XMSSMTPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			SafeArrayHandle result = await this.Sign(content, loadedPrivateKey).ConfigureAwait(false);

			// export the new private key
			SafeArrayHandle nextPrivateKey = loadedPrivateKey.SaveKey();
			
			return (result, nextPrivateKey);
		}

		public async Task<SafeArrayHandle> Sign(SafeArrayHandle content, XMSSMTPrivateKey privateKey) {

			NLog.Default.Verbose($"Singing message using XMSS^MT (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Tree layers: {this.TreeLayers}, Hash bits: {this.HashType})");

			SafeArrayHandle signature = (SafeArrayHandle)await this.xmssmt.Sign(content.Entry, privateKey).ConfigureAwait(false);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmssmt);

			return signature;
		}
		
		public override Task<bool> Verify(SafeArrayHandle message, SafeArrayHandle signature, SafeArrayHandle publicKey) {

			return this.xmssmt.Verify(signature.Entry, message.Entry, publicKey.Entry);
		}
		
		public override SafeArrayHandle SetPrivateKeyIndex(int index, SafeArrayHandle privateKey) {
			using XMSSMTPrivateKey loadedPrivateKey = this.LoadPrivateKey(privateKey);

			loadedPrivateKey.Index = index;
			
			// export the new private key
			return loadedPrivateKey.SaveKey();
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.xmssmt?.Dispose();
			this.ExcutionContext?.Dispose();
		}

		public override long GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}