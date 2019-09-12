using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     A rough implementation of the Sakura tree
	/// </summary>
	public abstract class SakuraTree : TreeHasher {
		private const int hopCount = 2; // we include 2 regular nodes as chaining hops and add + 1 for the kangourou hop (so 3 each group)

		/// <summary>
		///  we use this for cleaning up buffers that we created
		/// </summary>
		private readonly List<IByteArray> buffersSet = new List<IByteArray>();
		
		public IByteArray HashBytes(IHashNodeList nodeList) {
			IByteArray result = null;
			
			try {
				if((nodeList == null) || (nodeList.Count == 0)) {
					throw new ApplicationException("Nodes are required for sakura hashing. Entries can not be null or empty");
				}

				//TODO: convert this to a streaming method, instead of creating all nodes from the start.
				// convert the byte arrays into our own internal hop structure. they are all lead hops, to start.
				IHopSet leafHops = new HopSet(nodeList, this.buffersSet);

				const int level = 1; // 0 is used by the leaves. here we operate at the next step, so 1.

				result = this.ConcatenateHops(leafHops, level).Clone();
				
			} finally {
				foreach(var entry in this.buffersSet) {
					entry.Return();
				}
				this.buffersSet.Clear();
				nodeList?.Dispose();
			}
				
			return result;
		}

		public override IByteArray Hash(IHashNodeList nodeList) {
			return this.HashBytes(nodeList);
		}

		/// <summary>
		///     Here we loop the hops list and group them together into chain hops and kangourou hops. we recurse until there is
		///     only one left
		/// </summary>
		/// <param name="hops"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		private IByteArray ConcatenateHops(IHopSet hops, int level) {
			// now we preppare the next level group
			int totalHopJump = hopCount + 1;
			SubHopSet results = new SubHopSet();
			int totalHops = hops.Count;

			if(totalHops < totalHopJump) {
				totalHopJump = totalHops;
			}

			int i = 0;

			for(; (i + totalHopJump) <= totalHops; i += totalHopJump) {
				ChainingHop chainHop = new ChainingHop(this.buffersSet);

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
				return this.ConcatenateHops(results, level + 1);
			}

			// its the end of the line, the top of the tree, the ONE. hash and return this
			Hop theOne = results.Single();
			this.HashHop(theOne, level);

			IByteArray resultHash = theOne.data; // this is the final hash
			theOne.data = null; // so it doesnt get disposed, we loaned it out


			return resultHash;
		}

		/// <summary>
		///     Perform the actual hashing of the hop
		/// </summary>
		/// <param name="hop"></param>
		/// <param name="level"></param>
		private void HashHop(Hop hop, int level) {
			IByteArray hopBytes = hop.GetHopBytes(level);
			hop.data = this.GenerateHash(hopBytes);
			this.buffersSet.Add(hop.data);
			hop.IsHashed = true;
		}

		protected abstract IByteArray GenerateHash(IByteArray entry);

	#region internal classes

		protected abstract class Hop  {
			public IByteArray data;
			public bool IsHashed { get; set; }
			public abstract IByteArray GetHopBytes(int level);

			protected readonly List<IByteArray> buffersSet;
			public Hop(IByteArray entry, List<IByteArray> buffersSet) {
				this.data = entry;
				this.IsHashed = false;
				this.buffersSet = buffersSet;
			}
			public Hop(List<IByteArray> buffersSet) {
				this.data = null;
				this.IsHashed = false;
				this.buffersSet = buffersSet;
			}
		}

		protected class LeafHop : Hop {
			private static readonly byte[] LEAF_HOP_FLAG = {0};
			private static readonly byte[] LEAF_HOP_LEVEL = BitConverter.GetBytes(0);

			public LeafHop(IByteArray entry, List<IByteArray> buffersSet) : base(entry, buffersSet) {

			}

			public override IByteArray GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}
				
				IByteArray result = MemoryAllocators.Instance.cryptoAllocator.Take(this.data.Length + sizeof(int) + sizeof(int) + sizeof(byte));

				Span<byte> intBytes = stackalloc byte[sizeof(int)];
				TypeSerializer.Serialize(this.data.Length, intBytes);

				//first we copy the data itself
				result.CopyFrom(this.data);

				// now we add the size of the array
				result.CopyFrom(intBytes, 0, this.data.Length, sizeof(int));

				// now we add a constant 0 level
				result.CopyFrom(LEAF_HOP_LEVEL, 0, this.data.Length + sizeof(int), sizeof(int));

				// and since this is a leaf hop, we always have a flag of 0
				result.CopyFrom(LEAF_HOP_FLAG, 0, this.data.Length + (sizeof(int) * 2), sizeof(byte));
				
				this.buffersSet.Add(result);
				
				return result;
			}
		}

		protected class ChainingHop : Hop {
			private static readonly byte[] CHAIN_HOP_FLAG = {1};
			private readonly List<Hop> ChainingHops = new List<Hop>();

			private Hop KangourouHop;
			private int totalChainingHopsSize;

			public override IByteArray GetHopBytes(int level) {
				if(this.IsHashed) {
					throw new ApplicationException("Hope has already been hashed");
				}

				if(this.KangourouHop is LeafHop && this.KangourouHop.IsHashed) {
					throw new ApplicationException("A kangourou hop should not be hashed at this point");
				}

				IByteArray kangourouBytes = this.KangourouHop.GetHopBytes(level); // should not be hashed yet

				// out final mega array
				IByteArray result = MemoryAllocators.Instance.cryptoAllocator.Take(kangourouBytes.Length + this.totalChainingHopsSize + sizeof(int) + sizeof(int) + sizeof(byte));
				int offset = 0;

				//first we copy the kangourou hop itself
				result.CopyFrom(kangourouBytes, 0, offset, kangourouBytes.Length);
				offset += kangourouBytes.Length;

				// now theWalletProv chaining hops are concatenated
				foreach(Hop hop in this.ChainingHops) {
					result.CopyFrom(hop.data, 0, offset, hop.data.Length);
					offset += hop.data.Length;
				}

				// now we add the size of the array
				Span<byte> intBytes = stackalloc byte[sizeof(int)];
				TypeSerializer.Serialize(this.ChainingHops.Count, intBytes);
				result.CopyFrom(intBytes, 0, offset, sizeof(int));
				offset += sizeof(int);

				// the amount of chaining hops
				TypeSerializer.Serialize(level, intBytes);
				result.CopyFrom(intBytes, 0, offset, sizeof(int));
				offset += sizeof(int);

				// and since this is a chaining hop, we always have a flag of 1
				result.CopyFrom(CHAIN_HOP_FLAG, 0, offset, sizeof(byte));
				
				this.buffersSet.Add(result);
				return result;
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

			public ChainingHop(List<IByteArray> buffersSet) : base(buffersSet) {
			}
		}

		private interface IHopSet {
			Hop this[int i] { get; }
			int Count { get; }
		}

		private class HopSet : IHopSet {
			private readonly Dictionary<int, Hop> createdHops = new Dictionary<int, Hop>();

			private readonly IHashNodeList hashNodeList;
			private readonly List<IByteArray> buffersSet;
				
			public HopSet(IHashNodeList hashNodeList, List<IByteArray> buffersSet) {
				this.hashNodeList = hashNodeList;
				this.buffersSet = buffersSet;
			}

			public Hop this[int i] {
				get {
					if(!this.createdHops.ContainsKey(i)) {
						this.createdHops.Add(i, new LeafHop(this.hashNodeList[i], this.buffersSet));
					}

					return this.createdHops[i];
				}
			}

			public int Count => this.hashNodeList.Count;
		}

		private class SubHopSet : IHopSet {

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