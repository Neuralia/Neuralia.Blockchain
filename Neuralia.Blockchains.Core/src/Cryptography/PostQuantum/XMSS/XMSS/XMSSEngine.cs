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
		private readonly int height;

		private readonly object locker = new object();
		private readonly ThreadContext[] threadContexts;
		private readonly int threadCount;

		private readonly WotsPlus wotsPlusProvider;
		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray[]> wotsPublicKeysCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray[]>();

		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray> wotsSecretSeedsCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray>();

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		/// <remarks>Can sign 2^height messages</remarks>
		public XMSSEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, WotsPlus wotsProvider, XMSSExecutionContext xmssExecutionContext, int height) {

			this.height = height;
			this.xmssExecutionContext = xmssExecutionContext;

			this.LeafCount = 1 << this.height;

			this.wotsPlusProvider = wotsProvider ?? new WotsPlus(threadMode, this.xmssExecutionContext);

			this.digestLength = this.xmssExecutionContext.DigestSize;

			this.threadCount = XMSSCommonUtils.GetThreadCount(threadMode);

			if(mode.HasFlag(XMSSOperationModes.Signature)) {
				this.threadContexts = new ThreadContext[this.threadCount];

				for(int i = 0; i < this.threadCount; i++) {
					this.threadContexts[i] = new ThreadContext(this.xmssExecutionContext);
				}
			}
		}

		public int LeafCount { get; }
		public int MaximumIndex => 1 << this.height;

		public ByteArray GenerateWotsDeterministicPrivateSeed(ByteArray secretSeed, int nonce1, OtsHashAddress otsHashAddress) {

			XMSSMTLeafId addressId = otsHashAddress;

			if(this.wotsSecretSeedsCache.TryGetValue(addressId, out ByteArray cached)) {

				return cached;
			}

			int previousValue = otsHashAddress.KeyAndMask;
			otsHashAddress.KeyAndMask = nonce1;

			ByteArray result = XMSSCommonUtils.PRF(secretSeed, otsHashAddress, this.xmssExecutionContext);

			// restore the previous
			otsHashAddress.KeyAndMask = previousValue;

			this.wotsSecretSeedsCache.AddSafe(addressId, result);

			return result;
		}

		private ByteArray[] GenerateWotsPublicKeyParallel(XMSSPrivateKey privateKey, ThreadContext threadContext) {

			XMSSMTLeafId addressId = threadContext.OtsHashAddress;

			if(this.wotsPublicKeysCache.TryGetValue(addressId, out ByteArray[] cached)) {

				return cached;
			}

			ByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(privateKey.SecretSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce1, threadContext.OtsHashAddress);

			ByteArray[] wotsPublicKey = this.wotsPlusProvider.GeneratePublicKey(wotsPrivateSeed, privateKey.PublicSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce2, threadContext);

			this.wotsPublicKeysCache.AddSafe(addressId, wotsPublicKey);

			return wotsPublicKey;
		}

		public async Task<(XMSSPrivateKey privateKey, XMSSPublicKey publicKey)> GenerateKeys(Func<int, Task> progressCallback = null) {
			(ByteArray publicSeed, ByteArray secretSeed, ByteArray secretSeedPrf) = XMSSCommonUtils.GenerateSeeds(this.xmssExecutionContext);

			// cache all our entries for immadiate reuse
			this.wotsPublicKeysCache.Clear();

			List<(int nonce1, int nonce2)> nonces = new List<(int nonce1, int nonce2)>();

			for(int i = 0; i < this.LeafCount; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
				nonces.Add((this.xmssExecutionContext.Random.NextInt(), this.xmssExecutionContext.Random.NextInt()));
#endif
			}

			nonces.Shuffle();

			// build our secret key
			XMSSPrivateKey secretKey = new XMSSPrivateKey(this.height, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces), this.xmssExecutionContext);

			// now lets generate our public xmss key
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			//secretKey.Root = this.TreeHashOld(secretKey, 0, this.height, publicSeed, adrs);

			secretKey.Root = await this.TreeHash(secretKey, 0, this.height, publicSeed, adrs, progressCallback).ConfigureAwait(false);

			XMSSPublicKey publicKey = new XMSSPublicKey(publicSeed, secretKey.Root.Clone(), this.xmssExecutionContext);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			return (secretKey, publicKey);
		}

		/// <summary>
		///     H function
		/// </summary>
		/// <param name="key"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public ByteArray Hash(ByteArray key, ByteArray buffer) {
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
				return XMSSCommonUtils.HashEntry(XMSSCommonUtils.HashCodes.H, key, buffer, this.xmssExecutionContext);
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
				return XMSSCommonUtils.HashEntry(XMSSCommonUtils.HashCodes.HMsg, key, buffer, this.xmssExecutionContext);
			} catch(Exception ex) {
				throw new Exception("Exception raised while hashing buffer and key", ex);
			}
		}

		public ByteArray RandHashParallel(ByteArray left, ByteArray right, ByteArray publicSeed, ThreadContext threadContext) {

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
			ByteArray key = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			threadContext.randHashAddress.KeyAndMask = 1;
			ByteArray bm = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			XMSSCommonUtils.Xor(bm, bm, left);
			bm.CopyTo(threadContext.RandHashFinalBuffer);
			bm.Return();

			threadContext.randHashAddress.KeyAndMask = 2;
			bm = XMSSCommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			XMSSCommonUtils.Xor(bm, bm, right);
			bm.CopyTo(threadContext.RandHashFinalBuffer, bm.Length);
			bm.Return();

			// restore the value
			threadContext.randHashAddress.KeyAndMask = previousKeyMask;

			ByteArray tmp = this.Hash(key, threadContext.RandHashFinalBuffer);

			key.Return();

			return tmp;
		}

		// Algorithm 7: RAND_HASH 
		public ByteArray RandHash(ByteArray left, ByteArray right, ByteArray publicSeed, CommonAddress adrs) {

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

			ByteArray randHashFinalBuffer = ByteArray.Create(this.digestLength << 1);
			tmpAdrs.KeyAndMask = 0;
			ByteArray key = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
			tmpAdrs.KeyAndMask = 1;
			ByteArray bm = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
			XMSSCommonUtils.Xor(bm, bm, left);
			bm.CopyTo(randHashFinalBuffer);
			bm.Return();

			tmpAdrs.KeyAndMask = 2;
			bm = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
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

			ByteArray tmp = this.Hash(key, randHashFinalBuffer);

			key.Return();
			randHashFinalBuffer.Return();

			return tmp;
		}

		public ByteArray LTree(ByteArray[] publicKey, ByteArray publicSeed, LTreeAddress adrs, ThreadContext threadContext = null) {
			adrs.TreeHeight = 0;

			//NOTE: here we do a shallow copy, so we MUST NOT return any memory here. Public key still owns the buffers
			ByteArray[] publicKeyClone = DoubleArrayHelper.CloneShallow(publicKey);

			int lenPrime = this.wotsPlusProvider.Len;

			while(lenPrime > 1) {
				for(int i = 0; i < (int) Math.Floor((decimal) lenPrime / 2); i++) {
					adrs.TreeIndex = i;

					// build the secretseet
					if(threadContext != null) {
						publicKeyClone[i] = this.RandHashParallel(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, threadContext);
					} else {
						publicKeyClone[i] = this.RandHash(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, adrs);
					}
				}

				if((lenPrime % 2) == 1) {
					int index = (int) Math.Floor((decimal) lenPrime / 2);
					publicKeyClone[index] = publicKeyClone[lenPrime - 1];
				}

				lenPrime = (int) Math.Ceiling((decimal) lenPrime / 2);
				adrs.TreeHeight += 1;
			}

			ByteArray result = publicKeyClone[0].Clone();

			return result;
		}

		public async Task<ByteArray> TreeHash(XMSSPrivateKey privateKey, int startIndex, int targetNodeHeigth, ByteArray publicSeed, OtsHashAddress adrs, Func<int, Task> progressCallback = null) {
			if((startIndex % (1 << targetNodeHeigth)) != 0) {
				return null;
			}

			// clean up
			this.incompleteNodes.Clear();

			for(int i = 0; i < this.readyNodes.Count; i++) {
				this.readyNodes.TryDequeue(out XmssNodeInfo entry);
			}

			// first, parse the tree, find nodes that need computing

			ByteArray root = this.PrepareHashTreeWorkNodes((startIndex >> targetNodeHeigth, targetNodeHeigth), default, XmssNodeInfo.Directions.None, privateKey.NodeCache);

			int totalNodes = this.readyNodes.Count + this.incompleteNodes.Count;
			int completedNodes = 0;

			if(root != null) {
				// we have our root, it was most probably cached
				return root;
			}

			// ok, now lets compute our tree
			bool completed = false;

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
							workingXmssNode = null;

							continue;
						}

						if(workingXmssNode is MergedXmssNodeInfo merged) {

							// hash left and right together
							threadContext.HashTreeAddress.Initialize(adrs);
							threadContext.HashTreeAddress.TreeHeight = workingXmssNode.Id.Height - 1; // the height of the children
							threadContext.HashTreeAddress.TreeIndex = workingXmssNode.Id.Index;

							threadContext.randHashAddress = threadContext.HashTreeAddress;
							workingXmssNode.Hash = this.RandHashParallel(merged.Left, merged.Right, publicSeed, threadContext);
							threadContext.randHashAddress = null;
						} else {
							// hash single node
							threadContext.OtsHashAddress.Initialize(adrs);
							threadContext.OtsHashAddress.OtsAddress = workingXmssNode.Id.Index;

							ByteArray[] wotsPublicKey = this.GenerateWotsPublicKeyParallel(privateKey, threadContext);

							threadContext.LTreeAddress.Initialize(adrs);
							threadContext.LTreeAddress.LtreeAddress = workingXmssNode.Id.Index;

							threadContext.randHashAddress = threadContext.LTreeAddress;
							workingXmssNode.Hash = this.LTree(wotsPublicKey, publicSeed, threadContext.LTreeAddress);
							threadContext.randHashAddress = null;
						}

						// here we would add the node to cache
						Interlocked.Increment(ref completedNodes);
						privateKey.NodeCache.Cache(workingXmssNode.Id, workingXmssNode.Hash);

						if(workingXmssNode.Id.Height == targetNodeHeigth) {
							if(!workingXmssNode.IsCompleted) {
								//  this is bad, we are done but we have no result
								throw new ApplicationException("Failed to find the root node.");
							}

							// its the root, we are done!
							root = workingXmssNode.Hash;

							// stop the other threads too, we are done
							completed = true;

							break;
						}

						if(this.incompleteNodes.TryGetValue(workingXmssNode.Parent, out MergedXmssNodeInfo parent)) {
							if(workingXmssNode.Direction == XmssNodeInfo.Directions.Left) {
								parent.Left = workingXmssNode.Hash;
							} else {
								parent.Right = workingXmssNode.Hash;
							}

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

			// ok, we are done! return the root

			return root;
		}

		/// <summary>
		///     Prepare the tree of work that needs to be done to find a root node
		/// </summary>
		/// <param name="id"></param>
		/// <param name="parentId"></param>
		/// <param name="direction"></param>
		/// <param name="nodeCache"></param>
		/// <returns></returns>
		private ByteArray PrepareHashTreeWorkNodes(XMSSNodeId id, XMSSNodeId parentId, XmssNodeInfo.Directions direction, XMSSNodeCache nodeCache) {

			ByteArray cached = nodeCache[id];

			if(cached != null) {
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

				return null;
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

			// always null, the ndoe is not computed yet
			return null;
		}

		internal async Task<ByteArray[]> BuildAuth(XMSSPrivateKey secretKey, int index, ByteArray publicSeed, OtsHashAddress adrs) {
			ByteArray[] auth = new ByteArray[this.height];

			//TODO: paralellise
			for(int j = 0; j < this.height; j++) {
				int expo = 1 << j;
				int k = (int) Math.Floor((decimal) index / expo) ^ 1;

				auth[j] = await this.TreeHash(secretKey, k * expo, j, publicSeed, adrs).ConfigureAwait(false);
			}

			return auth;
		}

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

		public XMSSNodeId? BuildTreePath(int startIndex, int targetNodeHeigth) {

			if((startIndex % (1 << targetNodeHeigth)) != 0) {
				return null;
			}

			int limit = 1 << targetNodeHeigth;

			Stack<XMSSNodeId> stack = new Stack<XMSSNodeId>();

			//TODO: make parallel
			for(int i = 0; i < limit; i++) {
				int index = startIndex + i;

				XMSSNodeId node = (index, 0);

				int treeHeight = 0;
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
			ByteArray[] auth = await this.BuildAuth(xmssSecretKey, signatureIndex, publicSeed, adrs).ConfigureAwait(false);

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);
			otsHashAddress.OtsAddress = signatureIndex;

			ByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(xmssSecretKey.SecretSeed, xmssSecretKey.Nonce1, otsHashAddress);

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

		public async Task<ByteArray> Sign(ByteArray message, XMSSPrivateKey xmssSecretKey) {
			int signatureIndex = xmssSecretKey.Index;

			this.CheckValidIndex(xmssSecretKey);
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			ByteArray temp2 = XMSSCommonUtils.ToBytes(signatureIndex, this.digestLength);
			ByteArray random = XMSSCommonUtils.PRF(xmssSecretKey.SecretPrf, temp2, this.xmssExecutionContext);
			ByteArray temp = xmssSecretKey.Root;

			ByteArray concatenated = XMSSCommonUtils.Concatenate(random, temp, temp2);

			temp2.Return();

			ByteArray hasedMessage = this.HashMessage(concatenated, message);

			concatenated.Return();

			XMSSSignature.XMSSTreeSignature treeSig = await this.TreeSig(hasedMessage, xmssSecretKey, signatureIndex, xmssSecretKey.PublicSeed, adrs).ConfigureAwait(false);

			hasedMessage.Return();
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			XMSSSignature signature = new XMSSSignature(random, signatureIndex, treeSig, this.xmssExecutionContext);

			ByteArray result = signature.Save();

			signature.Dispose();

			return result;
		}

		public ByteArray XmssRootFromSig(int leafIndex, ByteArray[] otsSignature, ByteArray[] auth, ByteArray hasedMessage, ByteArray publicSeed, OtsHashAddress adrs) {

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);

			ByteArray[] publickey = this.wotsPlusProvider.GeneratePublicKeyFromSignature(hasedMessage, otsSignature, publicSeed, otsHashAddress);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			LTreeAddress lTreeAddress = this.xmssExecutionContext.LTreeAddressPool.GetObject();
			lTreeAddress.Reset();
			lTreeAddress.TreeAddress = adrs.TreeAddress;
			lTreeAddress.LayerAddress = adrs.LayerAddress;
			lTreeAddress.LtreeAddress = adrs.OtsAddress;

			ByteArray xmssNode = this.LTree(publickey, publicSeed, lTreeAddress);
			this.xmssExecutionContext.LTreeAddressPool.PutObject(lTreeAddress);

			DoubleArrayHelper.Return(publickey);

			HashTreeAddress hashTreeAddress = this.xmssExecutionContext.HashTreeAddressPool.GetObject();
			hashTreeAddress.Reset();
			hashTreeAddress.TreeAddress = adrs.TreeAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.TreeIndex = adrs.OtsAddress;

			for(int k = 0; k < this.height; k++) {
				hashTreeAddress.TreeHeight = k;

				ByteArray randHash = null;

				if((Math.Floor((decimal) leafIndex / (1 << k)) % 2) == 0) {
					hashTreeAddress.TreeIndex /= 2;
					randHash = this.RandHash(xmssNode, auth[k], publicSeed, hashTreeAddress);
				} else {
					hashTreeAddress.TreeIndex = (hashTreeAddress.TreeIndex - 1) / 2;
					randHash = this.RandHash(auth[k], xmssNode, publicSeed, hashTreeAddress);
				}

				xmssNode.Return();
				xmssNode = randHash;
			}

			this.xmssExecutionContext.HashTreeAddressPool.PutObject(hashTreeAddress);

			// thats it, our result
			return xmssNode;
		}

		public Task<bool> Verify(ByteArray signature, ByteArray message, ByteArray publicKey) {
			XMSSSignature loadedSignature = new XMSSSignature(this.xmssExecutionContext);
			loadedSignature.Load(signature, this.wotsPlusProvider, this.height);

			XMSSPublicKey loadedPublicKey = new XMSSPublicKey(this.xmssExecutionContext);
			loadedPublicKey.LoadKey(publicKey);

			ByteArray temp2 = XMSSCommonUtils.ToBytes(loadedSignature.Index, this.digestLength);

			ByteArray concatenated = XMSSCommonUtils.Concatenate(loadedSignature.Random, loadedPublicKey.Root, temp2);

			temp2.Return();

			ByteArray hasedMessage = this.HashMessage(concatenated, message);

			concatenated.Return();

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();
			adrs.OtsAddress = loadedSignature.Index;

			ByteArray node = this.XmssRootFromSig(loadedSignature.Index, loadedSignature.XmssTreeSignature.otsSignature, loadedSignature.XmssTreeSignature.Auth, hasedMessage, loadedPublicKey.PublicSeed, adrs);

			hasedMessage.Return();

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			bool result = XMSSCommonUtils.EqualsConstantTime(loadedPublicKey.Root, node);

			node.Return();

			loadedSignature.Dispose();
			loadedPublicKey.Dispose();

			return Task.FromResult(result);
		}

	#region TREE HASH

		private class XmssNodeInfo {
			public enum Directions {
				None,
				Left,
				Right
			}

			public XMSSNodeId Parent { get; set; }
			public ByteArray Hash { get; set; }
			public XMSSNodeId Id { get; set; }
			public Directions Direction { get; set; }

			public bool IsCompleted => (this.Hash != null) && this.Hash.HasData;
		}

		private class MergedXmssNodeInfo : XmssNodeInfo {

			public ByteArray Left { get; set; }
			public ByteArray Right { get; set; }

			public bool AreChildrenReady => (this.Left != null) && (this.Right != null);
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

			public void Return() {
				this.xmssExecutionContext.HashTreeAddressPool.PutObject(this.HashTreeAddress);
				this.xmssExecutionContext.OtsHashAddressPool.PutObject(this.OtsHashAddress);
				this.xmssExecutionContext.LTreeAddressPool.PutObject(this.LTreeAddress);
				this.RandHashFinalBuffer.Return();
			}

			protected virtual void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {
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

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				foreach(ByteArray[] entry in this.wotsPublicKeysCache.Values) {
					DoubleArrayHelper.Dispose(entry);
				}

				foreach(ByteArray entry in this.wotsSecretSeedsCache.Values) {
					entry?.Dispose();
				}

				for(int i = 0; i < this.threadCount; i++) {
					this.threadContexts[i].Dispose();
				}

				this.wotsPlusProvider?.Dispose();
				this.wotsPublicKeysCache.Clear();

				this.xmssExecutionContext?.Dispose();
			}

			this.IsDisposed = true;
		}

		~XMSSEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}