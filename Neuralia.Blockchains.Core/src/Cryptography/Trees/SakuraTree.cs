using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     A rough implementation of the Sakura tree
	/// </summary>
	public abstract class SakuraTree : TreeHasher {
		private const int HOP_COUNT = 2; // we include 2 regular nodes as chaining hops and add + 1 for the kangourou hop (so 3 each group)

		/// <summary>
		/// cache for disposing
		/// </summary>
		private readonly List<Hop> hopCache = new List<Hop>();

		/// <summary>
		///  we use this for cleaning up buffers that we created
		/// </summary>
		protected SafeArrayHandle HashBytes(IHashNodeList nodeList) {
			SafeArrayHandle result = null;

			try {
				if((nodeList == null) || (nodeList.Count == 0)) {
					throw new ApplicationException("Nodes are required for sakura hashing. Entries can not be null or empty");
				}

				//TODO: convert this to a streaming method, instead of creating all nodes from the start.
				// convert the byte arrays into our own internal hop structure. they are all lead hops, to start.
				IHopSet leafHops = new HopSet(nodeList, this.hopCache);

				const int level = 1; // 0 is used by the leaves. here we operate at the next step, so 1.

				result = this.ConcatenateHops(leafHops, level);

			} finally {
				foreach(var entry in this.hopCache) {
					entry.Dispose();
				}
			}

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
				// now we preppare the next level group
				int totalHopJump = HOP_COUNT + 1;
				SubHopSet results = new SubHopSet();
				int totalHops = hops.Count;

				if(totalHops < totalHopJump) {
					totalHopJump = totalHops;
				}

				int i = 0;

				for(; (i + totalHopJump) <= totalHops; i += totalHopJump) {
					ChainingHop chainHop = new ChainingHop();
					this.hopCache.Add(chainHop);
					
					// the number if regular hops we include as chaining ones
					int totalRoundChainingHops = totalHopJump - 1;

					for(int j = i; j < (i + totalRoundChainingHops); j++) {
						Hop hop = hops[j];

						// should be hashed at this point, lets do it if it was not done yet
						if(!hop.IsHashed) {
							this.HashHop(hop, level);
						}

						// these are the regular nodes
						chainHop.AddHop(hop);
					}

					// now we add the kangourou node (this one should NOT be hashed yet)
					chainHop.SetKangourouHop(hops[i + totalRoundChainingHops]);

					// ok, thats our new hop group
					results.Add(chainHop);
				}

				// now we add the remainders for later use, they will be combined in further levels
				for(; i < totalHops; i++) {
					results.Add(hops[i]);
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

				return theOne.data.Branch(); // this is the final hash
			}
		}

		/// <summary>
		///     Perform the actual hashing of the hop
		/// </summary>
		/// <param name="hop"></param>
		/// <param name="level"></param>
		private void HashHop(Hop hop, int level) {
			SafeArrayHandle hopBytes = hop.GetHopBytes(level);
			var hash = this.GenerateHash(hopBytes);
			hop.data.Entry = hash.Entry;
			hop.IsHashed = true;

			hash.Dispose();
			hopBytes.Dispose();
		}

		protected abstract SafeArrayHandle GenerateHash(SafeArrayHandle entry);

	#region internal classes

		public abstract class Hop : IDisposable2 {
			public readonly SafeArrayHandle data;
			public bool IsHashed { get; set; }
			public abstract SafeArrayHandle GetHopBytes(int level);


			protected Hop(SafeArrayHandle entry) {
				this.data = entry??new SafeArrayHandle();
				this.IsHashed = false;

			}

			protected Hop() : this(null) {

			}

		#region Disposable

			public void Dispose() {
				this.Dispose(true);
			}

			private void Dispose(bool disposing) {

				if(this.IsDisposed) {
					return;
				}

				this.DisposeAll(disposing);
				this.IsDisposed = true;
			}

			protected virtual void DisposeAll(bool disposing) {
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
			private static readonly byte[] LEAF_HOP_LEVEL = TypeSerializer.Serialize(0);

			public LeafHop(SafeArrayHandle entry) : base(entry) {

			}

			public override SafeArrayHandle GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}

				SafeArrayHandle result = ByteArray.Create(this.data.Length + sizeof(int) + sizeof(int) + sizeof(byte));

				Span<byte> intBytes = stackalloc byte[sizeof(int)];
				TypeSerializer.Serialize(this.data.Length, intBytes);

				//first we copy the data itself
				if(this.data.HasData) {
					result.Entry.CopyFrom(this.data.Entry);
				}

				// now we add the size of the array
				result.Entry.CopyFrom(intBytes, 0, this.data.Length, sizeof(int));

				// now we add a constant 0 level
				result.Entry.CopyFrom(LEAF_HOP_LEVEL.AsSpan(), 0, this.data.Length + sizeof(int), sizeof(int));

				// and since this is a leaf hop, we always have a flag of 0
				result.Entry.CopyFrom(LEAF_HOP_FLAG.AsSpan(), 0, this.data.Length + (sizeof(int) * 2), sizeof(byte));

				return result;
			}
		}

		protected class ChainingHop : Hop {
			private static readonly byte[] CHAIN_HOP_FLAG = {1};
			private readonly List<Hop> ChainingHops = new List<Hop>();

			private Hop KangourouHop;
			private int totalChainingHopsSize;

			public override SafeArrayHandle GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}

				if(this.KangourouHop is LeafHop && this.KangourouHop.IsHashed) {
					throw new ApplicationException("A kangourou hop should not be hashed at this point");
				}

				using(SafeArrayHandle kangourouBytes = this.KangourouHop.GetHopBytes(level)) {
					// should not be hashed yet

					// out final mega array
					SafeArrayHandle result = ByteArray.Create(kangourouBytes.Length + this.totalChainingHopsSize + sizeof(int) + sizeof(int) + sizeof(byte));
					int offset = 0;

					//first we copy the kangourou hop itself
					result.Entry.CopyFrom(kangourouBytes.Entry, 0, offset, kangourouBytes.Length);
					offset += kangourouBytes.Length;

					// now the chaining hops are concatenated
					foreach(var hop in this.ChainingHops) {
						result.Entry.CopyFrom(hop.data.Entry, 0, offset, hop.data.Length);
						offset += hop.data.Length;
					}

					// now we add the size of the array
					Span<byte> intBytes = stackalloc byte[sizeof(int)];
					TypeSerializer.Serialize(this.ChainingHops.Count, intBytes);
					result.Entry.CopyFrom(intBytes, 0, offset, sizeof(int));
					offset += sizeof(int);

					// the amount of chaining hops
					TypeSerializer.Serialize(level, intBytes);
					result.Entry.CopyFrom(intBytes, 0, offset, sizeof(int));
					offset += sizeof(int);

					// and since this is a chaining hop, we always have a flag of 1
					result.Entry.CopyFrom(CHAIN_HOP_FLAG.AsSpan(), 0, offset, sizeof(byte));

					return result;
				}
			}

			protected override void DisposeAll(bool disposing) {
				base.DisposeAll(disposing);

				foreach(var hop in this.ChainingHops) {
					hop.Dispose();
				}

				this.KangourouHop?.Dispose();
			}

			public void AddHop(Hop hop) {
				if(!hop.IsHashed) {
					throw new ApplicationException("A hop should be already hashed");
				}

				this.totalChainingHopsSize += hop.data.Length;
				this.ChainingHops.Add(hop);
			}

			public void SetKangourouHop(Hop hop) {
				if(hop.IsHashed) {
					throw new ApplicationException("A kangourou hop must not be already hashed");
				}

				this.KangourouHop = hop;
			}

			public ChainingHop() : base() {
			}
		}

		private interface IHopSet {
			Hop this[int i] { get; }
			int Count { get; }
		}

		private sealed class HopSet : IHopSet {
			private readonly Dictionary<int, Hop> createdHops = new Dictionary<int, Hop>();

			private readonly IHashNodeList hashNodeList;
			private readonly List<Hop> hopCache;

			public HopSet(IHashNodeList hashNodeList, List<Hop> hopCache) {
				this.hashNodeList = hashNodeList;
				this.hopCache = hopCache;
			}

			public Hop this[int i] {
				get {
					if(!this.createdHops.ContainsKey(i)) {

						var hop = new LeafHop(this.hashNodeList[i]);
						this.hopCache.Add(hop);
						this.createdHops.Add(i,hop);
					}

					return this.createdHops[i];
				}
			}

			public int Count => this.hashNodeList.Count;
		}

		private sealed class SubHopSet : IHopSet {

			private readonly List<Hop> createdHops = new List<Hop>();

			public Hop this[int i] => this.createdHops[i];

			public int Count => this.createdHops.Count;

			public void Add(Hop hop) {
				this.createdHops.Add(hop);
			}

			public Hop Single() {
				return this.createdHops.Single();

			}
		}
		#endregion
	}
}