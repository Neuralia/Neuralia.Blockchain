using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	// <summary>
	/// THE XMSS class
	/// </summary>
	/// <remarks>this was built according to the XMSS RFC https://tools.ietf.org/html/rfc8391</remarks>
	public class XMSSEngine : IDisposable2 {

		private readonly int digestLength;
		private readonly int height;

		private readonly object locker = new object();

		private readonly int logWinternitz;

		private readonly ThreadContext[] threadContexts;
		private readonly int threadCount;
		private readonly Enums.ThreadMode threadMode;

		private readonly int winternitz;
		private readonly WotsPlusEngine wotsPlusProvider;
		private readonly ConcurrentDictionary<XMSSMTLeafId, MemoryBlockDoubleArray> wotsPublicKeysCache = new ConcurrentDictionary<XMSSMTLeafId, MemoryBlockDoubleArray>();

		private readonly ConcurrentDictionary<XMSSMTLeafId, IByteArray> wotsSecretSeedsCache = new ConcurrentDictionary<XMSSMTLeafId, IByteArray>();

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		public XMSSEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, WotsPlusEngine wotsProvider, XMSSExecutionContext xmssExecutionContext, int height, WinternitzParameter wParam = WinternitzParameter.Param16) {

			this.height = height;
			this.xmssExecutionContext = xmssExecutionContext;

			this.LeafCount = 1 << this.height;

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.winternitz = (int) wParam;
			this.logWinternitz = CommonUtils.Log2(this.winternitz);

			this.threadMode = threadMode;
			this.wotsPlusProvider = wotsProvider ?? new WotsPlusEngine(mode, threadMode, this.xmssExecutionContext, wParam);

			this.threadCount = CommonUtils.GetThreadCount(this.threadMode);

			if(mode.HasFlag(XMSSOperationModes.Signature)) {
				this.threadContexts = new ThreadContext[this.threadCount];

				for(int i = 0; i < this.threadCount; i++) {
					this.threadContexts[i] = new ThreadContext(this.xmssExecutionContext);
				}
			}
		}

		public int LeafCount { get; }
		public int MaximumIndex => 1 << this.height;

		public IByteArray GenerateWotsDeterministicPrivateSeed(IByteArray secretSeed, int nonce1, OtsHashAddress otsHashAddress) {

			XMSSMTLeafId addressId = otsHashAddress;

			if(this.wotsSecretSeedsCache.TryGetValue(addressId, out IByteArray cached)) {

				return cached;
			}

			int previousValue = otsHashAddress.KeyAndMask;
			otsHashAddress.KeyAndMask = nonce1;

			IByteArray result = CommonUtils.PRF(secretSeed, otsHashAddress, this.xmssExecutionContext);

			// restore the previous
			otsHashAddress.KeyAndMask = previousValue;

			this.wotsSecretSeedsCache.AddSafe(addressId, result);

			return result;
		}

		private MemoryBlockDoubleArray GenerateWotsPublicKeyParallel(XMSSPrivateKey privateKey, ThreadContext threadContext) {

			XMSSMTLeafId addressId = threadContext.OtsHashAddress;

			if(this.wotsPublicKeysCache.TryGetValue(addressId, out MemoryBlockDoubleArray cached)) {

				return cached;
			}

			IByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(privateKey.SecretSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce1, threadContext.OtsHashAddress);

			MemoryBlockDoubleArray wotsPublicKey = this.wotsPlusProvider.GeneratePublicKey(wotsPrivateSeed, privateKey.PublicSeed, privateKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce2, threadContext);

			this.wotsPublicKeysCache.AddSafe(addressId, wotsPublicKey);

			return wotsPublicKey;
		}

		public (XMSSPrivateKey privateKey, XMSSPublicKey publicKey) GenerateKeys() {
			(IByteArray publicSeed, IByteArray secretSeed, IByteArray secretSeedPrf) = CommonUtils.GenerateSeeds(this.xmssExecutionContext);

			// cache all our entries for immadiate reuse
			this.wotsPublicKeysCache.Clear();

			var nonces = new List<(int nonce1, int nonce2)>();

			for(int i = 0; i < this.LeafCount; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
				nonces.Add((this.xmssExecutionContext.Random.NextInt(), this.xmssExecutionContext.Random.NextInt()));

				nonces.Shuffle();
#endif
			}

			// build our secret key
			XMSSPrivateKey secretKey = new XMSSPrivateKey(this.height, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces), this.xmssExecutionContext);

			// now lets generate our public xmss key
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			//secretKey.Root = this.TreeHashOld(secretKey, 0, this.height, publicSeed, adrs);

			secretKey.Root = this.TreeHash(secretKey, 0, this.height, publicSeed, adrs);

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
		public IByteArray Hash(IByteArray key, IByteArray buffer) {
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
				return CommonUtils.HashEntry(CommonUtils.HashCodes.H, key, buffer, this.xmssExecutionContext);
			} catch(Exception ex) {
				throw new Exception("Exception raised while hashing buffer and key", ex);
			}
		}

		// H_msg function
		public IByteArray HashMessage(IByteArray key, IByteArray buffer) {

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
				return CommonUtils.HashEntry(CommonUtils.HashCodes.HMsg, key, buffer, this.xmssExecutionContext);
			} catch(Exception ex) {
				throw new Exception("Exception raised while hashing buffer and key", ex);
			}
		}

		public IByteArray RandHashParallel(IByteArray left, IByteArray right, IByteArray publicSeed, ThreadContext threadContext) {

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
			IByteArray key = CommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			threadContext.randHashAddress.KeyAndMask = 1;
			IByteArray bm = CommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			CommonUtils.Xor(bm, bm, left);
			bm.CopyTo(threadContext.RandHashFinalBuffer);
			bm.Return();

			threadContext.randHashAddress.KeyAndMask = 2;
			bm = CommonUtils.PRF(publicSeed, threadContext.randHashAddress, this.xmssExecutionContext);
			CommonUtils.Xor(bm, bm, right);
			bm.CopyTo(threadContext.RandHashFinalBuffer, bm.Length);
			bm.Return();

			// restore the value
			threadContext.randHashAddress.KeyAndMask = previousKeyMask;

			IByteArray tmp = this.Hash(key, threadContext.RandHashFinalBuffer);

			key.Return();

			return tmp;
		}

		// Algorithm 7: RAND_HASH 
		public IByteArray RandHash(IByteArray left, IByteArray right, IByteArray publicSeed, CommonAddress adrs) {

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

			IByteArray randHashFinalBuffer = MemoryAllocators.Instance.cryptoAllocator.Take(this.digestLength << 1);
			tmpAdrs.KeyAndMask = 0;
			IByteArray key = CommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
			tmpAdrs.KeyAndMask = 1;
			IByteArray bm = CommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
			CommonUtils.Xor(bm, bm, left);
			bm.CopyTo(randHashFinalBuffer);
			bm.Return();

			tmpAdrs.KeyAndMask = 2;
			bm = CommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext);
			CommonUtils.Xor(bm, bm, right);
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

			IByteArray tmp = this.Hash(key, randHashFinalBuffer);

			key.Return();
			randHashFinalBuffer.Return();

			return tmp;
		}

		public IByteArray LTree(MemoryBlockDoubleArray publicKey, IByteArray publicSeed, LTreeAddress adrs, ThreadContext threadContext = null) {
			adrs.TreeHeight = 0;

			//NOTE: here we do a shallow copy, so we MUST NOT return any memory here. Public key still owns the buffers
			MemoryBlockDoubleArray publicKeyClone = publicKey.CloneShallow();

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

			IByteArray result = publicKeyClone[0].Clone();

			publicKeyClone.ReturnShallow();

			return result;
		}

		public IByteArray TreeHash(XMSSPrivateKey privateKey, int startIndex, int targetNodeHeigth, IByteArray publicSeed, OtsHashAddress adrs) {
			if((startIndex % (1 << targetNodeHeigth)) != 0) {
				return null;
			}

			// clean up
			this.incompleteNodes.Clear();

			for(int i = 0; i < this.readyNodes.Count; i++) {
				this.readyNodes.TryDequeue(out NodeInfo entry);
			}

			// first, parse the tree, find nodes that need computing

			IByteArray root = this.PrepareHashTreeWorkNodes((startIndex >> targetNodeHeigth, targetNodeHeigth), default, NodeInfo.Directions.None, privateKey.NodeCache);

			if(root != null) {
				// we have our root, it was most probably cached
				return root;
			}

			// ok, now lets compute our tree
			bool completed = false;

			int Callback(int index) {

				ThreadContext threadContext = this.threadContexts[index];
				NodeInfo workingNode = null;

				while((!completed && (workingNode != null)) || this.readyNodes.TryDequeue(out workingNode) || !this.incompleteNodes.IsEmpty) {
					// ok, lets process the node
					if(workingNode != null) {
						if(workingNode.IsCompleted) {
							// it was already done, lets keep going
							workingNode = null;

							continue;
						}

						if(workingNode is MergedNodeInfo merged) {

							// hash left and right together
							threadContext.HashTreeAddress.Initialize(adrs);
							threadContext.HashTreeAddress.TreeHeight = workingNode.Id.Height - 1; // the height of the children
							threadContext.HashTreeAddress.TreeIndex = workingNode.Id.Index;

							threadContext.randHashAddress = threadContext.HashTreeAddress;
							workingNode.Hash = this.RandHashParallel(merged.Left, merged.Right, publicSeed, threadContext);
							threadContext.randHashAddress = null;
						} else {
							// hash single node
							threadContext.OtsHashAddress.Initialize(adrs);
							threadContext.OtsHashAddress.OtsAddress = workingNode.Id.Index;

							MemoryBlockDoubleArray wotsPublicKey = this.GenerateWotsPublicKeyParallel(privateKey, threadContext);

							threadContext.LTreeAddress.Initialize(adrs);
							threadContext.LTreeAddress.LtreeAddress = workingNode.Id.Index;

							threadContext.randHashAddress = threadContext.LTreeAddress;
							workingNode.Hash = this.LTree(wotsPublicKey, publicSeed, threadContext.LTreeAddress);
							threadContext.randHashAddress = null;
						}

						// here we would add the node to cache
						privateKey.NodeCache.Cache(workingNode.Id, workingNode.Hash);

						if(workingNode.Id.Height == targetNodeHeigth) {
							if(!workingNode.IsCompleted) {
								//  this is bad, we are done but we have no result
								throw new ApplicationException("Failed to find the root node.");
							}

							// its the root, we are done!
							root = workingNode.Hash;

							// stop the other threads too, we are done
							completed = true;

							break;
						}

						if(this.incompleteNodes.TryGetValue(workingNode.Parent, out MergedNodeInfo parent)) {
							if(workingNode.Direction == NodeInfo.Directions.Left) {
								parent.Left = workingNode.Hash;
							} else {
								parent.Right = workingNode.Hash;
							}

							workingNode = null;

							if(!parent.AreChildrenReady) {
								continue;
							}

							lock(this.locker) {
								// move it to the ready queue
								if(this.incompleteNodes.TryRemove(parent.Id, out parent)) {
									// its ready, let's process it right away
									workingNode = parent;
								}
							}
						} else {
							workingNode = null;
						}
					} else {
						// ok, we have nothing, let's see who is available
						lock(this.locker) {
							// simply pick the next in line
							var entry = this.incompleteNodes.FirstOrDefault(e => e.Value.AreChildrenReady);

							if(entry.Value != null) {

								this.incompleteNodes.RemoveSafe(entry.Key);
								workingNode = entry.Value;
							}
						}

						if(workingNode == null) {
							// we found nothing, lets sleep a bit untl something becomes available
							Thread.Sleep(10);
						}
					}
				}

				return 1;
			}

			var tasks = new Task[this.threadCount];

			for(int i = 0; i < this.threadCount; i++) {
				this.threadContexts[i].OtsHashAddress.Initialize(adrs);
				this.threadContexts[i].LTreeAddress.Initialize(adrs);
				this.threadContexts[i].HashTreeAddress.Initialize(adrs);

				int index = i;

				var task = new Task<int>(() => Callback(index));
				task.Start();
				tasks[i] = task;
			}

			Task.WaitAll(tasks);

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
		private IByteArray PrepareHashTreeWorkNodes(XMSSNodeId id, XMSSNodeId parentId, NodeInfo.Directions direction, XMSSNodeCache nodeCache) {

			IByteArray cached = nodeCache[id];

			if(cached != null) {
				// we have it, lets stop here and return the cached entry
				return cached;
			}

			if(id.Height == 0) {

				// this one is ready to compute
				NodeInfo leafNode = new NodeInfo();
				leafNode.Id = id;
				leafNode.Direction = direction;
				leafNode.Parent = parentId;
				this.readyNodes.Enqueue(leafNode);

				return null;
			}

			MergedNodeInfo node = new MergedNodeInfo();

			node.Id = id;

			node.Left = this.PrepareHashTreeWorkNodes((2 * id.Index, id.Height - 1), id, NodeInfo.Directions.Left, nodeCache);
			node.Right = this.PrepareHashTreeWorkNodes(((2 * id.Index) + 1, id.Height - 1), id, NodeInfo.Directions.Right, nodeCache);
			node.Direction = direction;
			node.Parent = parentId;

			if(node.AreChildrenReady) {
				// its ready to be processed
				this.readyNodes.Enqueue(node);
			} else {
				// we will come back to it later
				this.incompleteNodes.AddSafe(id, node);
			}

			// always null, the ndoe is not computed yet
			return null;
		}

		internal MemoryBlockDoubleArray BuildAuth(XMSSPrivateKey secretKey, int index, IByteArray publicSeed, OtsHashAddress adrs) {
			MemoryBlockDoubleArray auth = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(this.height);

			//TODO: paralellise
			for(int j = 0; j < this.height; j++) {
				int expo = 1 << j;
				int k = (int) Math.Floor((decimal) index / expo) ^ 1;

				auth[j] = this.TreeHash(secretKey, k * expo, j, publicSeed, adrs);
			}

			return auth;
		}

		public ImmutableList<XMSSNodeId> BuildAuthTreeNodesList(int index) {
			if(index >= this.LeafCount) {
				return null;
			}

			var nodes = new List<XMSSNodeId>();

			//TODO: paralellise?
			for(int j = 0; j <= this.height; j++) {
				int expo = 1 << j;
				int k = (int) Math.Floor((decimal) index / expo) ^ 1;

				if(j == this.height) {
					// this is the root
					nodes.Add((0, this.height));
				} else {
					var node = this.BuildTreePath(k * expo, j);

					if(node.HasValue) {
						nodes.Add(node.Value);
					}
				}
			}

			var result = nodes.ToImmutableList();

			return result;
		}

		public void ShakeAuthTree(XMSSNodeCache xmssNodeCache, int index, ImmutableList<XMSSNodeId> nodes) {
			if(xmssNodeCache == null) {

				return;
			}

			// now remove the nodes that are not shared
			var excludedNodes = xmssNodeCache.NodeIds.Where(n => (n.Height < (this.height - XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY)) && (n != (index + 1, 0)) && !nodes.Contains(n)).ToList();

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
			var allNodes = new List<XMSSNodeId>();

			foreach(int entry in Enumerable.Range(index, 3)) {
				var result = this.BuildAuthTreeNodesList(entry);

				if(result != null) {
					allNodes.AddRange(result);
				}
			}

			// make them unique
			var remainingNodes = allNodes.Distinct().ToImmutableList();

			// and shake!
			this.ShakeAuthTree(xmssNodeCache, index, remainingNodes);

		}

		public XMSSNodeId? BuildTreePath(int startIndex, int targetNodeHeigth) {

			if((startIndex % (1 << targetNodeHeigth)) != 0) {
				return null;
			}

			int limit = 1 << targetNodeHeigth;

			var stack = new Stack<XMSSNodeId>();

			//TODO: make parallel
			for(int i = 0; i < limit; i++) {
				int index = startIndex + i;

				XMSSNodeId node = (index, 0);

				int treeHeight = 0;
				int treeIndex = index;

				var peekXmssNode = stack.Any() ? stack.Peek() : (XMSSNodeId?) null;

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

		internal XMSSSignature.XMSSTreeSignature TreeSig(IByteArray message, XMSSPrivateKey xmssSecretKey, int signatureIndex, IByteArray publicSeed, OtsHashAddress adrs) {
			MemoryBlockDoubleArray auth = this.BuildAuth(xmssSecretKey, signatureIndex, publicSeed, adrs);

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);
			otsHashAddress.OtsAddress = signatureIndex;

			IByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(xmssSecretKey.SecretSeed, xmssSecretKey.Nonce1, otsHashAddress);

			MemoryBlockDoubleArray otsSignature = this.wotsPlusProvider.GenerateSignature(message, wotsPrivateSeed, publicSeed, xmssSecretKey.Nonce2, otsHashAddress);

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			XMSSSignature.XMSSTreeSignature result = new XMSSSignature.XMSSTreeSignature(otsSignature, auth, this.xmssExecutionContext);

			return result;
		}

		private void CheckValidIndex(XMSSPrivateKey xmssSecretKey) {
			if(xmssSecretKey.Index >= this.LeafCount) {
				throw new ArgumentException("The key index is higher than the key size");
			}
		}

		public IByteArray Sign(IByteArray message, XMSSPrivateKey xmssSecretKey) {
			int signatureIndex = xmssSecretKey.Index;

			this.CheckValidIndex(xmssSecretKey);
			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();

			IByteArray temp2 = CommonUtils.ToBytes(signatureIndex, this.digestLength);
			IByteArray random = CommonUtils.PRF(xmssSecretKey.SecretPrf, temp2, this.xmssExecutionContext);
			IByteArray temp = xmssSecretKey.Root;

			IByteArray concatenated = CommonUtils.Concatenate(random, temp, temp2);

			temp2.Return();

			IByteArray hasedMessage = this.HashMessage(concatenated, message);

			concatenated.Return();

			XMSSSignature.XMSSTreeSignature treeSig = this.TreeSig(hasedMessage, xmssSecretKey, signatureIndex, xmssSecretKey.PublicSeed, adrs);

			hasedMessage.Return();
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			XMSSSignature signature = new XMSSSignature(random, signatureIndex, treeSig, this.xmssExecutionContext);

			IByteArray result = signature.Save();

			signature.Dispose();

			return result;
		}

		public IByteArray XmssRootFromSig(int leafIndex, MemoryBlockDoubleArray otsSignature, MemoryBlockDoubleArray auth, IByteArray hasedMessage, IByteArray publicSeed, OtsHashAddress adrs) {

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);

			MemoryBlockDoubleArray publickey = this.wotsPlusProvider.GeneratePublicKeyFromSignature(hasedMessage, otsSignature, publicSeed, otsHashAddress);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			LTreeAddress lTreeAddress = this.xmssExecutionContext.LTreeAddressPool.GetObject();
			lTreeAddress.Reset();
			lTreeAddress.TreeAddress = adrs.TreeAddress;
			lTreeAddress.LayerAddress = adrs.LayerAddress;
			lTreeAddress.LtreeAddress = adrs.OtsAddress;

			IByteArray xmssNode = this.LTree(publickey, publicSeed, lTreeAddress);
			this.xmssExecutionContext.LTreeAddressPool.PutObject(lTreeAddress);

			publickey.Return();

			HashTreeAddress hashTreeAddress = this.xmssExecutionContext.HashTreeAddressPool.GetObject();
			hashTreeAddress.Reset();
			hashTreeAddress.TreeAddress = adrs.TreeAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.TreeIndex = adrs.OtsAddress;

			for(int k = 0; k < this.height; k++) {
				hashTreeAddress.TreeHeight = k;

				IByteArray randHash = null;

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

		public bool Verify(IByteArray signature, IByteArray message, IByteArray publicKey) {
			XMSSSignature loadedSignature = new XMSSSignature(this.xmssExecutionContext);
			loadedSignature.Load(signature, this.wotsPlusProvider, this.height);

			XMSSPublicKey loadedPublicKey = new XMSSPublicKey(this.xmssExecutionContext);
			loadedPublicKey.LoadKey(publicKey);

			IByteArray temp2 = CommonUtils.ToBytes(loadedSignature.Index, this.digestLength);

			IByteArray concatenated = CommonUtils.Concatenate(loadedSignature.Random, loadedPublicKey.Root, temp2);

			temp2.Return();

			IByteArray hasedMessage = this.HashMessage(concatenated, message);

			concatenated.Return();

			OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			adrs.Reset();
			adrs.OtsAddress = loadedSignature.Index;

			IByteArray node = this.XmssRootFromSig(loadedSignature.Index, loadedSignature.XmssTreeSignature.otsSignature, loadedSignature.XmssTreeSignature.Auth, hasedMessage, loadedPublicKey.PublicSeed, adrs);

			hasedMessage.Return();

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);

			bool result = CommonUtils.EqualsConstantTime(loadedPublicKey.Root, node);

			node.Return();

			loadedSignature.Dispose();
			loadedPublicKey.Dispose();

			return result;
		}

	#region TREE HASH

		private class NodeInfo {
			public enum Directions {
				None,
				Left,
				Right
			}

			public XMSSNodeId Parent { get; set; }
			public IByteArray Hash { get; set; }
			public XMSSNodeId Id { get; set; }
			public Directions Direction { get; set; }

			public bool IsCompleted => (this.Hash != null) && this.Hash.HasData;
		}

		private class MergedNodeInfo : NodeInfo {

			public IByteArray Left { get; set; }
			public IByteArray Right { get; set; }

			public bool AreChildrenReady => (this.Left != null) && (this.Right != null);
		}

		private readonly ConcurrentDictionary<XMSSNodeId, MergedNodeInfo> incompleteNodes = new ConcurrentDictionary<XMSSNodeId, MergedNodeInfo>();
		private readonly ConcurrentQueue<NodeInfo> readyNodes = new ConcurrentQueue<NodeInfo>();

		public class ThreadContext : IDisposable2 {

			public readonly HashTreeAddress HashTreeAddress;
			public readonly LTreeAddress LTreeAddress;
			public readonly OtsHashAddress OtsHashAddress;

			public readonly IByteArray RandHashFinalBuffer;
			private readonly XMSSExecutionContext xmssExecutionContext;

			public CommonAddress randHashAddress;

			public ThreadContext(XMSSExecutionContext xmssExecutionContext) {
				this.xmssExecutionContext = xmssExecutionContext;
				this.HashTreeAddress = xmssExecutionContext.HashTreeAddressPool.GetObject();
				this.OtsHashAddress = xmssExecutionContext.OtsHashAddressPool.GetObject();
				this.LTreeAddress = xmssExecutionContext.LTreeAddressPool.GetObject();

				this.RandHashFinalBuffer = MemoryAllocators.Instance.cryptoAllocator.Take(xmssExecutionContext.DigestSize << 1);
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
				foreach(MemoryBlockDoubleArray entry in this.wotsPublicKeysCache.Values) {
					entry?.Dispose();
				}

				foreach(IByteArray entry in this.wotsSecretSeedsCache.Values) {
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