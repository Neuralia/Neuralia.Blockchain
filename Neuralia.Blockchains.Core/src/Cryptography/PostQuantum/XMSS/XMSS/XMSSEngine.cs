using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Data.Pools;
using Neuralia.Blockchains.Tools.General;
using Newtonsoft.Json;
using Nito.AsyncEx.Synchronous;
using Zio.FileSystems;

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
		private readonly byte height;

		private readonly ThreadContext[] threadContexts;
		private readonly int threadCount;

		private readonly WotsPlus wotsPlusProvider;
		
		//TODO: caching everything is inneficient. we should use another type of cache with a maximum amount of nodes and better heuristics. i.e. we dont need to cache the level 0.
		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray[]> wotsPublicKeysCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray[]>();

		private readonly ConcurrentDictionary<XMSSMTLeafId, ByteArray> wotsSecretSeedsCache = new ConcurrentDictionary<XMSSMTLeafId, ByteArray>();

		private readonly XMSSExecutionContext xmssExecutionContext;

		public const int DEFAULT_NONCES_EXPONENT = 4; // 1 nonce per 4 entries
		
		/// <summary>
		/// this exponent determines the number of nodes (2^groupingExponent) that will be processed in a single turn. the bigger the number, the more ram we use
		/// </summary>
		public const int DEFAULT_GROUPING_EXPONENT = 10; // 2^10 = 1024 nodes
		
		/// <summary>
		/// the nonce exponent tells us the size of the grouping (number of indices) that will use the same nonce index.
		/// </summary>
		private readonly byte noncesExponent = DEFAULT_NONCES_EXPONENT;
		
		private XMSSOperationModes mode;
		
		/// <summary>
		/// XMSS can take up A LOT of ram. if true, the node cache will be saved to disk while building the whole key. False will use ram
		/// </summary>
		public bool SaveToDisk { get; set; } = false;
		public bool ClearWorkingFolder { get; set; } = true;
		public string WorkingFolderPath { get; set; }
		
		/// <summary>
		/// </summary>
		/// <param name="levels">Number of levels of the tree</param>
		/// <param name="length">Length in bytes of the message digest as well as of each node</param>
		/// <param name="wParam">Winternitz parameter {4,16}</param>
		/// <remarks>Can sign 2^height messages</remarks>
		public XMSSEngine(XMSSOperationModes mode, Enums.ThreadMode threadMode, WotsPlus wotsProvider, XMSSExecutionContext xmssExecutionContext, byte height) {

			this.mode = mode;
			this.height = height;
			this.xmssExecutionContext = xmssExecutionContext;

			this.LeafCount = 1L << this.height;

			this.wotsPlusProvider = wotsProvider ?? new WotsPlus(threadMode, this.xmssExecutionContext);

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.backupDigestLength = this.xmssExecutionContext.BackupDigestSize;

			this.noncesExponent = this.xmssExecutionContext.NoncesExponent;
			this.threadCount = XMSSCommonUtils.GetThreadCount(threadMode);

			if(mode.HasFlag(XMSSOperationModes.Signature)) {
				this.threadContexts = new ThreadContext[this.threadCount];
			}
		}

		private bool EnableCaches => this.xmssExecutionContext.EnableCaches;
		public long LeafCount { get; }
		public long MaximumIndex => 1L << this.height;

		public ByteArray GenerateWotsDeterministicPrivateSeed(ByteArray secretSeed, short nonce1, OtsHashAddress otsHashAddress) {

			XMSSMTLeafId addressId = otsHashAddress;

			bool enabledCache = this.EnableCaches && addressId.Layer != 0;
			if(enabledCache && this.wotsSecretSeedsCache.TryGetValue(addressId, out ByteArray cached)) {

				return cached.Clone();
			}

			int previousValue = otsHashAddress.KeyAndMask;
			otsHashAddress.KeyAndMask = nonce1;

			ByteArray result = XMSSCommonUtils.PRF(secretSeed, otsHashAddress, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

			// restore the previous
			otsHashAddress.KeyAndMask = previousValue;

			if(enabledCache) {
				this.wotsSecretSeedsCache.AddSafe(addressId, result.Clone());
			}

			return result;
		}

		private (ByteArray[] key, bool cached) GenerateWotsPublicKeyParallel(XMSSPrivateKey xmssSecretKey, ThreadContext threadContext) {

			XMSSMTLeafId addressId = threadContext.OtsHashAddress;

			bool enabledCache = this.EnableCaches && addressId.Layer != 0;
			if(enabledCache && this.wotsPublicKeysCache.TryGetValue(addressId, out ByteArray[] cached)) {

				return (cached, true);
			}

			bool isCached = false;
			using ByteArray wotsPrivateSeed = this.GenerateWotsDeterministicPrivateSeed(xmssSecretKey.SecretSeed, xmssSecretKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce1, threadContext.OtsHashAddress);

			ByteArray[] wotsPublicKey = this.wotsPlusProvider.GeneratePublicKey(wotsPrivateSeed, xmssSecretKey.PublicSeed, xmssSecretKey.Nonces[threadContext.OtsHashAddress.OtsAddress].nonce2, threadContext);

			if(enabledCache) {
				this.wotsPublicKeysCache.AddSafe(addressId, wotsPublicKey);
				isCached = true;
			}

			return (wotsPublicKey, isCached);
		}

		public class SaveContext {
			public String ContextFile { get; set; }
			public String IncompleteNodesKeysFile { get; set; }
			public String IncompleteNodesValuesFile { get; set; }
			public FileSystemWrapper FileSystem { get; set; }
		}
		public async Task<(XMSSPrivateKey xmssSecretKey, XMSSPublicKey publicKey)> GenerateKeys(int? seedSize =  null, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = DEFAULT_NONCES_EXPONENT) {
			
			ByteArray publicSeed = null;
			ByteArray secretSeed = null;
			ByteArray secretSeedPrf = null;

			string folderPath = this.WorkingFolderPath;

			if(string.IsNullOrWhiteSpace(folderPath)) {
				folderPath = Path.Combine(Path.GetTempPath(), "XMSS-Cache");
			}
			SaveContext saveContext = new SaveContext();
			
			try {
				saveContext.ContextFile = Path.Combine(folderPath, $"context");
				saveContext.IncompleteNodesKeysFile = Path.Combine(folderPath, $"incomplete-nodes-keys");
				saveContext.IncompleteNodesValuesFile = Path.Combine(folderPath, $"incomplete-nodes-values");
				
				string privateKeyFile = Path.Combine(folderPath, $"private-key");
				
				XMSSPrivateKey secretKey = null;
				if(this.SaveToDisk) {
					saveContext.FileSystem = new FileSystemWrapper(new PhysicalFileSystem());
					FileExtensions.EnsureDirectoryStructure(folderPath);

					if(saveContext.FileSystem.FileExists(privateKeyFile)) {
						using var bytes = FileExtensions.ReadAllBytes(privateKeyFile, saveContext.FileSystem);
						secretKey = new XMSSPrivateKey(this.xmssExecutionContext);
						secretKey.LoadKey(bytes);

						publicSeed = secretKey.PublicSeed.Clone();
						secretSeed = secretKey.SecretSeed.Clone();
						secretSeedPrf = secretKey.SecretPrf.Clone();
					}
				}

				// cache all our entries for immediate reuse
				this.ClearCaches();

				if(secretKey == null) {
					(publicSeed, secretSeed, secretSeedPrf) = XMSSCommonUtils.GenerateSeeds(seedSize, this.xmssExecutionContext);
					
					List<(short nonce1, short nonce2)> nonces = new List<(short nonce1, short nonce2)>();

					for(int i = 0; i < (this.LeafCount >> this.noncesExponent) + 1; i++) {

#if DETERMINISTIC_DEBUG
				nonces.Add((0, 0));
#else
						nonces.Add((GlobalRandom.GetNextShort(), GlobalRandom.GetNextShort()));
#endif
					}

					nonces.Shuffle();

					// build our secret key
					secretKey = new XMSSPrivateKey(this.height, publicSeed, secretSeed, secretSeedPrf, new XMSSNonceSet(nonces, this.noncesExponent), this.xmssExecutionContext, cacheMode: cacheMode, cacheLevels: cacheLevels);
					
					// dummy defaults
					secretKey.Root = ByteArray.Create(this.xmssExecutionContext.DigestSize);
					secretKey.BackupRoot = ByteArray.Create(this.xmssExecutionContext.DigestSize);
					
					if(this.SaveToDisk) {
						using var bytes = secretKey.SaveKey();
						FileExtensions.WriteAllBytes(privateKeyFile, bytes, saveContext.FileSystem);
					}
				}

				// now lets generate our public xmss key
				OtsHashAddress adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
				adrs.Reset();
				
				(secretKey.Root, secretKey.BackupRoot) = await this.TreeHash(secretKey, 0, this.height, publicSeed, adrs,secretKey.NodeCache , progressCallback, saveContext: saveContext).ConfigureAwait(false);
				
				this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);
				
				if(this.SaveToDisk && this.ClearWorkingFolder) {

					if(saveContext.FileSystem.DirectoryExists(folderPath)) {
						foreach(var file in saveContext.FileSystem.EnumerateFiles(folderPath)) {
							SecureWipe.WipeFile(folderPath, saveContext.FileSystem, 20).WaitAndUnwrapException();
						}
						Directory.Delete(folderPath, true);
					}
				}
				
				XMSSPublicKey publicKey = new XMSSPublicKey(publicSeed, secretKey.Root, secretKey.BackupRoot, this.xmssExecutionContext);
				
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
				throw new Exception("Right byte array should not be null");
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

			using(bm) {
				XMSSCommonUtils.Xor(bm, bm, left);
				bm.CopyTo(randHashFinalBuffer);
			}

			tmpAdrs.KeyAndMask = 2;
			bm = XMSSCommonUtils.PRF(publicSeed, tmpAdrs, this.xmssExecutionContext, hashType);

			using(bm) {
				XMSSCommonUtils.Xor(bm, bm, right);
				bm.CopyTo(randHashFinalBuffer, bm.Length);
			}

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

		public ByteArray LTree(ByteArray[] publicKey, ByteArray publicSeed, LTreeAddress adrs, XMSSCommonUtils.HashTypes hashType, ThreadContext threadContext = null) {
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
						publicKeyClone[i] = this.RandHashParallel(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, threadContext, hashType);
					} else {
						publicKeyClone[i] = this.RandHash(publicKeyClone[2 * i], publicKeyClone[(2 * i) + 1], publicSeed, adrs, hashType, this.digestLength);
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private XMSSNodeId GetParentId(XMSSNodeId id) {
			return (id.Index >> 1, (byte)(id.Height+1));
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private XmssNodeInfo.Directions GetNodeDirection(XMSSNodeId id) {
			return (id.Index & 1) == 0?XmssNodeInfo.Directions.Left:XmssNodeInfo.Directions.Right;
		}

		private JsonSerializerSettings GetJsonSettings() {
			JsonSerializerSettings settings = JsonUtilsOld.CreateSerializerSettings();
			settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.NullValueHandling = NullValueHandling.Ignore;

			settings.Converters.Add(new XMSSNodeId.XmssNodeIdConverterOld());
			
			return settings;
		}
		/// <summary>
		/// prepare a list of leaf nodes required to go up the tree
		/// </summary>
		/// <param name="leafGroupIndex"></param>
		/// <param name="leafGroupSize"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void PrepareNextLeafGroup(long startingLeafIndex, ClosureWrapper<int> leafGroupIndex, int leafGroupSize, ObjectPool<XmssNodeInfo> xmssNodeInfoPool, SaveContext saveContext, Context context) {
			
			long startIndex = startingLeafIndex+(leafGroupIndex * leafGroupSize);
			long endIndex = startIndex + leafGroupSize;
			
			if(this.SaveToDisk && context != null && startIndex != 0 && context.LeafIndex != startIndex) {
				// serialize the state
				context.LeafIndex = startIndex;
				context.LeafGroupIndex = leafGroupIndex.Value;
				
				FileExtensions.WriteAllText(saveContext.ContextFile, Newtonsoft.Json.JsonConvert.SerializeObject(context), saveContext.FileSystem);

				var nodes = this.incompleteNodes.ToArray().OrderBy(e => e.Key.Height).ThenBy(e => e.Key.Index).Select(e => (e.Key, e.Value)).ToArray();

				var keys = nodes.Select(e => e.Key).ToArray();
				var values = nodes.Select(e => e.Value).ToArray();
				
				FileExtensions.WriteAllText(saveContext.IncompleteNodesKeysFile, Newtonsoft.Json.JsonConvert.SerializeObject(keys, this.GetJsonSettings()), saveContext.FileSystem);
				FileExtensions.WriteAllText(saveContext.IncompleteNodesValuesFile, Newtonsoft.Json.JsonConvert.SerializeObject(values, this.GetJsonSettings()), saveContext.FileSystem);

			}
			
			for(long index = startIndex; index < endIndex; index++) {

				this.PrepareLeafEntry((index, 0), xmssNodeInfoPool);
			}

			leafGroupIndex.Value++;
		}

		private class Context {
			
			public long StartingLeafIndex { get; set; }
			public long LeafIndex { get; set; }
			public long CompletedNodes { get; set; }
			public int LeafGroupIndex  { get; set; }
		}
		
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void PrepareLeafEntry(XMSSNodeId id, ObjectPool<XmssNodeInfo> xmssNodeInfoPool) {

			XmssNodeInfo leafXmssNode = xmssNodeInfoPool.GetObject();
			leafXmssNode.Id = id;
			leafXmssNode.Hash = null;
			leafXmssNode.Hash2 = null;
			leafXmssNode.Direction = this.GetNodeDirection(leafXmssNode.Id);
			leafXmssNode.Parent = this.GetParentId(leafXmssNode.Id);
			this.readyLeafNodes.Enqueue(leafXmssNode);
		}
		
		public async Task<(ByteArray root, ByteArray backupRoot)> TreeHash(XMSSPrivateKey xmssSecretKey, long rootIndex, byte targetNodeHeight, ByteArray publicSeed, OtsHashAddress adrs, XMSSNodeCache cache, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, SaveContext saveContext = null) {
			if(xmssSecretKey == null) {
				throw new NullReferenceException(nameof(xmssSecretKey));
			}
			
			XMSSNodeId rootId = (rootIndex, targetNodeHeight);
			
			long leafCount = 1L << rootId.Height;
			// depending on which index of the root we have, we determine which leaf index we will be starting at.
			long startingLeafIndex = leafCount * rootId.Index;
			
			if((startingLeafIndex % (1L << targetNodeHeight)) != 0) {
				if(progressCallback != null) {
					await progressCallback(0).ConfigureAwait(false);
				}
				return (null, null);
			}

			// clean up
			this.ClearNodeBuffers();

			// first let's see if the key root is already cached, we can just use this
			var cachedRoot = cache[rootId];

			if(cachedRoot.root != null) {
				if(progressCallback != null) {
					await progressCallback(100).ConfigureAwait(false);
				}
				// we have our root, it was most probably cached
				return (cachedRoot.root, cachedRoot.backupRoot);
			}

			// determine how many leaf groups we have to process
			int leafGroupCount = (int)Math.Max(leafCount >> DEFAULT_GROUPING_EXPONENT, 1);
			int leafGroupSize = (int)(leafCount / leafGroupCount);
			// now the total amount of nodes we will have to compute in the entire set
			long totalNodes = (leafCount * 2) - 1;
			long completedNodes = 0;
			ClosureWrapper<int> leafGroupIndex = 0;

			bool LeafProcessingRemaining() {
				return leafGroupIndex < leafGroupCount;
			}
			
			// since we create a TON of these objects, a pool is the right thing to do
			using ObjectPool<XmssNodeInfo> xmssNodeInfoPool = new ObjectPool<XmssNodeInfo>(() => new XmssNodeInfo(), (int)(leafGroupSize*1.1), Math.Max(leafGroupSize/10,1));
			using ObjectPool<MergedXmssNodeInfo> mergedXmssNodeInfoPool = new ObjectPool<MergedXmssNodeInfo>(() => new MergedXmssNodeInfo(), (int)(leafGroupSize*1.1), Math.Max(leafGroupSize/10,1));

			Context context = null;

			if(this.SaveToDisk && saveContext != null) {
				// restore state

				if(saveContext.FileSystem.FileExists(saveContext.ContextFile)) {
					context = Newtonsoft.Json.JsonConvert.DeserializeObject<Context>(FileExtensions.ReadAllText(saveContext.ContextFile, saveContext.FileSystem));
					startingLeafIndex = context.StartingLeafIndex;
					completedNodes = context.CompletedNodes;
					leafGroupIndex.Value = context.LeafGroupIndex;

				} else {
					context = new Context();
					context.StartingLeafIndex = startingLeafIndex;
					context.CompletedNodes = completedNodes;
					context.LeafGroupIndex = leafGroupIndex.Value;
				}
				
				if(saveContext.FileSystem.FileExists(saveContext.IncompleteNodesKeysFile) && saveContext.FileSystem.FileExists(saveContext.IncompleteNodesValuesFile)) {
					var keys = Newtonsoft.Json.JsonConvert.DeserializeObject<XMSSNodeId[]>(FileExtensions.ReadAllText(saveContext.IncompleteNodesKeysFile, saveContext.FileSystem), this.GetJsonSettings());
					var values = Newtonsoft.Json.JsonConvert.DeserializeObject<MergedXmssNodeInfo[]>(FileExtensions.ReadAllText(saveContext.IncompleteNodesValuesFile, saveContext.FileSystem), this.GetJsonSettings());

					if(keys.Length != values.Length) {
						throw new ApplicationException("Different key and value lengths");
					}

					for(int i = 0; i < keys.Length; i++) {
						this.incompleteNodes.TryAdd(keys[i], values[i]);
					}
				}
			}
			this.PrepareNextLeafGroup(startingLeafIndex, leafGroupIndex, leafGroupSize, xmssNodeInfoPool, saveContext, context);

			// ok, now lets compute our sub tree
			bool completed = false;

			ByteArray root = null;
			ByteArray backupRoot = null;

			object incompleteNodesLocker = new object();
			object nextReadyLeafNodesLocker = new object();
			
			this.ResetThreadContexts();

			ClosureWrapper<Task> createNextLeafNodesTask = new ClosureWrapper<Task>();
			
			void DisposeNodeSimple(ref XmssNodeInfo workingXmssNode, ref long localWorkingThreads) {

				if(workingXmssNode != null) {
					workingXmssNode.ClearNode();

					if(workingXmssNode is MergedXmssNodeInfo mergedXmssNodeInfo) {
						mergedXmssNodeInfoPool.PutObject(mergedXmssNodeInfo);
					} else {
						xmssNodeInfoPool.PutObject(workingXmssNode);
					}
				}

				workingXmssNode = null;
			}
			
			void DisposeNode(ref XmssNodeInfo workingXmssNode, ref long localWorkingThreads, ref bool currentWorking, bool resetWorking) {

				DisposeNodeSimple(ref workingXmssNode, ref localWorkingThreads);

				if(resetWorking && currentWorking) {
					Interlocked.Decrement(ref localWorkingThreads);
					currentWorking = false;
				}
			}

			// the number of threads actively working on nodes (not idle)
			long workingThreads = 0;

			using CancellationTokenSource tokenSource = new CancellationTokenSource();
			
			int Callback(int index) {
				bool working = false;
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				ThreadContext threadContext = this.threadContexts[index];
				XmssNodeInfo workingXmssNode = null;
				int counter = 0;

				while((!completed && (workingXmssNode != null)) || this.readyLeafNodes.TryDequeue(out workingXmssNode) || !this.incompleteNodes.IsEmpty || LeafProcessingRemaining()) {

					tokenSource.Token.ThrowIfCancellationRequested();
					
					if(workingXmssNode == null && !working) {
						lock(nextReadyLeafNodesLocker) {
							if(createNextLeafNodesTask.IsDefault && LeafProcessingRemaining() && !this.readyLeafNodes.Any()) {
								// ok, we have to launch the creation of the next leaves entries in a parallel thread while the main workers continue with what is left
								createNextLeafNodesTask.Value = Task.Run(() => {

									if(this.SaveToDisk && context != null) {
										context.CompletedNodes = completedNodes;

										// if we save to disk, we have to wait until the entire remaining tree of work is completed
										while(Interlocked.Read(ref workingThreads) != 0) {
											Thread.Sleep(10);
										}
									}

									this.PrepareNextLeafGroup(startingLeafIndex, leafGroupIndex, leafGroupSize, xmssNodeInfoPool, saveContext, context);
								}, tokenSource.Token).ContinueWith(t => {
									lock(nextReadyLeafNodesLocker) {
										// done, reset it
										createNextLeafNodesTask.Value = null;
									}
								}, tokenSource.Token);
							}
						}
					}

					// ok, lets process the node
					if(workingXmssNode != null) {

						if(!working) {
							working = true;
							Interlocked.Increment(ref workingThreads);
						}

						if(workingXmssNode.IsCompleted) {
							// it was already done, lets keep going
							DisposeNode(ref workingXmssNode, ref workingThreads, ref working, true);
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
							
							var wots = this.GenerateWotsPublicKeyParallel(xmssSecretKey, threadContext);

							workingXmssNode.Hash = this.LTree(wots.key, publicSeed, threadContext.LTreeAddress, XMSSCommonUtils.HashTypes.Regular);
							workingXmssNode.Hash2 = this.LTree(wots.key, publicSeed, threadContext.LTreeAddress, XMSSCommonUtils.HashTypes.Backup);

							if(!wots.cached) {
								DoubleArrayHelper.Return(wots.key);
							}

							threadContext.randHashAddress = null;
						}

						// here we would add the node to cache
						Interlocked.Increment(ref completedNodes);

						if(this.EnableCaches) {
							cache.Cache(workingXmssNode.Id, (workingXmssNode.Hash, workingXmssNode.Hash2));
						}

						if(workingXmssNode.Id.Height == targetNodeHeight) {
							if(!workingXmssNode.IsCompleted) {
								//  this is bad, we are done but we have no result
								throw new ApplicationException("Failed to find the root node.");
							}

							// its the root, we are done!

							(root, backupRoot) = workingXmssNode.ReleaseHashes();
							
							DisposeNode(ref workingXmssNode, ref workingThreads, ref working, true);

							// stop the other threads too, we are done
							completed = true;

							break;
						}

						bool fullDispose = true;
						bool shouldContinue = false;
						MergedXmssNodeInfo parent = null;
						lock(incompleteNodesLocker) {
							// check if the parent node exists, and if not create it
							if(!this.incompleteNodes.ContainsKey(workingXmssNode.Parent)) {
								parent = mergedXmssNodeInfoPool.GetObject();
								parent.Left = default;
								parent.Right = default;
								parent.Id = workingXmssNode.Parent;
								parent.Direction = this.GetNodeDirection(parent.Id);
								parent.Parent = this.GetParentId(parent.Id);
								this.incompleteNodes.AddSafe(parent.Id, parent);
							}

							if(parent != null || this.incompleteNodes.TryGetValue(workingXmssNode.Parent, out parent)) {
								if(workingXmssNode.Direction == XmssNodeInfo.Directions.Left) {
									parent.Left = workingXmssNode.ReleaseHashes();
								} else {
									parent.Right = workingXmssNode.ReleaseHashes();
								}

								XMSSNodeId parentId = default;
								
								if(parent.AreChildrenReady) {
									parentId = parent.Id;

									// move it to the ready queue
									this.incompleteNodes.TryRemove(parentId, out parent);

									if(parent != null) {
										fullDispose = false;
									}
								} else {
									shouldContinue = true;
								}
							}
						}
						
						if(fullDispose || parent == null) {
							DisposeNode(ref workingXmssNode, ref workingThreads, ref working, true);

							if(shouldContinue) {
								continue;
							}
						} else {
							// here we keep working with the parent so we maintain working mode
							DisposeNodeSimple(ref workingXmssNode, ref workingThreads);

							// its ready, let's process it right away
							workingXmssNode = parent;
						}

						if(counter >= LOOP_REST_COUNT) {
							counter = 0;

							// ok, we reached our counter limit, let's sleep a bit to be nice with the rest of the system
							Thread.Sleep(10);
						} else {
							counter++;
						}
					} else {
						if(working) {
							DisposeNode(ref workingXmssNode, ref workingThreads, ref working, true);
						}

						// ok, we have nothing, let's see who is available
						lock(incompleteNodesLocker) {
							// simply pick the next in line
							KeyValuePair<XMSSNodeId, MergedXmssNodeInfo> entry = this.incompleteNodes.FirstOrDefault(e => e.Value.AreChildrenReady);

							if(entry.Value != null) {

								this.incompleteNodes.RemoveSafe(entry.Key);
								workingXmssNode = entry.Value;

								if(workingXmssNode != null) {
									Interlocked.Increment(ref workingThreads);
									working = true;
								}
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

				tasks[i] = Task.Run(() => Callback(index), tokenSource.Token);
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

				if(tasks.Any(t => t.IsFaulted)) {
					tokenSource.Cancel();
					throw new AggregateException(tasks.Where(t => t.IsFaulted).Select(e => e.Exception));
				}

				long progress = Interlocked.CompareExchange(ref completedNodes, 0, 0);

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
		
		internal async Task<(ByteArray root, ByteArray backupRoot)[]> BuildAuth(XMSSPrivateKey secretKey, long index, ByteArray publicSeed, OtsHashAddress adrs, XMSSNodeCache cache = null, Func<int, int ,int, Task> progressCallback = null) {

			if(index >= this.LeafCount) {
				throw new ApplicationException($"Index {index} can not be equal or higher to leaf count {this.LeafCount}");
			}
			
			(ByteArray root, ByteArray backupRoot)[] auth = new (ByteArray root, ByteArray backupRoot)[this.height];

			bool cacheEnabled = this.EnableCaches;
			
			try {
				// we will need the caches now, since we will repeat a lot of computations
				this.xmssExecutionContext.EnableCaches = true;
				// create a node cache for all nodes, we will have some repeats
				if(cache == null) {
					cache = secretKey.NodeCache.Clone;
					cache.CacheMode = XMSSNodeCache.XMSSCacheModes.All;
				}

				var nodes = this.BuildAuthTreeNodesList(index, false);
				
				for(byte j = 0; j < this.height; j++) {
					var node = nodes[j];
					
					Func<int, Task> treeHashProgressCallback = null;
				
					if(progressCallback != null) {
						treeHashProgressCallback = (pct) => {

							return progressCallback(j+1, this.height, pct);
						};
					}
					
					auth[j] = await this.TreeHash(secretKey, node.Index, node.Height, publicSeed, adrs, cache, treeHashProgressCallback).ConfigureAwait(false);
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
		public XMSSNodeId[] BuildAuthTreeNodesList(long index, bool addRoot = true) {
			if(index >= this.LeafCount) {
				return Array.Empty<XMSSNodeId>();
			}
			
			int loopHeight = this.height+1;
			if(addRoot == false) {
				loopHeight = this.height;
			}
			
			XMSSNodeId[] nodes = new XMSSNodeId[loopHeight];
			
			for(int j = 0; j < loopHeight; j++) {
				long expo = 1L << j;
				long k = (long) Math.Floor((decimal) index / expo) ^ 1L;

				if(j == this.height) {
					// this is the root
					nodes[j] = (0, this.height);
				} else {
					XMSSNodeId? node = this.BuildTreePath(k * expo, j);

					if(node.HasValue) {
						nodes[j] = node.Value;
					}
				}
			}

			return nodes;
		}

		public void ShakeAuthTree(XMSSNodeCache xmssNodeCache, long index, XMSSNodeId[] nodes) {
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
		public void CleanAuthTree(XMSSNodeCache xmssNodeCache, long index) {

			if(xmssNodeCache == null) {
				return;
			}

			index = 5;

			// lets take the auth path for this index and 2 more only
			List<XMSSNodeId> allNodes = new List<XMSSNodeId>();

			foreach(int entry in Enumerable.Range(0, 3)) {
				
				XMSSNodeId[] result = this.BuildAuthTreeNodesList(index+entry);

				if(result != null) {
					allNodes.AddRange(result);
				}
			}

			// make them unique
			XMSSNodeId[] remainingNodes = allNodes.Distinct().ToArray();

			// and shake!
			this.ShakeAuthTree(xmssNodeCache, index, remainingNodes);

		}

		public XMSSNodeId? BuildTreePath(long startIndex, int targetNodeHeight) {

			long limit = 1L << targetNodeHeight;
			
			if((startIndex % limit) != 0) {
				return null;
			}
			
			Stack<XMSSNodeId> stack = new Stack<XMSSNodeId>();

			//TODO: make parallel
			for(long i = 0; i < limit; i++) {
				long index = startIndex + i;

				XMSSNodeId node = (index, 0);

				byte treeHeight = 0;
				long treeIndex = index;

				XMSSNodeId? peekXmssNode = stack.Any() ? stack.Peek() : (XMSSNodeId?) null;

				while(peekXmssNode.HasValue && (peekXmssNode.Value.Height == node.Height)) {
					treeIndex = (treeIndex - 1) / 2;
					treeHeight += 1;

					stack.Pop();

					node = (treeIndex, (byte)(node.Height + 1));

					peekXmssNode = stack.Any() ? stack.Peek() : (XMSSNodeId?) null;
				}

				stack.Push(node);
			}

			return stack.Any() ? stack.Pop() : (XMSSNodeId?) null;
		}

		internal async Task<XMSSSignature.XMSSTreeSignature> TreeSig(ByteArray message, XMSSPrivateKey xmssSecretKey, long signatureIndex, ByteArray publicSeed, OtsHashAddress adrs, XMSSNodeCache extraNodeCache = null, Func<int, int ,int, Task> progressCallback = null) {

			XMSSNodeCache cache = xmssSecretKey.NodeCache;

			if(extraNodeCache != null) {
				cache = cache.Clone;
				cache.CacheMode = XMSSNodeCache.XMSSCacheModes.All;
				cache.Merge(extraNodeCache);
			}
			(ByteArray root, ByteArray backupRoot)[] auth = await this.BuildAuth(xmssSecretKey, signatureIndex, publicSeed, adrs, cache, progressCallback).ConfigureAwait(false);

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
		
		/// <summary>
		/// this method allows us to build a cache of uncached nodes required to perform a signature. This is very useful to preload the nodes required to perform a signature
		/// </summary>
		/// <param name="xmssSecretKey"></param>
		/// <returns></returns>
		public async Task<XMSSNodeCache> GenerateIndexNodeCache(XMSSPrivateKey xmssSecretKey, Func<int, int ,int, Task> progressCallback = null) {

			var cache = xmssSecretKey.NodeCache.Clone;
			cache.CacheMode = XMSSNodeCache.XMSSCacheModes.All;
			OtsHashAddress adrs = null;
			try {
				adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
				adrs.Reset();
				
				// build the auth path, forcing uncached nodes to be computed. we will cache this computation result
				await this.BuildAuth(xmssSecretKey, xmssSecretKey.Index, xmssSecretKey.PublicSeed, adrs, cache, progressCallback).ConfigureAwait(false);

				// we have all the nodes, let's now remove the ones already in the secret key cache
				cache.ClearNodes(xmssSecretKey.NodeCache, false);
				
				// finally, we can remove all nodes on the leaf level except the opposite
				var authNodes = this.BuildAuthTreeNodesList(xmssSecretKey.Index);
				cache.ClearLevel(0, authNodes.Where(e => e.Height == 0).Select(e => e.Index));
			} finally {
				if(adrs != null) {
					this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);
				}
			}

			return cache;
		}
		
		public async Task<ByteArray> Sign(ByteArray message, XMSSPrivateKey xmssSecretKey, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, XMSSNodeCache extraNodeCache = null, Func<int, int ,int, Task> progressCallback = null) {
			
			this.ClearCaches();
			long signatureIndex = xmssSecretKey.Index;

			this.CheckValidIndex(xmssSecretKey);

			OtsHashAddress adrs = null;
			try {
				adrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
				adrs.Reset();

				using ByteArray temp2 = XMSSCommonUtils.ToBytes(signatureIndex, this.digestLength);
				using ByteArray random = XMSSCommonUtils.PRF(xmssSecretKey.SecretPrf, temp2, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

				using ByteArray concatenated = XMSSCommonUtils.Concatenate(random, xmssSecretKey.Root, temp2);

				using ByteArray hashedMessage = this.HashMessage(concatenated, message);

				XMSSSignature.XMSSTreeSignature treeSig = await this.TreeSig(hashedMessage, xmssSecretKey, signatureIndex, xmssSecretKey.PublicSeed, adrs, extraNodeCache, progressCallback:progressCallback).ConfigureAwait(false);
				
				XMSSNodeId[] authNodeList = null;
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
			} finally {

				if(adrs != null) {
					this.xmssExecutionContext.OtsHashAddressPool.PutObject(adrs);
				}
			}
		}

		private void VerifySignaturePathCache(long signatureIndex, XMSSSignaturePathCache xmssSignaturePathCache) {

			if(xmssSignaturePathCache.TreeHeight != this.height) {
				throw new ApplicationException("Invalid tree height.");
			}
			if(signatureIndex != 0 && xmssSignaturePathCache.Index != signatureIndex-1) {
				throw new ApplicationException("Invalid signature index.");
			}
		}

		public (ByteArray node, ByteArray backupNode) XmssRootFromSig(long leafIndex, ByteArray[] otsSignature, ByteArray[] auth, ByteArray[] backupAuth, ByteArray hashedMessage, ByteArray publicSeed, OtsHashAddress adrs) {

			OtsHashAddress otsHashAddress = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			otsHashAddress.Initialize(adrs);

			ByteArray[] publickey = this.wotsPlusProvider.GeneratePublicKeyFromSignature(hashedMessage, otsSignature, publicSeed, otsHashAddress);
			this.xmssExecutionContext.OtsHashAddressPool.PutObject(otsHashAddress);

			LTreeAddress lTreeAddress = this.xmssExecutionContext.LTreeAddressPool.GetObject();
			lTreeAddress.Reset();
			lTreeAddress.TreeAddress = adrs.TreeAddress;
			lTreeAddress.LayerAddress = adrs.LayerAddress;
			lTreeAddress.LtreeAddress = adrs.OtsAddress;

			ByteArray xmssNode = this.LTree(publickey, publicSeed, lTreeAddress, XMSSCommonUtils.HashTypes.Regular);
			ByteArray xmssBackupNode = this.LTree(publickey, publicSeed, lTreeAddress, XMSSCommonUtils.HashTypes.Backup);
			
			this.xmssExecutionContext.LTreeAddressPool.PutObject(lTreeAddress);

			DoubleArrayHelper.Return(publickey);

			HashTreeAddress hashTreeAddress = this.xmssExecutionContext.HashTreeAddressPool.GetObject();
			hashTreeAddress.Reset();
			hashTreeAddress.TreeAddress = adrs.TreeAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.LayerAddress = adrs.LayerAddress;
			hashTreeAddress.TreeIndex = adrs.OtsAddress;

			for(byte k = 0; k < this.height; k++) {
				hashTreeAddress.TreeHeight = k;

				ByteArray randHash = null;
				ByteArray randBackupHash = null;

				if((Math.Floor((decimal) leafIndex / (1L << k)) % 2) == 0) {
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
			public ByteArray Hash2 { get; set; }
			public XMSSNodeId Id { get; set; }
			public Directions Direction { get; set; }

			public bool IsCompleted => (this.Hash != null) && this.Hash.HasData && (this.Hash2 != null) && this.Hash2.HasData;

			public virtual void ClearNode() {
				this.Hash?.Dispose();
				this.Hash = null;
					
				this.Hash2?.Dispose();
				this.Hash2 = null;
					
				this.Direction = Directions.None;
				this.Parent = default;
			}
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
					this.ClearNode();
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
			public override void ClearNode() {
				base.ClearNode();
				
				this.Left.root?.Dispose();
				this.Left.backupRoot?.Dispose();
				
				this.Right.root?.Dispose();
				this.Right.backupRoot?.Dispose();

				this.Left = default;
				this.Right = default;
			}

		}

		private readonly ConcurrentDictionary<XMSSNodeId, MergedXmssNodeInfo> incompleteNodes = new ConcurrentDictionary<XMSSNodeId, MergedXmssNodeInfo>();
		private readonly WrapperConcurrentQueue<XmssNodeInfo> readyLeafNodes = new WrapperConcurrentQueue<XmssNodeInfo>();
		
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
			
			while(this.readyLeafNodes.TryDequeue(out var node)) {
				node?.Dispose();
			}
			
			this.readyLeafNodes.Clear();
			
			foreach(var node in this.incompleteNodes) {
				node.Value?.Dispose();
			}
			this.incompleteNodes.Clear();
		}

		public void ClearCaches() {
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