using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {
	public static class DigestChannelSetFactory {

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

			foreach(var channelDescriptor in blockchainDigestDescriptor.Channels) {

				IDigestChannel channel = blockchainDigestChannelFactory.CreateCreateDigestChannels(channelDescriptor.Value, folder);
				channel.Initialize();
				validatingDigestChannelSet.Channels.Add(channelDescriptor.Key, channel);
			}

			return validatingDigestChannelSet;
		}
	}
}