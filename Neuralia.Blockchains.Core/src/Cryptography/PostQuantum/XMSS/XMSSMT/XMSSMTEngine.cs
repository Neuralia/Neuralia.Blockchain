using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Tools;
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

		private readonly int winternitz;
		private readonly WotsPlusEngine wotsPlusProvider;

		private readonly Dictionary<XMSSMTreeId, ByteArray[]> wotsPrivateSeedsCache = new Dictionary<XMSSMTreeId, ByteArray[]>();
		private readonly Dictionary<XMSSMTLeafId, ByteArray[]> wotsPublicKeysCache = new Dictionary<XMSSMTLeafId, ByteArray[]>();

		private readonly XMSSEngine xmssEngine;

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		public XMSSMTEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, XMSSExecutionContext xmssExecutionContext, int height, int layers, WinternitzParameter wParam = WinternitzParameter.Param16) {

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

			this.ReducedHeight = this.height / this.layers;
			this.LeafCount = 1 << this.height;
			this.ReducedLeafCount = 1 << this.ReducedHeight;

			this.xmssExecutionContext = xmssExecutionContext;

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.winternitz = (int) wParam;

			this.wotsPlusProvider = new WotsPlusEngine(mode, threadMode, this.xmssExecutionContext, wParam);

			this.xmssEngine = new XMSSEngine(mode, threadMode, this.wotsPlusProvider, this.xmssExecutionContext, this.ReducedHeight, (WinternitzParameter) this.winternitz);
		}

		public int LeafCount { get; }
		public int ReducedHeight { get; }
		public int ReducedLeafCount { get; }
		public int MaximumIndex => 1 << this.ReducedHeight;

		public ByteArray[] GenerateWotsDeterministicPrivateSeeds(ByteArray secretSeed, int nonce, XMSSMTLeafId index) {

			if(this.wotsPrivateSeedsCache.ContainsKey(index)) {
				return this.wotsPrivateSeedsCache[index];
			}

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Reset();
			otsHashAddress.OtsAddress = (int) index.Index;
			otsHashAddress.LayerAddress = index.Layer;
			otsHashAddress.TreeAddress = index.Tree;

			ByteArray[] result = this.wotsPlusProvider.GeneratePseudorandomPrivateKeys(secretSeed, nonce, otsHashAddress);

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			this.wotsPrivateSeedsCache.Add(index, result);

			return result;
		}

		public (XMSSMTPrivateKey privateKey, XMSSMTPublicKey publicKey) GenerateKeys() {

			(ByteArray publicSeed, ByteArray secretSeed, ByteArray secretSeedPrf) = CommonUtils.GenerateSeeds(this.xmssExecutionContext);

			this.wotsPublicKeysCache.Clear();
			this.wotsPrivateSeedsCache.Clear();

			var nonces = new Dictionary<XMSSMTLeafId, (int nonce1, int nonce2)>();

			for(int layer = 0; layer < this.layers; layer++) {
				for(int tree = 0; tree < (1 << ((this.layers - 1 - layer) * this.ReducedHeight)); tree++) {
					for(int i = 0; i < this.LeafCount; i++) {
#if DETERMINISTIC_DEBUG
				nonces.Add((i,tree, layer), (i,i));
#else
						nonces.Add((i, tree, layer), (this.xmssExecutionContext.Random.NextInt(), this.xmssExecutionContext.Random.NextInt()));
#endif
					}
				}
			}

			XMSSMTPrivateKey secretKey = new XMSSMTPrivateKey(this.height, this.layers, publicSeed, secretSeed, secretSeedPrf, new XMSSMTNonceSet(nonces), this.xmssExecutionContext);

			// now lets prepare the public key
			int lastLayer = this.layers - 1;

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			adrs.LayerAddress = lastLayer;
			adrs.TreeAddress = 0;

			XMSSPrivateKey xmssPrivateKey = this.BuildXmssPrivateKey(adrs, secretKey, 0);

			secretKey.Root = this.xmssEngine.TreeHash(xmssPrivateKey, 0, this.ReducedHeight, publicSeed, adrs);

			XMSSMTPublicKey publicKey = new XMSSMTPublicKey(publicSeed, secretKey.Root.Clone(), this.xmssExecutionContext);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			return (secretKey, publicKey);
		}

		private XMSSPrivateKey BuildXmssPrivateKey(XMSSMTreeId id, XMSSMTPrivateKey xmssmtSecretKey, int leafIndex) {

			var nonces = xmssmtSecretKey.Nonces.Nonces.Where(e => (e.Key.Tree == id.Tree) && (e.Key.Layer == id.Layer)).OrderBy(e => e.Key.Index).Select(e => e.Value).ToList();
			XMSSPrivateKey key = new XMSSPrivateKey(this.ReducedHeight, xmssmtSecretKey.PublicSeed, xmssmtSecretKey.SecretSeed, xmssmtSecretKey.SecretPrf, new XMSSNonceSet(nonces), this.xmssExecutionContext, xmssmtSecretKey.NodeCache?[id]);

			key.SetIndex(leafIndex);

			return key;
		}

		public ByteArray Sign(ByteArray message, XMSSMTPrivateKey xmssmtSecretKey) {

			long signatureIndex = xmssmtSecretKey.Index;

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			ByteArray temp2 = CommonUtils.ToBytes(signatureIndex, this.digestLength);
			ByteArray random = CommonUtils.PRF(xmssmtSecretKey.SecretPrf, temp2, this.xmssExecutionContext);
			ByteArray temp = xmssmtSecretKey.Root;

			ByteArray concatenated = CommonUtils.Concatenate(random, temp, temp2);

			temp2.Return();

			ByteArray hasedMessage = this.xmssEngine.HashMessage(concatenated, message);

			concatenated.Return();

			long treeIndex = this.GetTreeIndex(signatureIndex, this.height - this.ReducedHeight);
			int leafIndex = this.GetLeafIndex(signatureIndex);

			adrs.LayerAddress = 0;
			adrs.TreeAddress = treeIndex;
			adrs.OtsAddress = leafIndex;

			XMSSPrivateKey xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);
			XMSSSignature.XMSSTreeSignature treeSig = this.xmssEngine.TreeSig(hasedMessage, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs);

			hasedMessage.Return();

			XMSSMTSignature xmssmtSignature = new XMSSMTSignature(random, signatureIndex, this.xmssExecutionContext);
			XMSSSignature xmssSignature = new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext);

			xmssmtSignature.Signatures.Add(adrs.LayerAddress, xmssSignature);

			for(int j = 1; j < this.layers; j++) {
				ByteArray root = this.xmssEngine.TreeHash(xmssSecretKey, 0, this.ReducedHeight, xmssSecretKey.PublicSeed, adrs);

				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);

				treeSig = this.xmssEngine.TreeSig(root, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs);

				xmssSignature = new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext);

				xmssmtSignature.Signatures.Add(adrs.LayerAddress, xmssSignature);
			}

			ByteArray result = xmssmtSignature.Save();

			xmssmtSignature.Dispose();

			return result;
		}

		public bool Verify(ByteArray signature, ByteArray message, ByteArray publicKey) {

			XMSSMTSignature loadedSignature = new XMSSMTSignature(this.xmssExecutionContext);
			loadedSignature.Load(signature, this.wotsPlusProvider, this.height, this.layers);

			if(loadedSignature.Signatures.Count < this.layers) {
				throw new ArgumentException("Invalid amount of layers in signature");
			}

			XMSSMTPublicKey loadedPublicKey = new XMSSMTPublicKey(this.xmssExecutionContext);
			loadedPublicKey.LoadKey(publicKey);

			ByteArray temp2 = CommonUtils.ToBytes(loadedSignature.Index, this.digestLength);

			ByteArray concatenated = CommonUtils.Concatenate(loadedSignature.Random, loadedPublicKey.Root, temp2);

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

			ByteArray node = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, hasedMessage, loadedPublicKey.PublicSeed, adrs);

			hasedMessage.Return();

			for(int j = 1; j < this.layers; j++) {

				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				xmssSignature = loadedSignature.Signatures[j];

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				ByteArray backNode = node;
				node = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, node, loadedPublicKey.PublicSeed, adrs);
				backNode.Return();
			}

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			bool result = CommonUtils.EqualsConstantTime(loadedPublicKey.Root, node);

			node.Return();

			loadedSignature.Dispose();
			loadedPublicKey.Dispose();

			return result;
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

			var nodes = this.xmssEngine.BuildAuthTreeNodesList((int) xmssmtSecretKey.Index);

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

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.xmssEngine?.Dispose();

				foreach(ByteArray[] entry in this.wotsPublicKeysCache.Values) {
					DoubleArrayHelper.Dispose(entry);
				}

				foreach(ByteArray[] entry in this.wotsPrivateSeedsCache.Values) {
					DoubleArrayHelper.Dispose(entry);
				}

				this.wotsPlusProvider?.Dispose();
				this.wotsPublicKeysCache.Clear();

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