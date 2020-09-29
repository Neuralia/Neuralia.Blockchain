using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     A rough implementation of the Sakura tree
	/// </summary>

	//TODO: This class desperately needs to be optimized! 
	public abstract class SakuraTree<T> : TreeHasher, IDisposableExtended
		where T : class {

		private const int MAXIMUM_SEQUENTIAL_COUNT = 10_000;

		private const int HOP_COUNT = 2; // we include 2 regular nodes as chaining hops and add + 1 for the kangourou hop (so 3 each group)

		private readonly ObjectPool<T> hasherPool;

		private readonly int threadCounts;

		public SakuraTree(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) {
			this.threadCounts = XMSSCommonUtils.GetThreadCount(threadMode);

			this.hasherPool = new ObjectPool<T>(this.DigestFactory, 0, this.threadCounts);
		}

		protected abstract T DigestFactory();

		/// <summary>
		///     we use this for cleaning up buffers that we created
		/// </summary>
		protected SafeArrayHandle HashBytes(IHashNodeList nodeList) {
			SafeArrayHandle result = null;

			if((nodeList == null) || (nodeList.Count == 0)) {
				throw new ApplicationException("Nodes are required for sakura hashing. Entries can not be null or empty");
			}

			//TODO: convert this to a streaming method, instead of creating all nodes from the start.
			// convert the byte arrays into our own internal hop structure. they are all lead hops, to start.
			IHopSet leafHops = new HopSet(nodeList);

			const int level = 1; // 0 is used by the leaves. here we operate at the next step, so 1.

			result = this.ConcatenateHops(leafHops, level);

			return result;
		}

		public override SafeArrayHandle Hash(IHashNodeList nodeList) {
			return this.HashBytes(nodeList);
		}

		/// <summary>
		///     Here we loop the hops list and group them together into chain hops and kangourou hops. we recurse until there is
		///     only one left
		/// </summary>
		/// <param name="hops"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		private SafeArrayHandle ConcatenateHops(IHopSet hops, int level) {
			while(true) {
				//TODO: this method can be severely optimized. we can also prepare the entire worklist and loop as fast as possible. see XMSSEngine.TreeHash for an example

				// now we preppare the next level group
				int totalHopJump = HOP_COUNT + 1;
				SubHopSet results = new SubHopSet();
				int totalHops = hops.Count;

				if(totalHops < totalHopJump) {
					totalHopJump = totalHops;
				}

				static IEnumerable<int> SteppingList(int start, int end, int step) {
					for(int i = start; (i + step) <= end; i += step) {
						yield return i;
					}
				}

				int level1 = level;

				IHopSet hops1 = hops;

				// now we add the remainders for later use, they will be combined in further levels
				int start = totalHopJump * (totalHops / totalHopJump);
				int end = start + (totalHops % totalHopJump);

				int GetActualIndex(int index) {
					return index / totalHopJump;
				}

				void RunHop(int index) {
					ChainingHop chainHop = new ChainingHop();

					// the number if regular hops we include as chaining ones
					int totalRoundChainingHops = totalHopJump - 1;

					for(int j = index; j < (index + totalRoundChainingHops); j++) {
						Hop hop = hops1[j];

						// should be hashed at this point, lets do it if it was not done yet
						if(!hop.IsHashed) {
							this.HashHop(hop, level1);
						}

						// these are the regular nodes
						chainHop.AddHop(hop);
					}

					// now we add the kangourou node (this one should NOT be hashed yet)
					chainHop.SetKangourouHop(hops1[index + totalRoundChainingHops]);

					// ok, thats our new hop group
					results.Add(GetActualIndex(index), chainHop);
				}

				if((totalHops <= MAXIMUM_SEQUENTIAL_COUNT) || (this.threadCounts == 1)) {
					// for small amounts, its faster to go sequential
					for(int i = 0; (i + totalHopJump) <= totalHops; i += totalHopJump) {
						RunHop(i);
					}
				} else {
					Parallel.ForEach(SteppingList(0, totalHops, totalHopJump), new ParallelOptions {MaxDegreeOfParallelism = this.threadCounts}, RunHop);
				}

				int finalIndex = GetActualIndex(start);

				for(int i = start; i < end; i++) {
					results.Add(finalIndex + (i - start), hops[i]);
				}

				if(results.Count > 1) {
					// now we process the next level if we still have hashes to combine
					hops = results;

					level += 1;

					continue;
				}

				// its the end of the line, the top of the tree, the ONE. hash and return this
				Hop theOne = results.Single();

				this.HashHop(theOne, level);

				var hash = theOne.data.Branch(); // this is the final hash

				theOne.Dispose();

				return hash;
			}
		}

		/// <summary>
		///     Perform the actual hashing of the hop
		/// </summary>
		/// <param name="hop"></param>
		/// <param name="level"></param>
		private void HashHop(Hop hop, int level) {
			if(hop.IsHashed) {
				return;
			}

			using SafeArrayHandle hopBytes = hop.GetHopBytes(level);
			using SafeArrayHandle hash = this.GenerateHash(hopBytes);
			hop.data.Entry = hash.Entry;
			hop.IsHashed = true;
		}

		protected SafeArrayHandle GenerateHash(SafeArrayHandle entry) {

			T hasher = null;

			try {
				hasher = this.hasherPool.GetObject();

				//TODO: create one with a stackalloc hash
				return this.GenerateHash(entry, hasher);
			} finally {
				if(hasher != null) {
					this.hasherPool.PutObject(hasher);
				}
			}
		}

		protected abstract SafeArrayHandle GenerateHash(SafeArrayHandle entry, T hasher);

	#region internal classes

		public abstract class Hop : IDisposableExtended {
			public readonly SafeArrayHandle data = SafeArrayHandle.Create();

			protected Hop(SafeArrayHandle entry) {
				if(entry != null) {
					this.data.Entry = entry.Entry;
				}

				this.IsHashed = false;

			}

			protected Hop() : this(null) {

			}

			public bool IsHashed { get; set; }
			public abstract SafeArrayHandle GetHopBytes(int level);

		#region Disposable

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {

				if(this.IsDisposed) {
					return;
				}

				if(disposing) {
					this.DisposeAll();
				}

				this.IsDisposed = true;
			}

			protected virtual void DisposeAll() {
				this.data?.Dispose();
			}

			~Hop() {
				this.Dispose(false);
			}

			public bool IsDisposed { get; private set; }

		#endregion

		}

		protected class LeafHop : Hop {
			private static readonly byte[] LEAF_HOP_FLAG = {0};
			private static readonly SafeArrayHandle LEAF_HOP_LEVEL = TypeSerializer.Serialize(0);

			public LeafHop(SafeArrayHandle entry) : base(entry) {

			}

			public override SafeArrayHandle GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}

				SafeArrayHandle result = SafeArrayHandle.Create(this.data.Length + sizeof(int) + sizeof(int) + sizeof(byte));

				Span<byte> intBytes = stackalloc byte[sizeof(int)];
				TypeSerializer.Serialize(this.data.Length, intBytes);

				//first we copy the data itself
				if(this.data.HasData) {
					result.Entry.CopyFrom(this.data.Entry);
				}

				// now we add the size of the array
				result.Entry.CopyFrom(intBytes, 0, this.data.Length, sizeof(int));

				// now we add a constant 0 level
				result.Entry.CopyFrom(LEAF_HOP_LEVEL.Span, 0, this.data.Length + sizeof(int), sizeof(int));

				// and since this is a leaf hop, we always have a flag of 0
				result.Entry.CopyFrom(LEAF_HOP_FLAG.AsSpan(), 0, this.data.Length + (sizeof(int) * 2), sizeof(byte));

				return result;
			}
		}

		protected class ChainingHop : Hop {
			private static readonly byte[] ChainHopFlag = {1};
			private readonly List<Hop> chainingHops = new List<Hop>();

			private Hop kangourouHop;
			private int totalChainingHopsSize;

			public override SafeArrayHandle GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}

				if(this.kangourouHop is LeafHop && this.kangourouHop.IsHashed) {
					throw new ApplicationException("A kangourou hop should not be hashed at this point");
				}

				SafeArrayHandle result = null;

				try {
					using(SafeArrayHandle kangourouBytes = this.kangourouHop.GetHopBytes(level)) {
						// should not be hashed yet

						// out final mega array
						result = SafeArrayHandle.Create(kangourouBytes.Length + this.totalChainingHopsSize + sizeof(int) + sizeof(int) + sizeof(byte));
						int offset = 0;

						//first we copy the kangourou hop itself
						result.Entry.CopyFrom(kangourouBytes.Entry, 0, offset, kangourouBytes.Length);
						offset += kangourouBytes.Length;

						// now the chaining hops are concatenated
						foreach(Hop hop in this.chainingHops) {
							result.Entry.CopyFrom(hop.data.Entry, 0, offset, hop.data.Length);
							offset += hop.data.Length;
						}

						// now we add the size of the array
						Span<byte> intBytes = stackalloc byte[sizeof(int)];
						TypeSerializer.Serialize(this.chainingHops.Count, intBytes);
						result.Entry.CopyFrom(intBytes, 0, offset, sizeof(int));
						offset += sizeof(int);

						// the amount of chaining hops
						TypeSerializer.Serialize(level, intBytes);
						result.Entry.CopyFrom(intBytes, 0, offset, sizeof(int));
						offset += sizeof(int);

						// and since this is a chaining hop, we always have a flag of 1
						result.Entry.CopyFrom(ChainHopFlag.AsSpan(), 0, offset, sizeof(byte));
					}
				} finally {
					// these hashing sets can get huge, so lets clear as we go so we dont use up too much RAM.
					this.ClearHops();
				}

				return result;
			}

			protected override void DisposeAll() {
				base.DisposeAll();

				this.ClearHops();
			}

			private void ClearHops() {
				foreach(Hop hop in this.chainingHops) {
					hop.Dispose();
				}

				this.chainingHops.Clear();
				this.kangourouHop?.Dispose();
				this.kangourouHop = null;
			}

			public void AddHop(Hop hop) {
				if(!hop.IsHashed) {
					throw new ApplicationException("A hop should be already hashed");
				}

				this.totalChainingHopsSize += hop.data.Length;
				this.chainingHops.Add(hop);
			}

			public void SetKangourouHop(Hop hop) {
				if(hop.IsHashed) {
					throw new ApplicationException("A kangourou hop must not be already hashed");
				}

				this.kangourouHop = hop;
			}
		}

		private interface IHopSet {
			Hop this[int i] { get; }
			int Count { get; }
		}

		private sealed class HopSet : IHopSet {
			private readonly ConcurrentDictionary<int, Hop> createdHops = new ConcurrentDictionary<int, Hop>();

			private readonly IHashNodeList hashNodeList;

			public HopSet(IHashNodeList hashNodeList) {
				this.hashNodeList = hashNodeList;
			}

			public Hop this[int i] {
				get {

					if(this.createdHops.ContainsKey(i)) {
						return this.createdHops[i];
					}

					this.createdHops.AddSafe(i, new LeafHop(this.hashNodeList[i]));

					return this.createdHops[i];
				}
			}

			public int Count => this.hashNodeList.Count;
		}

		private sealed class SubHopSet : IHopSet {

			private readonly ConcurrentDictionary<int, Hop> createdHops = new ConcurrentDictionary<int, Hop>();

			public Hop this[int i] => this.createdHops[i];

			public int Count => this.createdHops.Count;

			public void Add(int index, Hop hop) {
				this.createdHops.AddSafe(index, hop);
			}

			public Hop Single() {
				return this.createdHops.Values.Single();
			}
		}

	#endregion

	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {
			this.hasherPool.Dispose();
		}

		~SakuraTree() {
			this.Dispose(false);
		}

	#endregion

	}
}