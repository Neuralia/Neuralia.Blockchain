using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {
	public static class DigestChannelSetFactory {


		public static BlockchainDigestSimpleChannelSetDescriptor ConvertToDigestSimpleChannelSetDescriptor(BlockchainDigestDescriptor blockchainDigestDescriptor) {
			BlockchainDigestSimpleChannelSetDescriptor descriptor = new BlockchainDigestSimpleChannelSetDescriptor();

			foreach(var channel in blockchainDigestDescriptor.Channels) {
				BlockchainDigestSimpleChannelDescriptor channelDescriptor = new BlockchainDigestSimpleChannelDescriptor();

				channelDescriptor.SetVersion(channel.Value.Version);

				channelDescriptor.ChannelType = channel.Key;
				channelDescriptor.TotalEntries = channel.Value.TotalEntries;
				channelDescriptor.LastEntryId = channel.Value.LastEntryId;
				channelDescriptor.GroupSize = channel.Value.GroupSize;

				descriptor.Channels.Add(channel.Key, channelDescriptor);
			}

			return descriptor;
		}
		
		public static DigestChannelSet CreateDigestChannelSet(string folder, Dictionary<DigestChannelType, BlockchainDigestSimpleChannelDescriptor> channels, IBlockchainDigestChannelFactory blockchainDigestChannelFactory) {
			DigestChannelSet digestChannelSet = new DigestChannelSet();

			foreach(var channelDescriptor in channels) {

				IDigestChannel channel = blockchainDigestChannelFactory.CreateCreateDigestChannels(channelDescriptor.Value, folder);
				channel.Initialize();
				digestChannelSet.Channels.Add(channelDescriptor.Key, channel);
			}

			return digestChannelSet;
		}
		
		public static DigestChannelSet CreateDigestChannelSet(string folder, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor, IBlockchainDigestChannelFactory blockchainDigestChannelFactory) {
			DigestChannelSet digestChannelSet = new DigestChannelSet();

			foreach(var channelDescriptor in blockchainDigestDescriptor.Channels) {

				IDigestChannel channel = blockchainDigestChannelFactory.CreateCreateDigestChannels(channelDescriptor.Value, folder);
				channel.Initialize();
				digestChannelSet.Channels.Add(channelDescriptor.Key, channel);
			}

			return digestChannelSet;
		}

		public static ValidatingDigestChannelSet CreateValidatingDigestChannelSet(string folder, BlockchainDigestDescriptor blockchainDigestDescriptor, IBlockchainDigestChannelFactory blockchainDigestChannelFactory) {

			ValidatingDigestChannelSet validatingDigestChannelSet = new ValidatingDigestChannelSet();

			foreach(KeyValuePair<ushort, BlockchainDigestChannelDescriptor> channelDescriptor in blockchainDigestDescriptor.Channels) {

				IDigestChannel channel = blockchainDigestChannelFactory.CreateCreateDigestChannels(channelDescriptor.Value, folder);
				channel.Initialize();
				validatingDigestChannelSet.Channels.Add(channelDescriptor.Key, channel);
			}

			return validatingDigestChannelSet;
		}
	}
}