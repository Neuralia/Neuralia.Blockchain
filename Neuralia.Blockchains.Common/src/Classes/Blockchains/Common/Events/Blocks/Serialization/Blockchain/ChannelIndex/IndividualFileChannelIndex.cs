using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex {
	public class IndividualFileChannelIndex : ChannelIndex<IndependentFileChannelProvider> {

		public IndividualFileChannelIndex(string folderPath, BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels, FileSystemWrapper fileSystem, BlockChannelUtils.BlockChannelTypes channelType, IndependentFileChannelProvider provider) : base(folderPath, blockchainEnabledChannels, fileSystem) {
			this.ChannelType = channelType;
			this.Provider = provider;
		}

		public BlockChannelUtils.BlockChannelTypes ChannelType { get; }

		public IndependentFileChannelProvider Provider {
			get => this.Providers[this.ChannelType];
			set => this.Providers[this.ChannelType] = value;
		}

		public override void WriteEntry(ChannelsEntries<SafeArrayHandle> blockData) {

			uint length = this.Provider.DataFile.FileSize;

			try {
				this.Provider.DataFile.Write(blockData[this.ChannelType]);
			} catch {
				// try to restore the file
				try {
					this.Provider.DataFile.Truncate(length);
				} catch {
					// we tried, do nothing
				}

				throw;
			}
		}

		public override ChannelsEntries<(long start, int end)> QueryIndex(uint adjustedBlockId) {
			ChannelsEntries<(long start, int end)> result = new ChannelsEntries<(long start, int end)>();

			result[this.ChannelType] = (0, (int) this.Provider.DataFile.FileSize);

			return result;
		}

		public override ChannelsEntries<SafeArrayHandle> QueryBytes(uint adjustedBlockId) {
			ChannelsEntries<SafeArrayHandle> result = new ChannelsEntries<SafeArrayHandle>();

			result[this.ChannelType] = this.Provider.DataFile.ReadAllBytes();

			return result;
		}

		public override ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(uint adjustedBlockId, ChannelsEntries<(int offset, int length)> offsets) {
			ChannelsEntries<SafeArrayHandle> result = new ChannelsEntries<SafeArrayHandle>();

			(int offset, int length) index = offsets[this.ChannelType];
			result[this.ChannelType] = this.Provider.DataFile.ReadBytes(index.offset, index.length);

			return result;
		}

		public override SafeArrayHandle QueryIndexedTransactionOffsets(uint adjustedBlockId, int indexedTransactionIndex) {
			throw new NotImplementedException();
		}

		public override ChannelsEntries<long> QueryProviderFileSizes() {
			ChannelsEntries<long> result = new ChannelsEntries<long>();

			result[this.ChannelType] = this.Provider.DataFile.FileSize;

			return result;
		}
	}
}