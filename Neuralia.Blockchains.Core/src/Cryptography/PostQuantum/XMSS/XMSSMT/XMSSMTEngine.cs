using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;
using Nito.AsyncEx.Synchronous;
using Zio.FileSystems;

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
		
		private readonly byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		/// XMSS^MT can take up A LOT of ram. if true, the node cache will be saved to disk while building the whole key. False will use ram
		/// </summary>
		public bool SaveToDisk { get; set; } = false;
		public bool ClearWorkingFolder { get; set; } = true;
		public string WorkingFolderPath { get; set; }
		
		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		/// <remarks>Can sign 2^height messages. More layers (example 4 layers vs 2 layers) make key generation dramatically faster but signature size twice as large, signature time twice as fast and verification time twice as long.</remarks>
		public XMSSMTEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, XMSSExecutionContext xmssExecutionContext, int height, int layers, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) {

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

			this.ReducedHeight = (byte)(this.height / this.layers);
			this.LeafCount = 1L << this.height;
			this.ReducedLeafCount = 1L << this.ReducedHeight;

			this.noncesExponent = noncesExponent;
			this.digestLength = this.xmssExecutionContext.DigestSize;

			this.xmssEngine = new XMSSEngine(mode, threadMode, this.wotsPlusProvider, this.xmssExecutionContext, this.ReducedHeight);
		}

		private bool EnableCaches => this.xmssExecutionContext.EnableCaches;
		public long LeafCount { get; }
		public byte ReducedHeight { get; }
		public long ReducedLeafCount { get; }
		public int MaximumIndex => 1 << this.ReducedHeight;

		public void SetXmssSaveToDisk(bool saveToDisk, string workingFolderPath = null, bool clearWorkingFolder = true) {
			this.xmssEngine.SaveToDisk = saveToDisk;
			this.xmssEngine.WorkingFolderPath = workingFolderPath;
			this.xmssEngine.ClearWorkingFolder = clearWorkingFolder;
		}
		
		public ByteArray[] GenerateWotsDeterministicPrivateSeeds(ByteArray secretSeed, short nonce, XMSSMTLeafId index) {

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

		private class KeyContext {
			public long Tree { get; set; }
			public int Layer { get; set; }
		}
		
		public async Task<(XMSSMTPrivateKey privateKey, XMSSMTPublicKey publicKey)> GenerateKeys(int? seedSize =  null, bool buildCache = true, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) {

			ByteArray publicSeed = null;
			ByteArray secretSeed = null;
			ByteArray secretSeedPrf = null;

			string folderPath = this.WorkingFolderPath;

			if(string.IsNullOrWhiteSpace(folderPath)) {
				folderPath = Path.Combine(Path.GetTempPath(), "XMSS-MT-Cache");
			}
			FileSystemWrapper fileSystem = null;
			try {

				string contextFile = Path.Combine(folderPath, $"context");
				string privateKeyFile = Path.Combine(folderPath, $"private-key");

				XMSSMTPrivateKey secretKey = null;
				if(this.SaveToDisk) {
					fileSystem = new FileSystemWrapper(new PhysicalFileSystem());
					FileExtensions.EnsureDirectoryStructure(folderPath);

					if(fileSystem.FileExists(privateKeyFile)) {
						using var bytes = FileExtensions.ReadAllBytes(privateKeyFile, fileSystem);
						secretKey = new XMSSMTPrivateKey(this.xmssExecutionContext);
						secretKey.LoadKey(bytes);

						publicSeed = secretKey.PublicSeed.Clone();
						secretSeed = secretKey.SecretSeed.Clone();
						secretSeedPrf = secretKey.SecretPrf.Clone();
					}
				}

				this.ClearCaches();

				
				if(secretKey == null) {
					(publicSeed, secretSeed, secretSeedPrf) = XMSSCommonUtils.GenerateSeeds(seedSize, this.xmssExecutionContext);
					
					List<(short nonce1, short nonce2)> nonces = new List<(short nonce1, short nonce2)>();

					for(int i = 0; i < (this.ReducedLeafCount >> this.noncesExponent) + 1; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
						nonces.Add((GlobalRandom.GetNextShort(), GlobalRandom.GetNextShort()));
#endif
					}

					nonces.Shuffle();

					secretKey = new XMSSMTPrivateKey(this.height, this.layers, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces, this.noncesExponent), this.xmssExecutionContext, cacheMode: cacheMode, cacheLevels: cacheLevels);
					
					// dummy defaults
					secretKey.Root = ByteArray.Create(this.xmssExecutionContext.DigestSize);
					secretKey.BackupRoot = ByteArray.Create(this.xmssExecutionContext.DigestSize);
					
					if(this.SaveToDisk) {
						using var bytes = secretKey.SaveKey();
						FileExtensions.WriteAllBytes(privateKeyFile, bytes, fileSystem);
					}
				}
				
				// now lets prepare the public key
				int lastLayer = this.layers - 1;

				OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();

				long totalTrees = 0;
				long totalProcessedTrees = 0;
				
				for(int layer = 0; layer < this.layers; layer++) {
					totalTrees += (1 << ((this.layers - 1 - layer) * this.ReducedHeight));
				}
				
				ClosureWrapper<double> totalPct = 1;
				ClosureWrapper<double> singleTreePct = 1.0 / totalTrees;

				KeyContext context = null;
				if(this.SaveToDisk && fileSystem.FileExists(contextFile)) {

					context = Newtonsoft.Json.JsonConvert.DeserializeObject<KeyContext>(fileSystem.ReadAllText(contextFile));
				} else {
					context = new KeyContext();
				}

				int startingLayer = context.Layer;
				long startingTree = context.Tree;
				
				XMSSEngine.SaveContext saveContext = new XMSSEngine.SaveContext();

				if(this.SaveToDisk) {
					saveContext.ContextFile = Path.Combine(this.xmssEngine.WorkingFolderPath, $"context");
					saveContext.IncompleteNodesKeysFile = Path.Combine(this.xmssEngine.WorkingFolderPath, $"incomplete-nodes-keys");
					saveContext.IncompleteNodesValuesFile = Path.Combine(this.xmssEngine.WorkingFolderPath, $"incomplete-nodes-values");
					saveContext.FileSystem = fileSystem;
				}

				for(int layer = startingLayer; layer < this.layers; layer++) {

					ClosureWrapper<double> layerPct = 1;

					if(layer > startingLayer) {
						// reset it to increase level
						startingTree = 0;
					}
					
					int layerTrees = (1 << ((this.layers - 1 - layer) * this.ReducedHeight));
					ClosureWrapper<double> singleLayerTreePct = 1.0 / layerTrees;
					
					for(long tree = startingTree; tree <  layerTrees; tree++) {

						layerPct.Value = (double)tree / layerTrees;
						totalPct.Value = (double)totalProcessedTrees / totalTrees;
						
						if(this.SaveToDisk) {
							context.Tree = tree;
							context.Layer = layer;

							fileSystem.WriteAllText(contextFile, Newtonsoft.Json.JsonConvert.SerializeObject(context));
						}
						
						adrs.Reset();

						adrs.LayerAddress = layer;
						adrs.TreeAddress = tree;
						XMSSNodeCache nodeCache = null;
						XMSSPrivateKey xmssPrivateKey = null;
						if(this.SaveToDisk) {
							nodeCache = secretKey.NodeCache[adrs].Clone;

							xmssPrivateKey = this.BuildXmssPrivateKey(adrs, secretKey, 0, nodeCache);
						} else {
							xmssPrivateKey = this.BuildXmssPrivateKey(adrs, secretKey, 0);
							nodeCache = xmssPrivateKey.NodeCache;
						}

						using(xmssPrivateKey) {
							// build the node tree
							long tree1 = tree;
							int layer1 = layer;

							(ByteArray root, ByteArray backupRoot) = await this.xmssEngine.TreeHash(xmssPrivateKey, 0, this.ReducedHeight, publicSeed, adrs, nodeCache, pct => {

								if(progressCallback != null) {
									int layerPercentage = (int) ((layerPct.Value + (singleLayerTreePct.Value * ((double) pct / 100))) * 100);
									int totalPercentage = (int) ((totalPct.Value + (singleTreePct.Value * ((double) pct / 100))) * 100);

									return progressCallback(pct, layerPercentage, totalPercentage, tree1+1, layerTrees, totalTrees, layer1+1, this.layers);
								}

								return Task.CompletedTask;
							}, saveContext:saveContext ).ConfigureAwait(false);

							if(layer == lastLayer) {
								secretKey.Root = root;
								secretKey.BackupRoot = backupRoot;
							} else {
								root?.Dispose();
								backupRoot?.Dispose();
							}

							if(this.SaveToDisk) {
								// here we save it to disk, save up some RAM
								using var dehydrator = DataSerializationFactory.CreateDehydrator();
								nodeCache.Dehydrate(dehydrator);
								using var nodeCacheBytes = dehydrator.ToArray();

								string file = Path.Combine(folderPath, $"cache{tree}-{layer}");
								FileExtensions.WriteAllBytes(file, nodeCacheBytes, fileSystem);

								// clear the memory, we saved the cache
								nodeCache.Dispose();
							} else {
								// so it wont be disposed, since we keep it in the xmss^mt key
								xmssPrivateKey.ClearNodeCache();
							}

							totalProcessedTrees++;
							this.xmssEngine.ClearCaches();
						}
					}
				}

				this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

				if(this.SaveToDisk) {
					// rebuild the node cache
					for(int layer = 0; layer < this.layers; layer++) {
						int layerTrees = (1 << ((this.layers - 1 - layer) * this.ReducedHeight));

						for(long tree = 0; tree < layerTrees; tree++) {

							string file = Path.Combine(folderPath, $"cache{tree}-{layer}");
							using var bytes = FileExtensions.ReadAllBytes(file, fileSystem);
							using var rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
							secretKey.NodeCache[(tree, layer)].Rehydrate(rehydrator);
						}
					}
				}

				if(this.SaveToDisk && this.ClearWorkingFolder) {

					if(fileSystem.DirectoryExists(folderPath)) {
						foreach(var file in fileSystem.EnumerateFiles(folderPath)) {
							SecureWipe.WipeFile(folderPath, fileSystem, 20).WaitAndUnwrapException();
						}
						Directory.Delete(folderPath, true);
					}
				}
				
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

		private XMSSPrivateKey BuildXmssPrivateKey(XMSSMTreeId id, XMSSMTPrivateKey xmssmtSecretKey, int leafIndex, XMSSNodeCache nodeCache) {

			XMSSPrivateKey key = new XMSSPrivateKey(this.ReducedHeight, xmssmtSecretKey.PublicSeed, xmssmtSecretKey.SecretSeed, xmssmtSecretKey.SecretPrf, new XMSSNonceSet(xmssmtSecretKey.Nonces), this.xmssExecutionContext);

			key.SetIndex(leafIndex);

			return key;
		}
		
		private XMSSPrivateKey BuildXmssPrivateKey(XMSSMTreeId id, XMSSMTPrivateKey xmssmtSecretKey, int leafIndex) {

			return this.BuildXmssPrivateKey(id, xmssmtSecretKey, leafIndex, xmssmtSecretKey.NodeCache?[id]);
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
			using ByteArray random = XMSSCommonUtils.PRF(xmssmtSecretKey.SecretPrf, temp2, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);
			ByteArray temp = xmssmtSecretKey.Root;

			using ByteArray concatenated = XMSSCommonUtils.Concatenate(random, temp, temp2);
			
			using ByteArray hashedMessage = this.xmssEngine.HashMessage(concatenated, message);


			long treeIndex = this.GetTreeIndex(signatureIndex, this.height - this.ReducedHeight);
			int leafIndex = this.GetLeafIndex(signatureIndex);

			adrs.LayerAddress = 0;
			adrs.TreeAddress = treeIndex;
			adrs.OtsAddress = leafIndex;

			XMSSPrivateKey xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);
			
			using XMSSMTSignature xmssmtSignature = new XMSSMTSignature(random, signatureIndex, this.xmssExecutionContext);

			XMSSSignature.XMSSTreeSignature	treeSig = await this.xmssEngine.TreeSig(hashedMessage, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

			xmssmtSignature.Signatures.Add(adrs.LayerAddress, new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext));
			

			for(int j = 1; j < this.layers; j++) {
				(ByteArray root, ByteArray backupRoot) = await this.xmssEngine.TreeHash(xmssSecretKey, 0, this.ReducedHeight, xmssSecretKey.PublicSeed, adrs, xmssSecretKey.NodeCache).ConfigureAwait(false);
				
				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				xmssSecretKey?.Dispose();
				xmssSecretKey = this.BuildXmssPrivateKey(adrs, xmssmtSecretKey, leafIndex);

				using(root) {
					using(backupRoot) {

						treeSig = await this.xmssEngine.TreeSig(root, xmssSecretKey, leafIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);
						xmssmtSignature.Signatures.Add(adrs.LayerAddress, new XMSSSignature(random, leafIndex, treeSig, this.xmssExecutionContext));
					}
				}
			}

			xmssSecretKey?.Dispose();
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
			
			using ByteArray hashedMessage = this.xmssEngine.HashMessage(concatenated, message);
			
			long signatureIndex = loadedSignature.Index;

			long treeIndex = this.GetTreeIndex(signatureIndex, this.height - this.ReducedHeight);
			int leafIndex = this.GetLeafIndex(signatureIndex);

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			adrs.LayerAddress = 0;
			adrs.TreeAddress = treeIndex;
			adrs.OtsAddress = leafIndex;

			XMSSSignature xmssSignature= loadedSignature.Signatures[0];

			(ByteArray node, ByteArray backupNode) = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, xmssSignature.XmssTreeSignature.BackupAuth, hashedMessage, loadedPublicKey.PublicSeed, adrs);

			for(int j = 1; j < this.layers; j++) {

				treeIndex = this.GetTreeIndex(treeIndex, this.height - (j * this.ReducedHeight));
				leafIndex = this.GetLeafIndex(treeIndex);

				xmssSignature = loadedSignature.Signatures[j];

				adrs.LayerAddress = j;
				adrs.TreeAddress = treeIndex;
				adrs.OtsAddress = leafIndex;

				ByteArray backNode = node;
				ByteArray backBackupNode = backupNode;
				(node, backupNode) = this.xmssEngine.XmssRootFromSig(leafIndex, xmssSignature.XmssTreeSignature.otsSignature, xmssSignature.XmssTreeSignature.Auth, xmssSignature.XmssTreeSignature.BackupAuth, node, loadedPublicKey.PublicSeed, adrs);
				backNode.Return();
				backBackupNode.Return();
			}

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);
			
			bool result = ByteArray.EqualsConstantTime(loadedPublicKey.Root, node) && ByteArray.EqualsConstantTime(loadedPublicKey.BackupRoot, backupNode);

			node.Return();
			backupNode.Return();

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

			XMSSNodeId[] nodes = this.xmssEngine.BuildAuthTreeNodesList((int) xmssmtSecretKey.Index);

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