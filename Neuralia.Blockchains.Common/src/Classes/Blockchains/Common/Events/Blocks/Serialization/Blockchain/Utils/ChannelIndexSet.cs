using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public class ChannelIndexSet {

		private uint? adjustedBlockId;
		private (long index, long startingBlockId, long endingBlockId) blockIndex;
		public SharedChannelIndex MainChannelIndex { get; private set; }

		public List<IChannelIndex> ChannelIndices { get; } = new List<IChannelIndex>();

		public List<BlockChannelUtils.BlockChannelTypes> ChannelTypes {
			get {
				var items = new List<BlockChannelUtils.BlockChannelTypes>();

				this.ChannelIndices.ForEach(entries => {
					items.AddRange(entries.ChannelTypes);
				});

				return items.Where(e => !e.HasFlag(BlockChannelUtils.BlockChannelTypes.Keys)).Distinct().ToList();
			}
		}

		public void SetMainChannelIndex(SharedChannelIndex mainChannelIndex) {
			this.MainChannelIndex = mainChannelIndex;

			this.AddIndex(mainChannelIndex);
		}

		public void AddIndex(IChannelIndex index) {
			this.ChannelIndices.Add(index);
		}

		public void Reset(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			if((this.adjustedBlockId == adjustedBlockId) && (this.blockIndex == blockIndex)) {
				return;
			}

			this.adjustedBlockId = adjustedBlockId;
			this.blockIndex = blockIndex;

			foreach(IChannelIndex index in this.ChannelIndices) {
				index.ResetFileSpecs(adjustedBlockId, blockIndex);
			}
		}

		public void WriteEntry(ChannelsEntries<SafeArrayHandle> blockData) {

			foreach(IChannelIndex index in this.ChannelIndices) {
				index.WriteEntry(blockData);
			}
		}

		public virtual ChannelsEntries<(long start, int end)> QueryIndex(uint adjustedBlockId) {

			var result = new ChannelsEntries<(long start, int end)>();

			foreach(IChannelIndex index in this.ChannelIndices) {
				var subResults = index.QueryIndex(adjustedBlockId);

				if(subResults != null) {
					subResults.Entries.ForEach(entry => result[entry.Key] = entry.Value);
				}
			}

			return result;
		}

		public ChannelsEntries<SafeArrayHandle> QueryBytes(uint adjustedBlockId) {
			var result = new ChannelsEntries<SafeArrayHandle>(this.ChannelTypes);

			try {
				foreach(IChannelIndex index in this.ChannelIndices) {
					var subResults = index.QueryBytes(adjustedBlockId);

					subResults.Entries.ForEach(entry => result[entry.Key] = entry.Value);
				}
			} catch(BlockLoadException blex) {
				//TODO: do anything or just retunr null?
			}
			
			return result;
		}

		public ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(uint adjustedBlockId, ChannelsEntries<(int offset, int length)> offsets) {
			var result = new ChannelsEntries<SafeArrayHandle>(this.ChannelTypes);

			try {
				foreach(IChannelIndex index in this.ChannelIndices) {
					var subResults = index.QueryPartialBlockBytes(adjustedBlockId, offsets.GetSubset(index.ChannelTypes));

					subResults.Entries.ForEach(entry => result[entry.Key] = entry.Value);
				}
			} catch(BlockLoadException blex) {
				//TODO: do anything or just retunr null?
			}

			return result;
		}

		public SafeArrayHandle QueryMasterTransactionOffsets(uint adjustedBlockId, int masterTransactionIndex) {
			
			return this.MainChannelIndex.QueryMasterTransactionOffsets(adjustedBlockId, masterTransactionIndex);
			
		}

		public ChannelsEntries<long> QueryProviderFileSizes() {
			var result = new ChannelsEntries<long>(this.ChannelTypes);

			foreach(IChannelIndex index in this.ChannelIndices) {
				try {
					var subResults = index.QueryProviderFileSizes();

					subResults.Entries.ForEach(entry => result[entry.Key] = entry.Value);
				} catch(BlockLoadException blex) {
					//TODO: do anything or just retunr null?
				}
			}

			return result;
		}
	}
}