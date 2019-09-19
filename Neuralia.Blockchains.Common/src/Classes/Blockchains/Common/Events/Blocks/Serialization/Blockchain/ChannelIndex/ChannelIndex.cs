using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex {

	public interface IChannelIndex {
		List<BlockChannelUtils.BlockChannelTypes> ChannelTypes { get; }
		void WriteEntry(ChannelsEntries<SafeArrayHandle> blockData);
		void ResetFileSpecs(uint adjustedBlockId, (int index, long startingBlockId) blockIndex);
		void EnsureFilesCreated();

		ChannelsEntries<(long start, int end)> QueryIndex(uint adjustedBlockId);
		ChannelsEntries<SafeArrayHandle> QueryBytes(uint adjustedBlockId);

		ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(uint adjustedBlockId, ChannelsEntries<(int offset, int length)> offsets);
		SafeArrayHandle QueryKeyedTransactionOffsets(uint adjustedBlockId, int keyedTransactionIndex);

		ChannelsEntries<long> QueryProviderFileSizes();
	}

	public interface IChannelIndex<T> : IChannelIndex
		where T : ChannelProvider {
		Dictionary<BlockChannelUtils.BlockChannelTypes, T> Providers { get; }
	}

	public abstract class ChannelIndex<T> : ChannelFilesHandler, IChannelIndex<T>
		where T : ChannelProvider {

		protected readonly BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels;

		protected ChannelIndex(string folderPath, BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels, IFileSystem fileSystem) : base(folderPath, fileSystem) {
			this.blockchainEnabledChannels = blockchainEnabledChannels;

			BlockChannelUtils.RunForFlags(blockchainEnabledChannels, flag => {
				this.BlockchainChannelTypes.Add(flag);
			});
		}

		public List<BlockChannelUtils.BlockChannelTypes> BlockchainChannelTypes { get; } = new List<BlockChannelUtils.BlockChannelTypes>();
		public Dictionary<BlockChannelUtils.BlockChannelTypes, T> Providers { get; } = new Dictionary<BlockChannelUtils.BlockChannelTypes, T>();

		public List<BlockChannelUtils.BlockChannelTypes> ChannelTypes => this.Providers.Keys.ToList();

		public virtual void WriteEntry(ChannelsEntries<SafeArrayHandle> blockData) {
			if(!this.adjustedBlockId.HasValue) {
				throw new ApplicationException("block Value has not been set");
			}
		}

		public abstract ChannelsEntries<(long start, int end)> QueryIndex(uint adjustedBlockId);

		public abstract ChannelsEntries<SafeArrayHandle> QueryBytes(uint adjustedBlockId);

		public abstract ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(uint adjustedBlockId, ChannelsEntries<(int offset, int length)> offsets);
		public abstract SafeArrayHandle QueryKeyedTransactionOffsets(uint adjustedBlockId, int keyedTransactionIndex);

		public abstract ChannelsEntries<long> QueryProviderFileSizes();

		protected override void ResetAllFileSpecs(uint adjustedBlockId, (int index, long startingBlockId) blockIndex) {

			foreach(T providerFileSpec in this.Providers.Values) {
				providerFileSpec.ResetFileSpecs(adjustedBlockId, blockIndex);
			}
		}
	}
}