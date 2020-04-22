
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Tools;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex {
	public class ErasableFileChannelIndex : IndividualFileChannelIndex {

		public ErasableFileChannelIndex(string folderPath, BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels, FileSystemWrapper fileSystem, BlockChannelUtils.BlockChannelTypes channelType, IndependentFileChannelProvider provider) : base(folderPath, blockchainEnabledChannels, fileSystem, channelType, provider) {
		}
	}
}