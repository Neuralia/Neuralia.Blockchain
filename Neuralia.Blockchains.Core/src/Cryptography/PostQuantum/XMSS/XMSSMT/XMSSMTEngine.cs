using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT {

	// <summary>
	/// THE XMSS^MT class
	/// </summary>
	/// <remarks>this was built according to the XMSS RFC https://tools.ietf.org/html/rfc8391</remarks>
	public class XMSSMTEngine : IDisposableExtended {
		private readonly int digestLength;
		private readonly int height;
		private readonly int layers;

		private readonly WotsPlus wotsPlusProvider;

		private readonly Dictionary<XMSSMTreeId, ByteArray[]> wotsPrivateSeedsCache = new Dictionary<XMSSMTreeId, ByteArray[]>();

		private readonly XMSSEngine xmssEngine;

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		/// <remarks>Can sign 2^height messages. More layers (example 4 layers vs 2 layers) make key generation dramatically faster but signature size twice as large, signature time twice as fast and verification time twice as long.</remarks>
		public XMSSMTEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, XMSSExecutionContext xmssExecutionContext, int height, int layers) {

			if(height < 2) {
				throw new ArgumentException("totalHeight must be rgeater than 1");
			}

			if((height % layers) != 0) {
				throw new ArgumentException("layers must divide height without remainder");
			}

			if((height / layers) == 1) {
				throw new ArgumentException("height / layers must be greater than 1");
			}

			this.layers = layers;
			this.height = height;

			this.xmssExecutionContext = xmssExecutionContext;
			this.wotsPlusProvider = new WotsPlus(threadMode, this.xmssExecutionContext);

			this.ReducedHeight = this.height / this.layers;
			this.LeafCount = 1L << this.height;
			this.ReducedLeafCount = 1L << this.ReducedHeight;

			this.digestLength = this.xmssExecutionContext.DigestSize;

			this.xmssEngine = new XMSSEngine(mode, threadMode, this.wotsPlusProvider, this.xmssExecutionContext, this.ReducedHeight);
		}

		private bool EnableCaches => this.xmssExecutionContext.EnableCaches;
		public long LeafCount { get; }
		public int ReducedHeight { get; }
		public long ReducedLeafCount { get; }
		public int MaximumIndex => 1 << this.ReducedHeight;

		public ByteArray[] GenerateWotsDeterministicPrivateSeeds(ByteArray secretSeed, int nonce, XMSSMTLeafId index) {

			bool enabledCache = this.EnableCaches && index.Layer != 0;
			if(enabledCache && this.wotsPrivateSeedsCache.ContainsKey(index)) {
				return this.wotsPrivateSeedsCache[index];
			}

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Reset();
			otsHashAddress.OtsAddress = (int) index.Index;
			otsHashAddress.LayerAddress = index.Layer;
			otsHashAddress.TreeAddress = index.Tree;

			ByteArray[] result = this.wotsPlusProvider.GeneratePseudorandomPrivateKeys(secretSeed, nonce, otsHashAddress);

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			if(enabledCache) {
				this.wotsPrivateSeedsCache.Add(index, result);
			}

			return result;
		}

		public async Task<(XMSSMTPrivateKey privateKey, XMSSMTPublicKey publicKey)> GenerateKeys(bool buildCache = true, Func<int, long, int, Task> progressCallback = null) {

			ByteArray publicSeed = null;
			ByteArray secretSeed = null;
			ByteArray secretSeedPrf = null;

			try {
				(publicSeed, secretSeed, secretSeedPrf) = XMSSCommonUtils.GenerateSeeds(this.xmssExecutionContext);

				this.ClearCaches();

				List<(int nonce1, int nonce2)> nonces = new List<(int nonce1, int nonce2)>();

				for(int i = 0; i < this.ReducedLeafCount; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
					nonces.Add((GlobalRandom.GetNext(), GlobalRandom.GetNext()));
#endif
				}

				nonces.Shuffle();

				XMSSMTPrivateKey secretKey = new XMSSMTPrivateKey(this.height, this.layers, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces), this.xmssExecutionContext);

				// now lets prepare the public key
				int lastLayer = this.layers - 1;

				OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();

				for(int layer = 0; layer < this.layers; layer++) {
					for(long tree = 0; tree < (1 << ((this.layers - 1 - layer) * this.ReducedHeight)); tree++) {

						adrs.Reset();

						adrs.LayerAddress = layer;
						adrs.TreeAddress = tree;

						using XMSSPrivateKey xmssPrivateKey = this.BuildXmssPrivateKey(adrs, secretKey, 0);

						// build the node tree
						long tree1 = tree;
						int layer1 = layer;

						(ByteArray root, ByteArray backupRoot) = await this.xmssEngine.TreeHash(xmssPrivateKey, 0, this.ReducedHeight, publicSeed, adrs, pct => {

							if(progressCallback != null) {
								return progressCallback(pct, tree1, layer1);
							}

							return Task.CompletedTask;
						}).ConfigureAwait(false);

						if(layer == lastLayer) {
							secretKey.Root = root;
							secretKey.BackupRoot = backupRoot;
						} else {
							root?.Dispose();
							backupRoot?.Dispose();
						}

						// so it wont be disposed, since we keep it in the xmss^mt key
						xmssPrivateKey.ClearNodeCache();
					}
				}

				this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

				XMSSMTPublicKey publicKey = new XMSSMTPublicKey(publicSeed, secretKey.Root, secretKey.BackupRoot, this.xmssExecutionContext);

				if(!buildCache) {
					secretKey.NodeCache.Clear();
				}

				return (secretKey, publicKey);
			} finally {
				
				this.ClearCaches();
				
				publicSeed?.Dispose();
				secretSeed?.Dispose();
				secretSeedPrf?.Dispose();
			}
		}

		private XMSSPrivateKey BuildXmssPrivateKey(XMSSMTreeId id, XMSSMTPrivateKey xmssmtSecretKey, int leafIndex) {

			XMSSPrivateKey key = new XMSSPrivateKey(this.ReducedHeight, xmssmtSecretKey.PublicSeed, xmssmtSecretKey.SecretSeed, xmssmtSecretKey.SecretPrf, new XMSSNonceSet(xmssmtSecretKey.Nonces.Nonces), this.xmssExecutionContext, xmssmtSecretKey.NodeCache?[id]);

			key.SetIndex(leafIndex);

			return key;
		}

		private void CheckValidIndex(XMSSMTPrivateKey xmssmtSecretKey) {
			if(xmssmtSecretKey.Index >= this.MaximumIndex) {
				throw new ArgumentException("The key index is higher than the key size");
			}
		}

		public async Task<ByteArray> Sign(ByteArray message, XMSSMTPrivateKey xmssmtSecretKey) {

			this.CheckValidIndex(xmssmtSecretKey);

			this.ClearCaches();
			long signatureIndex = xmssmtSecretKey.Index;

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			using ByteArray temp2 = XMSSCommonUtils.ToBytes(signatureIndex, this.digestLength);
			ByteArray random = XMSSCommonUtils.PRF(xmssmtSecretKey.SecretPrf, temp2, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);
			ByteArray temp = xmssmtSecretKey.Root;

			ByteArray concatenated = XMSSCommonUtils.Concatenate(random, temp, temp2);

			temp2.Return();

			ByteArray hasedMessage = this.xmssEngine.HashMessage(concatenated, message);

			concatenated.Return();

			long treeIndex = this.GetTreeIndex(signatureIndex, this.height - this.ReducedHeight);
			int leafIndex = this.GetLeafIndex(signatureIndex);

			adrs.LayerAddress = 0;
			adrs.TreeAddress = treeIndex;
			adrs.OtsAddress = leafIndex;

			XMSSPrivateKey xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);
			XMSSSignature.XMSSTreeSignature treeSig = await this.xmssEngine.TreeSig(hasedMessage, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

			hasedMessage.Return();

			using XMSSMTSignature xmssmtSignature = new XMSSMTSignature(random, signatureIndex, this.xmssExecutionContext);
			XMSSSignature xmssSignature = new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext);

			xmssmtSignature.Signatures.Add(adrs.LayerAddress, xmssSignature);

			for(int j = 1; j < this.layers; j++) {
				(ByteArray root, ByteArray backupRoot) = await this.xmssEngine.TreeHash(xmssSecretKey, 0, this.ReducedHeight, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);

				treeSig = await this.xmssEngine.TreeSig(root, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

				xmssSignature = new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext);

				xmssmtSignature.Signatures.Add(adrs.LayerAddress, xmssSignature);
			}

			this.ClearCaches();
			return xmssmtSignature.Save();
		}

		public Task<bool> Verify(ByteArray signature, ByteArray message, ByteArray publicKey) {

			this.ClearCaches();
			using XMSSMTSignature loadedSignature = new XMSSMTSignature(this.xmssExecutionContext);
			loadedSignature.Load(signature, this.wotsPlusProvider, this.height, this.layers);

			if(loadedSignature.Signatures.Count < this.layers) {
				throw new ArgumentException("Invalid amount of layers in signature");
			}

			using XMSSMTPublicKey loadedPublicKey = new XMSSMTPublicKey(this.xmssExecutionContext);
			loadedPublicKey.LoadKey(SafeArrayHandle.Wrap(publicKey));

			using ByteArray temp2 = XMSSCommonUtils.ToBytes(loadedSignature.Index, this.digestLength);

			using ByteArray concatenated = XMSSCommonUtils.Concatenate(loadedSignature.Random, loadedPublicKey.Root, temp2);

			temp2.Return();

			ByteArray hasedMessage = this.xmssEngine.HashMessage(concatenated, message);

			concatenated.Return();

			long signatureIndex = loadedSignature.Index;

			long treeIndex = this.GetTreeIndex(signatureIndex, this.height - this.ReducedHeight);
			int leafIndex = this.GetLeafIndex(signatureIndex);

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			adrs.LayerAddress = 0;
			adrs.TreeAddress = treeIndex;
			adrs.OtsAddress = leafIndex;

			XMSSSignature xmssSignature = loadedSignature.Signatures[0];

			(ByteArray node, ByteArray backupNode) = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, xmssSignature.XmssTreeSignature.BackupAuth, hasedMessage, loadedPublicKey.PublicSeed, adrs);

			hasedMessage.Return();

			for(int j = 1; j < this.layers; j++) {

				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				xmssSignature = loadedSignature.Signatures[j];

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				ByteArray backNode = node;
				(node, backupNode) = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, xmssSignature.XmssTreeSignature.BackupAuth, node, loadedPublicKey.PublicSeed, adrs);
				backNode.Return();
			}

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			bool result = ByteArray.EqualsConstantTime(loadedPublicKey.Root, node);

			node.Return();

			this.ClearCaches();
			
			return Task.FromResult(result);
		}

		/// <summary>
		///     Here we check the auth path of the next key and shake any nodes that we do not need anymore
		/// </summary>
		/// <param name="secretKey"></param>
		/// <param name="index"></param>
		/// <param name="publicSeed"></param>
		/// <param name="adrs"></param>
		/// <returns></returns>
		public void CleanAuthTree(XMSSMTPrivateKey xmssmtSecretKey) {

			if(xmssmtSecretKey == null) {
				return;
			}

			ImmutableList<XMSSNodeId> nodes = this.xmssEngine.BuildAuthTreeNodesList((int) xmssmtSecretKey.Index);

			foreach(XMSSNodeCache xmssNodeCache in xmssmtSecretKey.NodeCache.CachesTree.Values) {
				this.xmssEngine.ShakeAuthTree(xmssNodeCache, (int) xmssmtSecretKey.Index, nodes);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long GetTreeIndex(long index, int higherMask) {
			return (index >> this.ReducedHeight) & index & ((1L << higherMask) - 1L);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetLeafIndex(long index) {
			return (int) (index & ((1L << this.ReducedHeight) - 1L));
		}

		private void ClearCaches() {
			foreach(ByteArray[] entry in this.wotsPrivateSeedsCache.Values) {
				DoubleArrayHelper.Dispose(entry);
			}
			this.wotsPrivateSeedsCache.Clear();
		}
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.xmssEngine?.Dispose();

				this.ClearCaches();
				
				this.wotsPlusProvider?.Dispose();

				this.xmssExecutionContext?.Dispose();
			}

			this.IsDisposed = true;
		}

		~XMSSMTEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}