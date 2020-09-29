using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	// <summary>
	/// THE XMSS class
	/// </summary>
	/// <remarks>this was built according to the XMSS RFC https://tools.ietf.org/html/rfc8391</remarks>
	public class XMSSEngine : IDisposableExtended {

		/// <summary>
		///     How many processing loops do we take before resting and sleeping the thread?
		/// </summary>
		private const int LOOP_REST_COUNT = 10;

		private readonly int digestLength;
		private readonly int backupDigestLength;
		private readonly int height;

		private readonly object locker = new object();
		private readonly ThreadContext[] threadContexts;
		private readonly int threadCount;

		private readonly WotsPlus wotsPlusProvider;
		
		//TODO: caching everything is inneficient. we should use another type of cache with a maximum amount of nodes and better heuristics. i.e. we dont need to cache the level 0.
		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray[]> wotsPublicKeysCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray[]>();

		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray> wotsSecretSeedsCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray>();

		private readonly XMSSExecutionContext xmssExecutionContext;

		private XMSSOperationModes mode;
		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		/// <remarks>Can sign 2^height messages</remarks>
		public XMSSEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, WotsPlus wotsProvider, XMSSExecutionContext xmssExecutionContext, int height) {

			this.mode = mode;
			this.height = height;
			this.xmssExecutionContext = xmssExecutionContext;

			this.LeafCount = 1 << this.height;

			this.wotsPlusProvider = wotsProvider ?? new WotsPlus(threadMode, this.xmssExecutionContext);

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.backupDigestLength = this.xmssExecutionContext.BackupDigestSize;

			this.threadCount = XMSSCommonUtils.GetThreadCount(threadMode);

			if(mode.HasFlag(XMSSOperationModes.Signature)) {
				this.threadContexts = new ThreadContext[this.threadCount];
			}
		}

		private bool EnableCaches => this.xmssExecutionContext.EnableCaches;
		public int LeafCount { get; }
		public int MaximumIndex => 1 << this.height;

		public ByteArray GenerateWotsDeterministicPrivateSeed(ByteArray secretSeed, int nonce1, OtsHashAddress otsHashAddress) {

			XMSSMTLeafId addressId = otsHashAddress;

			bool enabledCache = this.EnableCaches && addressId.Layer != 0;
			if(enabledCache && this.wotsSecretSeedsCache.TryGetValue(addressId, out ByteArray cached)) {

				return cached;
			}

			int previousValue = otsHashAddress.KeyAndMask;
			otsHashAddress.KeyAndMask = nonce1;

			ByteArray result = XMSSCommonUtils.PRF(secretSeed, otsHashAddress, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

			// restore the previous
			otsHashAddress.KeyAndMask = previousValue;

			if(enabledCache) {
				this.wotsSecretSeedsCache.AddSafe(addressId, result);
			}

			return result;
		}

		private ByteArray[] GenerateWotsPublicKeyParallel(XMSSPrivateKey privateKey, ThreadContext threadContext) {

			XMSSMTLeafId addressId = threadContext.OtsHashAddress;

			bool enabledCache = this.EnableCaches && addressId.Layer != 0;
			if(enabledCache && this.wotsPublicKeysCache.TryGetValue(addressId, out ByteArray[] cached)) {

				return cached;
			}

			using ByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(privateKey.SecretSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce1, threadContext.OtsHashAddress);

			ByteArray[] wotsPublicKey = this.wotsPlusProvider.GeneratePublicKey(wotsPrivateSeed, privateKey.PublicSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce2, threadContext);

			if(enabledCache) {
				this.wotsPublicKeysCache.AddSafe(addressId, wotsPublicKey);
			}

			return wotsPublicKey;
		}

		public async Task<(XMSSPrivateKey privateKey, XMSSPublicKey publicKey)> GenerateKeys(Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Heuristic) {
			
			ByteArray publicSeed = null;
			ByteArray secretSeed = null;
			ByteArray secretSeedPrf = null;

			try {

				(publicSeed, secretSeed, secretSeedPrf) = XMSSCommonUtils.GenerateSeeds(this.xmssExecutionContext);

				// cache all our entries for immediate reuse
				this.ClearCaches();

				List<(int nonce1, int nonce2)> nonces = new List<(int nonce1, int nonce2)>();

				for(int i = 0; i < this.LeafCount; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
					nonces.Add((GlobalRandom.GetNext(), GlobalRandom.GetNext()));
#endif
				}

				nonces.Shuffle();

				// build our secret key
				XMSSPrivateKey secretKey = new XMSSPrivateKey(this.height, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces), this.xmssExecutionContext, cacheMode: cacheMode);

				// now lets generate our public xmss key
				OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
				adrs.Reset();

				//secretKey.Root = this.TreeHashOld(secretKey, 0, this.height, publicSeed, adrs);

				(secretKey.Root, secretKey.BackupRoot) = await this.TreeHash(secretKey, 0, this.height, publicSeed, adrs, progressCallback).ConfigureAwait(false);

				XMSSPublicKey publicKey = new XMSSPublicKey(publicSeed, secretKey.Root, secretKey.BackupRoot, this.xmssExecutionContext);
				this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);
				
				return (secretKey, publicKey);
			} finally {
				this.ClearCaches();
				
				publicSeed?.Dispose();
				secretSeed?.Dispose();
				secretSeedPrf?.Dispose();
			}
		}

		/// <summary>
		///     H function
		/// </summary>
		/// <param name="key"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public ByteArray Hash(ByteArray key, ByteArray buffer, XMSSCommonUtils.HashTypes hashType) {
			if(buffer == null) {

				throw new Exception("Buffer should not be null");
			}

			if(key == null) {

				throw new Exception("Key should not be null");
			}

			if((buffer.Length >> 1) != key.Length) {

				throw new Exception($"Buffer length {buffer.Length} should twice the Key length {key.Length}");
			}

			try {
				return XMSSCommonUtils.HashEntry(XMSSCommonUtils.HashCodes.H, key, buffer, this.xmssExecutionContext, hashType);
			} catch(Exception ex) {
				throw new Exception("Exception raised while hashing buffer and key", ex);
			}
		}

		// H_msg function
		public ByteArray HashMessage(ByteArray key, ByteArray buffer) {

			if(buffer == null) {
				throw new Exception("Buffer should not be null");
			}

			if(key == null) {
				throw new Exception("Key should not be null");
			}

			if(key.Length != (3 * this.xmssExecutionContext.DigestSize)) {
				throw new Exception("The key size is not the right size.");
			}

			try {
				return XMSSCommonUtils.HashEntry(XMSSCommonUtils.HashCodes.HMsg, key, buffer, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);
			} catch(Exception ex) {
				throw new Exception("Exception raised while hashing buffer and key", ex);
			}
		}

		public ByteArray RandHashParallel(ByteArray left, ByteArray right, ByteArray publicSeed, ThreadContext threadContext, XMSSCommonUtils.HashTypes hashType) {

			if(left == null) {
				throw new Exception("Left byte array should not be null");
			}

			if(right == null) {
				throw new Exception("Rigth byte array should not be null");
			}

			if(threadContext.HashTreeAddress == null) {
				throw new Exception("Address should not be null");
			}

			if(left.Length != right.Length) {
				throw new Exception($"Left length {left.Length} should equal to Right length {right.Length}");
			}

			int previousKeyMask = threadContext.randHashAddress.KeyAndMask;

			threadContext.randHashAddress.KeyAndMask = 0;
			using ByteArray key = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext, hashType);
			threadContext.randHashAddress.KeyAndMask = 1;
			ByteArray bm = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext, hashType);
			XMSSCommonUtils.Xor(bm, bm, left);
			bm.CopyTo(threadContext.RandHashFinalBuffer);
			bm.Return();

			threadContext.randHashAddress.KeyAndMask = 2;
			bm = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext, hashType);
			XMSSCommonUtils.Xor(bm, bm, right);
			bm.CopyTo(threadContext.RandHashFinalBuffer, bm.Length);
			bm.Return();

			// restore the value
			threadContext.randHashAddress.KeyAndMask = previousKeyMask;

			return this.Hash(key, threadContext.RandHashFinalBuffer, hashType);
		}

		// Algorithm 7: RAND_HASH 
		public ByteArray RandHash(ByteArray left, ByteArray right, ByteArray publicSeed, CommonAddress adrs, XMSSCommonUtils.HashTypes hashType, int digestSize) {

			if(left == null) {
				throw new Exception("Left byte array should not be null");
			}

			if(right == null) {
				throw new Exception("Rigth byte array should not be null");
			}

			if(adrs == null) {
				throw new Exception("Address should not be null");
			}

			if(left.Length != right.Length) {
				throw new Exception($"Left length {left.Length} should equal to Right length {right.Length}");
			}

			CommonAddress tmpAdrs = null;

			switch(adrs) {
				case LTreeAddress _:
					tmpAdrs = this.xmssExecutionContext.LTreeAddressPool.GetObject();
					((LTreeAddress) tmpAdrs).Initialize(adrs);

					break;
				case HashTreeAddress _:
					tmpAdrs = this.xmssExecutionContext.HashTreeAddressPool.GetObject();
					((HashTreeAddress) tmpAdrs).Initialize(adrs);

					break;
				default: throw new ArgumentException();
			}

			using ByteArray randHashFinalBuffer = ByteArray.Create(digestSize << 1);
			tmpAdrs.KeyAndMask = 0;
			using ByteArray key = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext, hashType);
			tmpAdrs.KeyAndMask = 1;
			ByteArray bm = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext, hashType);
			XMSSCommonUtils.Xor(bm, bm, left);
			bm.CopyTo(randHashFinalBuffer);
			bm.Return();

			tmpAdrs.KeyAndMask = 2;
			bm = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext, hashType);
			XMSSCommonUtils.Xor(bm, bm, right);
			bm.CopyTo(randHashFinalBuffer, bm.Length);
			bm.Return();

			switch(tmpAdrs) {
				case LTreeAddress lTreeAddress:
					this.xmssExecutionContext.LTreeAddressPool.PutObject(lTreeAddress);

					break;
				case HashTreeAddress hashTreeAddress:
					this.xmssExecutionContext.HashTreeAddressPool.PutObject(hashTreeAddress);

					break;
			}

			return this.Hash(key, randHashFinalBuffer, hashType);
		}

		public ByteArray LTree(ByteArray[] publicKey, ByteArray publicSeed, LTreeAddress adrs, ThreadContext threadContext = null) {
			adrs.TreeHeight = 0;

			//NOTE: here we do a shallow copy, so we MUST NOT return any memory here. Public key still owns the buffers
			ByteArray[] publicKeyClone = DoubleArrayHelper.CloneShallow(publicKey);

			int lenPrime = this.wotsPlusProvider.Len;

			List<ByteArray> created = new List<ByteArray>();
			while(lenPrime > 1) {
				for(int i = 0; i < (int) Math.Floor((decimal) lenPrime / 2); i++) {
					adrs.TreeIndex = i;

					// build the secretseet
					if(threadContext != null) {
						publicKeyClone[i] = this.RandHashParallel(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, threadContext, XMSSCommonUtils.HashTypes.Regular);
					} else {
						publicKeyClone[i] = this.RandHash(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, adrs, XMSSCommonUtils.HashTypes.Regular, this.digestLength);
					}
					// add what we just created to clean
					created.Add(publicKeyClone[i]);
				}

				if((lenPrime % 2) == 1) {
					int index = (int) Math.Floor((decimal) lenPrime / 2);
					publicKeyClone[index] = publicKeyClone[lenPrime - 1];
				}

				lenPrime = (int) Math.Ceiling((decimal) lenPrime / 2);
				adrs.TreeHeight += 1;
			}

			var result = publicKeyClone[0].Clone();

			// now dispose only the ones we created here. we dont want to delete the ones from the shallow copy.
			foreach(var entry in created) {
				entry.Dispose();
			}
			return result;
		}

		public async Task<(ByteArray root, ByteArray backupRoot)> TreeHash(XMSSPrivateKey privateKey, int startIndex, int targetNodeHeight, ByteArray publicSeed, OtsHashAddress adrs, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Heuristic) {
			if((startIndex % (1 << targetNodeHeight)) != 0) {
				return (null, null);
			}

			// clean up
			this.incompleteNodes.Clear();

			for(int i = 0; i < this.readyNodes.Count; i++) {
				this.readyNodes.TryDequeue(out XmssNodeInfo entry);
			}

			// first, parse the tree, find nodes that need computing

			(ByteArray root, ByteArray backupRoot) = this.PrepareHashTreeWorkNodes((startIndex >> targetNodeHeight, targetNodeHeight), default, XmssNodeInfo.Directions.None, privateKey.NodeCache);

			int totalNodes = this.readyNodes.Count + this.incompleteNodes.Count;
			int completedNodes = 0;

			if(root != null) {
				// we have our root, it was most probably cached
				return (root, backupRoot);
			}

			// ok, now lets compute our tree
			bool completed = false;

			this.ResetThreadContexts();

			int Callback(int index) {
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				ThreadContext threadContext = this.threadContexts[index];
				XmssNodeInfo workingXmssNode = null;
				int counter = 0;

				while((!completed && (workingXmssNode != null)) || this.readyNodes.TryDequeue(out workingXmssNode) || !this.incompleteNodes.IsEmpty) {
					// ok, lets process the node
					if(workingXmssNode != null) {
						if(workingXmssNode.IsCompleted) {
							// it was already done, lets keep going
							workingXmssNode?.Dispose();
							
							workingXmssNode = null;
							continue;
						}

						if(workingXmssNode is MergedXmssNodeInfo merged) {

							// hash left and right together
							threadContext.HashTreeAddress.Initialize(adrs);
							threadContext.HashTreeAddress.TreeHeight = (byte)(workingXmssNode.Id.Height - 1); // the height of the children
							threadContext.HashTreeAddress.TreeIndex = workingXmssNode.Id.Index;

							threadContext.randHashAddress = threadContext.HashTreeAddress;
							
							// here we do our double hash in the tree nodes
							workingXmssNode.Hash = this.RandHashParallel(merged.Left.root, merged.Right.root, publicSeed, threadContext, XMSSCommonUtils.HashTypes.Regular);
							workingXmssNode.Hash2 = this.RandHashParallel(merged.Left.backupRoot, merged.Right.backupRoot, publicSeed, threadContext, XMSSCommonUtils.HashTypes.Backup);
							
							threadContext.randHashAddress = null;
						} else {
							// hash single node
							threadContext.OtsHashAddress.Initialize(adrs);
							threadContext.OtsHashAddress.OtsAddress = workingXmssNode.Id.Index;
							
							threadContext.LTreeAddress.Initialize(adrs);
							threadContext.LTreeAddress.LtreeAddress = workingXmssNode.Id.Index;
							threadContext.randHashAddress = threadContext.LTreeAddress;
							
							ByteArray[] wotsPublicKey = this.GenerateWotsPublicKeyParallel(privateKey, threadContext);

							workingXmssNode.Hash = this.LTree(wotsPublicKey, publicSeed, threadContext.LTreeAddress);
							workingXmssNode.Hash2 = workingXmssNode.Hash;
							
							threadContext.randHashAddress = null;
						}

						// here we would add the node to cache
						Interlocked.Increment(ref completedNodes);
						privateKey.NodeCache.Cache(workingXmssNode.Id, (workingXmssNode.Hash, workingXmssNode.Hash2));

						if(workingXmssNode.Id.Height == targetNodeHeight) {
							if(!workingXmssNode.IsCompleted) {
								//  this is bad, we are done but we have no result
								throw new ApplicationException("Failed to find the root node.");
							}

							// its the root, we are done!

							(root, backupRoot) = workingXmssNode.ReleaseHashes();
							workingXmssNode?.Dispose();
							
							workingXmssNode = null;
							// stop the other threads too, we are done
							completed = true;

							break;
						}

						if(this.incompleteNodes.TryGetValue(workingXmssNode.Parent, out MergedXmssNodeInfo parent)) {
							if(workingXmssNode.Direction == XmssNodeInfo.Directions.Left) {
								parent.Left = workingXmssNode.ReleaseHashes();
							} else {
								parent.Right = workingXmssNode.ReleaseHashes();
							}

							workingXmssNode?.Dispose();
							
							workingXmssNode = null;
							if(!parent.AreChildrenReady) {
								continue;
							}

							lock(this.locker) {
								// move it to the ready queue
								if(this.incompleteNodes.TryRemove(parent.Id, out parent)) {
									// its ready, let's process it right away
									workingXmssNode = parent;
								}
							}
						} else {
							workingXmssNode?.Dispose();
							
							workingXmssNode = null;
						}

						if(counter >= LOOP_REST_COUNT) {
							counter = 0;

							// ok, we reached our counter limit, let's sleep a bit to be nice with the rest of the system
							Thread.Sleep(10);
						} else {
							counter++;
						}
					} else {
						// ok, we have nothing, let's see who is available
						lock(this.locker) {
							// simply pick the next in line
							KeyValuePair<XMSSNodeId, MergedXmssNodeInfo> entry = this.incompleteNodes.FirstOrDefault(e => e.Value.AreChildrenReady);

							if(entry.Value != null) {

								this.incompleteNodes.RemoveSafe(entry.Key);
								workingXmssNode = entry.Value;
							}
						}

						if(workingXmssNode == null) {
							// we found nothing, lets sleep a bit until something becomes available
							Thread.Sleep(10);
							counter = 0;
						}
					}
				}

				return 1;
			}

			Task[] tasks = new Task[this.threadCount];

			for(int i = 0; i < this.threadCount; i++) {
				this.threadContexts[i].OtsHashAddress.Initialize(adrs);
				this.threadContexts[i].LTreeAddress.Initialize(adrs);
				this.threadContexts[i].HashTreeAddress.Initialize(adrs);

				int index = i;

				tasks[i] = Task.Run(() => Callback(index));
			}

			int lastPercentage = 0;

			if(progressCallback != null) {
				await progressCallback(lastPercentage).ConfigureAwait(false);
			}

			while(true) {
				if(Task.WaitAll(tasks, TimeSpan.FromSeconds(1))) {
					if(progressCallback != null) {
						await progressCallback(100).ConfigureAwait(false);
					}

					break;
				}

				int progress = Interlocked.CompareExchange(ref completedNodes, 0, 0);

				// get the % progress
				int percentage = (int) Math.Ceiling(((decimal) progress / totalNodes) * 100);

				if(lastPercentage != percentage) {
					lastPercentage = percentage;

					if(progressCallback != null) {
						await progressCallback(percentage).ConfigureAwait(false);
					}
				}
			}

			this.ClearThreadContexts();
			this.ClearNodeBuffers();
			
			// ok, we are done! return the root

			return (root, backupRoot);
		}

		private void ResetThreadContexts() {

			this.ClearThreadContexts();
			if(this.mode.HasFlag(XMSSOperationModes.Signature)) {
				for(int i = 0; i < this.threadContexts.Length; i++) {
					this.threadContexts[i] = new ThreadContext(this.xmssExecutionContext);
				}
			}
		}
		
		private void ClearThreadContexts() {
			for(int i = 0; i < this.threadContexts.Length; i++) {
				this.threadContexts[i]?.Dispose();
				this.threadContexts[i] = null;
			}
		}
		
		/// <summary>
		///     Prepare the tree of work that needs to be done to find a root node
		/// </summary>
		/// <param name="id"></param>
		/// <param name="parentId"></param>
		/// <param name="direction"></param>
		/// <param name="nodeCache"></param>
		/// <returns></returns>
		private (ByteArray root, ByteArray backupRoot) PrepareHashTreeWorkNodes(XMSSNodeId id, XMSSNodeId parentId, XmssNodeInfo.Directions direction, XMSSNodeCache nodeCache) {

			var cached = nodeCache[id];

			if(cached.root != null) {
				// we have it, lets stop here and return the cached entry
				return cached;
			}

			if(id.Height == 0) {

				// this one is ready to compute
				XmssNodeInfo leafXmssNode = new XmssNodeInfo();
				leafXmssNode.Id = id;
				leafXmssNode.Direction = direction;
				leafXmssNode.Parent = parentId;
				this.readyNodes.Enqueue(leafXmssNode);

				return (null, null);
			}

			MergedXmssNodeInfo xmssNode = new MergedXmssNodeInfo();

			xmssNode.Id = id;

			xmssNode.Left = this.PrepareHashTreeWorkNodes((2 * id.Index, id.Height - 1), id, XmssNodeInfo.Directions.Left, nodeCache);
			xmssNode.Right = this.PrepareHashTreeWorkNodes(((2 * id.Index) + 1, id.Height - 1), id, XmssNodeInfo.Directions.Right, nodeCache);
			xmssNode.Direction = direction;
			xmssNode.Parent = parentId;

			if(xmssNode.AreChildrenReady) {
				// its ready to be processed
				this.readyNodes.Enqueue(xmssNode);
			} else {
				// we will come back to it later
				this.incompleteNodes.AddSafe(id, xmssNode);
			}

			// always null, the node is not computed yet
			return (null, null);
		}

		internal async Task<(ByteArray root, ByteArray backupRoot)[]> BuildAuth(XMSSPrivateKey secretKey, int index, ByteArray publicSeed, OtsHashAddress adrs) {
			(ByteArray root, ByteArray backupRoot)[] auth = new (ByteArray root, ByteArray backupRoot)[this.height];

			bool cacheEnabled = this.EnableCaches;
			
			try {
				// we will need the caches now, since we will repeat a lot of computations
				this.xmssExecutionContext.EnableCaches = true;

				//TODO: paralellise
				for(int j = 0; j < this.height; j++) {
					int expo = 1 << j;
					int k = (int) Math.Floor((decimal) index / expo) ^ 1;

					auth[j] = await this.TreeHash(secretKey, k * expo, j, publicSeed, adrs).ConfigureAwait(false);
				}
			} finally {
				this.xmssExecutionContext.EnableCaches = cacheEnabled;

				if(!cacheEnabled) {
					this.ClearCaches();
				}
			}
			
			return auth;
		}

		/// <summary>
		/// build the indices of the authentication path required for the desired index
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public ImmutableList<XMSSNodeId> BuildAuthTreeNodesList(int index) {
			if(index >= this.LeafCount) {
				return null;
			}

			List<XMSSNodeId> nodes = new List<XMSSNodeId>();

			//TODO: paralellise?
			for(int j = 0; j <= this.height; j++) {
				int expo = 1 << j;
				int k = (int) Math.Floor((decimal) index / expo) ^ 1;

				if(j == this.height) {
					// this is the root
					nodes.Add((0, this.height));
				} else {
					XMSSNodeId? node = this.BuildTreePath(k * expo, j);

					if(node.HasValue) {
						nodes.Add(node.Value);
					}
				}
			}

			ImmutableList<XMSSNodeId> result = nodes.ToImmutableList();

			return result;
		}

		public void ShakeAuthTree(XMSSNodeCache xmssNodeCache, int index, ImmutableList<XMSSNodeId> nodes) {
			if(xmssNodeCache == null) {

				return;
			}

			// now remove the nodes that are not shared
			List<XMSSNodeId> excludedNodes = xmssNodeCache.NodeIds.Where(n => (n.Height < (this.height - XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY)) && (n != (index + 1, 0)) && !nodes.Contains(n)).ToList();

			if(excludedNodes.Any()) {
				xmssNodeCache.ClearNodes(excludedNodes);
			}

		}

		public void CleanAuthTree(XMSSPrivateKey secretKey) {
			this.CleanAuthTree(secretKey.NodeCache, secretKey.Index);
		}

		/// <summary>
		///     Here we check the auth path of the next key and shake any nodes that we do not need anymore
		/// </summary>
		/// <param name="secretKey"></param>
		/// <param name="index"></param>
		/// <param name="publicSeed"></param>
		/// <param name="adrs"></param>
		/// <returns></returns>
		public void CleanAuthTree(XMSSNodeCache xmssNodeCache, int index) {

			if(xmssNodeCache == null) {
				return;
			}

			index = 5;

			// lets take the auth path for this index and 2 more only
			List<XMSSNodeId> allNodes = new List<XMSSNodeId>();

			foreach(int entry in Enumerable.Range(index, 3)) {
				ImmutableList<XMSSNodeId> result = this.BuildAuthTreeNodesList(entry);

				if(result != null) {
					allNodes.AddRange(result);
				}
			}

			// make them unique
			ImmutableList<XMSSNodeId> remainingNodes = allNodes.Distinct().ToImmutableList();

			// and shake!
			this.ShakeAuthTree(xmssNodeCache, index, remainingNodes);

		}

		public XMSSNodeId? BuildTreePath(int startIndex, int targetNodeHeight) {

			if((startIndex % (1 << targetNodeHeight)) != 0) {
				return null;
			}

			int limit = 1 << targetNodeHeight;

			Stack<XMSSNodeId> stack = new Stack<XMSSNodeId>();

			//TODO: make parallel
			for(int i = 0; i < limit; i++) {
				int index = startIndex + i;

				XMSSNodeId node = (index, 0);

				byte treeHeight = 0;
				int treeIndex = index;

				XMSSNodeId? peekXmssNode = stack.Any() ? stack.Peek() : (XMSSNodeId?) null;

				while(peekXmssNode.HasValue && (peekXmssNode.Value.Height == node.Height)) {
					treeIndex = (treeIndex - 1) / 2;
					treeHeight += 1;

					stack.Pop();

					node = (treeIndex, node.Height + 1);

					peekXmssNode = stack.Any() ? stack.Peek() : (XMSSNodeId?) null;
				}

				stack.Push(node);
			}

			return stack.Any() ? stack.Pop() : (XMSSNodeId?) null;
		}

		internal async Task<XMSSSignature.XMSSTreeSignature> TreeSig(ByteArray message, XMSSPrivateKey xmssSecretKey, int signatureIndex, ByteArray publicSeed, OtsHashAddress adrs) {
			
			(ByteArray root, ByteArray backupRoot)[] auth = await this.BuildAuth(xmssSecretKey, signatureIndex, publicSeed, adrs).ConfigureAwait(false);

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);
			otsHashAddress.OtsAddress = signatureIndex;

			using ByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(xmssSecretKey.SecretSeed, xmssSecretKey.Nonce1, otsHashAddress);

			ByteArray[] otsSignature = this.wotsPlusProvider.GenerateSignature(message, wotsPrivateSeed, publicSeed, xmssSecretKey.Nonce2, otsHashAddress);

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			XMSSSignature.XMSSTreeSignature result = new XMSSSignature.XMSSTreeSignature(otsSignature, auth, this.xmssExecutionContext);
			
			return result;
		}

		private void CheckValidIndex(XMSSPrivateKey xmssSecretKey) {
			if(xmssSecretKey.Index >= this.MaximumIndex) {
				throw new ArgumentException("The key index is higher than the key size");
			}
		}

		public async Task<ByteArray> Sign(ByteArray message, XMSSPrivateKey xmssSecretKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null) {
			
			this.ClearCaches();
			int signatureIndex = xmssSecretKey.Index;

			this.CheckValidIndex(xmssSecretKey);
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			using ByteArray temp2 = XMSSCommonUtils.ToBytes(signatureIndex, this.digestLength);
			using ByteArray random = XMSSCommonUtils.PRF(xmssSecretKey.SecretPrf, temp2, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);
			using ByteArray temp = xmssSecretKey.Root.Clone();

			using ByteArray concatenated = XMSSCommonUtils.Concatenate(random, temp, temp2);
			
			using ByteArray hashedMessage = this.HashMessage(concatenated, message);
			
			XMSSSignature.XMSSTreeSignature treeSig = await this.TreeSig(hashedMessage, xmssSecretKey, signatureIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			ImmutableList<XMSSNodeId> authNodeList = null;
			if(buildOptimizedSignature && xmssSignaturePathCache != null) {
				this.VerifySignaturePathCache(signatureIndex, xmssSignaturePathCache);
				authNodeList = this.BuildAuthTreeNodesList(signatureIndex);
				
				xmssSignaturePathCache?.AdjustTreeSignAuth(treeSig, authNodeList);
			}
			using XMSSSignature signature = new XMSSSignature(random, signatureIndex, treeSig, this.xmssExecutionContext);

			if(buildOptimizedSignature) {
				
				xmssSignaturePathCache?.UpdateFromSignature(signature, authNodeList);
			}
			
			this.ClearCaches();
			
			return signature.Save();
		}

		private void VerifySignaturePathCache(int signatureIndex, XMSSSignaturePathCache xmssSignaturePathCache) {

			if(xmssSignaturePathCache.TreeHeight != this.height) {
				throw new ApplicationException("Invalid tree height.");
			}
			if(signatureIndex != 0 && xmssSignaturePathCache.Index != signatureIndex-1) {
				throw new ApplicationException("Invalid signature index.");
			}
		}

		public (ByteArray node, ByteArray backupNode) XmssRootFromSig(int leafIndex, ByteArray[] otsSignature, ByteArray[] auth, ByteArray[] backupAuth, ByteArray hashedMessage, ByteArray publicSeed, OtsHashAddress adrs) {

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);

			ByteArray[] publickey = this.wotsPlusProvider.GeneratePublicKeyFromSignature(hashedMessage, otsSignature, publicSeed, otsHashAddress);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			LTreeAddress lTreeAddress = this.xmssExecutionContext.LTreeAddressPool.GetObject();
			lTreeAddress.Reset();
			lTreeAddress.TreeAddress = adrs.TreeAddress;
			lTreeAddress.LayerAddress = adrs.LayerAddress;
			lTreeAddress.LtreeAddress = adrs.OtsAddress;

			ByteArray xmssNode = this.LTree(publickey, publicSeed, lTreeAddress);
			ByteArray xmssBackupNode = xmssNode;
			
			this.xmssExecutionContext.LTreeAddressPool.PutObject(lTreeAddress);

			DoubleArrayHelper.Return(publickey);

			HashTreeAddress hashTreeAddress = this.xmssExecutionContext.HashTreeAddressPool.GetObject();
			hashTreeAddress.Reset();
			hashTreeAddress.TreeAddress = adrs.TreeAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.TreeIndex = adrs.OtsAddress;

			for(int k = 0; k < this.height; k++) {
				hashTreeAddress.TreeHeight = k;

				ByteArray randHash = null;
				ByteArray randBackupHash = null;

				if((Math.Floor((decimal) leafIndex / (1 << k)) % 2) == 0) {
					hashTreeAddress.TreeIndex /= 2;
					randHash = this.RandHash(xmssNode, auth[k], publicSeed, hashTreeAddress, XMSSCommonUtils.HashTypes.Regular, this.digestLength);
					randBackupHash = this.RandHash(xmssBackupNode, backupAuth[k], publicSeed, hashTreeAddress, XMSSCommonUtils.HashTypes.Backup, this.backupDigestLength);
					
				} else {
					hashTreeAddress.TreeIndex = (hashTreeAddress.TreeIndex - 1) / 2;
					randHash = this.RandHash(auth[k], xmssNode, publicSeed, hashTreeAddress, XMSSCommonUtils.HashTypes.Regular, this.digestLength);
					randBackupHash = this.RandHash(backupAuth[k], xmssBackupNode, publicSeed, hashTreeAddress, XMSSCommonUtils.HashTypes.Backup, this.backupDigestLength);
				}

				xmssNode.Return();
				xmssNode = randHash;
				xmssBackupNode.Return();
				xmssBackupNode = randBackupHash;
			}

			this.xmssExecutionContext.HashTreeAddressPool.PutObject(hashTreeAddress);

			// thats it, our result
			return (xmssNode, xmssBackupNode);
		}

		public Task<bool> Verify(ByteArray signature, ByteArray message, ByteArray publicKey, XMSSSignaturePathCache xmssSignaturePathCache = null) {
			
			this.ClearCaches();
			using XMSSSignature loadedSignature = this.LoadSignature(signature, xmssSignaturePathCache);

			using XMSSPublicKey loadedPublicKey = new XMSSPublicKey(this.xmssExecutionContext);
			loadedPublicKey.LoadKey(SafeArrayHandle.Wrap(publicKey));

			using ByteArray temp2 = XMSSCommonUtils.ToBytes(loadedSignature.Index, this.digestLength);

			using ByteArray concatenated = XMSSCommonUtils.Concatenate(loadedSignature.Random, loadedPublicKey.Root, temp2);

			using ByteArray hashedMessage = this.HashMessage(concatenated, message);
			
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();
			adrs.OtsAddress = loadedSignature.Index;

			(ByteArray root, ByteArray backupNode) = this.XmssRootFromSig(loadedSignature.Index, loadedSignature.XmssTreeSignature.otsSignature, loadedSignature.XmssTreeSignature.Auth, loadedSignature.XmssTreeSignature.BackupAuth, hashedMessage, loadedPublicKey.PublicSeed, adrs);
			
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			bool result = ByteArray.EqualsConstantTime(loadedPublicKey.Root, root) && ByteArray.EqualsConstantTime(loadedPublicKey.BackupRoot, backupNode);

			root.Return();
			backupNode.Return();
			
			this.ClearCaches();
			
			return Task.FromResult(result);
		}

		public XMSSSignature LoadSignature(ByteArray signature, XMSSSignaturePathCache xmssSignaturePathCache = null) {
			XMSSSignature loadedSignature = new XMSSSignature(this.xmssExecutionContext);
			loadedSignature.Load(signature, this.wotsPlusProvider, this.height);

			if(loadedSignature.Optimized && xmssSignaturePathCache != null) {
				xmssSignaturePathCache.RestoreSignature(loadedSignature);
			}

			return loadedSignature;
		}
	#region TREE HASH
		
		
		private class XmssNodeInfo : IDisposableExtended {
			public enum Directions {
				None,
				Left,
				Right
			}
			
			public XMSSNodeId Parent { get; set; }
			public ByteArray Hash { get; set; }
			public ByteArray Hash2 { get; set; } = null;
			public XMSSNodeId Id { get; set; }
			public Directions Direction { get; set; }

			public bool IsCompleted => (this.Hash != null) && this.Hash.HasData;
			
		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			public (ByteArray Hash, ByteArray Hash2) ReleaseHashes() {

				var hash = this.Hash;
				var hash2 = this.Hash2;
				this.Hash = null;
				this.Hash2 = null;
				
				return (hash, hash2);
			}
			
			private void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {
					this.DisposeAll(disposing);
				}

				this.IsDisposed = true;
			}

			protected virtual void DisposeAll(bool disposing) {
				if(disposing) {
					this.Hash?.Dispose();
					this.Hash = null;
					
					this.Hash2?.Dispose();
					this.Hash2 = null;
					
					this.Direction = Directions.None;
					this.Parent = default;
				}
			}
			
			~XmssNodeInfo() {
				this.Dispose(false);
			}

		#endregion
		}

		private class MergedXmssNodeInfo : XmssNodeInfo {

			public (ByteArray root, ByteArray backupRoot) Left { get; set; }
			public (ByteArray root, ByteArray backupRoot) Right { get; set; }

			public bool AreChildrenReady => (this.Left.root != null) && (this.Right.root != null);
			protected override void DisposeAll(bool disposing) {
				base.DisposeAll(disposing);
				
				this.Left.root?.Dispose();
				this.Left.backupRoot?.Dispose();
				
				this.Right.root?.Dispose();
				this.Right.backupRoot?.Dispose();
			}
		}

		private readonly ConcurrentDictionary<XMSSNodeId, MergedXmssNodeInfo> incompleteNodes = new ConcurrentDictionary<XMSSNodeId, MergedXmssNodeInfo>();
		private readonly WrapperConcurrentQueue<XmssNodeInfo> readyNodes = new WrapperConcurrentQueue<XmssNodeInfo>();

		public class ThreadContext : IDisposableExtended {

			public readonly HashTreeAddress HashTreeAddress;
			public readonly LTreeAddress LTreeAddress;
			public readonly OtsHashAddress OtsHashAddress;

			public readonly ByteArray RandHashFinalBuffer;
			private readonly XMSSExecutionContext xmssExecutionContext;

			public CommonAddress randHashAddress;

			public ThreadContext(XMSSExecutionContext xmssExecutionContext) {
				this.xmssExecutionContext = xmssExecutionContext;
				this.HashTreeAddress = xmssExecutionContext.HashTreeAddressPool.GetObject();
				this.OtsHashAddress = xmssExecutionContext.OtsHashAddressPool.GetObject();
				this.LTreeAddress = xmssExecutionContext.LTreeAddressPool.GetObject();

				this.RandHashFinalBuffer = ByteArray.Create(xmssExecutionContext.DigestSize << 1);
			}

		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}
			
			protected virtual void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {

					this.xmssExecutionContext.HashTreeAddressPool.PutObject(this.HashTreeAddress);
					this.xmssExecutionContext.OtsHashAddressPool.PutObject(this.OtsHashAddress);
					this.xmssExecutionContext.LTreeAddressPool.PutObject(this.LTreeAddress);
					this.RandHashFinalBuffer.Dispose();
				}

				this.IsDisposed = true;
			}

			~ThreadContext() {
				this.Dispose(false);
			}

		#endregion

		}

	#endregion

		private void ClearNodeBuffers() {
			foreach(var node in this.readyNodes) {
				node.Entry?.Dispose();
			}
			this.readyNodes.Clear();
				
			foreach(var node in this.incompleteNodes) {
				node.Value?.Dispose();
			}
			this.incompleteNodes.Clear();
		}

		private void ClearCaches() {
			foreach(ByteArray[] entry in this.wotsPublicKeysCache.Values) {
				DoubleArrayHelper.Dispose(entry);
			}
			this.wotsPublicKeysCache.Clear();

			foreach(ByteArray entry in this.wotsSecretSeedsCache.Values) {
				entry?.Dispose();
			}
			this.wotsSecretSeedsCache.Clear();
		}
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.ClearCaches();

				this.ClearThreadContexts();

				this.wotsPlusProvider?.Dispose();

				this.xmssExecutionContext?.Dispose();

				this.ClearNodeBuffers();
			}

			this.IsDisposed = true;
		}

		~XMSSEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}