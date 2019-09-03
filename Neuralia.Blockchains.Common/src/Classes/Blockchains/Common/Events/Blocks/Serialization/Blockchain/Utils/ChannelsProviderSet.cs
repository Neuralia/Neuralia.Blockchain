using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public class ChannelsProviderSet : ChannelsEntries<ChannelProvider> {

		public ChannelsProviderSet() {
		}

		public ChannelsProviderSet(BlockChannelUtils.BlockChannelTypes channels) : base(channels) {
		}

		public ChannelsProviderSet(BlockChannelUtils.BlockChannelTypes channels, Func<BlockChannelUtils.BlockChannelTypes, ChannelProvider> action) : base(channels, action) {
		}

		public IEnumerable<(BlockChannelUtils.BlockChannelTypes channelType, MainIndexedConcatenatedChannelProvider provider)> SelectAllMainIndexedConcatenatedChannelProviders => this.SelectAllByType<MainIndexedConcatenatedChannelProvider>();
		public IEnumerable<(BlockChannelUtils.BlockChannelTypes channelType, IndependentFileChannelProvider provider)> SelectAllIndependentFileChannelProviders => this.SelectAllByType<IndependentFileChannelProvider>();

		public Dictionary<BlockChannelUtils.BlockChannelTypes, (int index, MainIndexedConcatenatedChannelProvider provider)> SelectAllDictMainIndexedConcatenatedChannelProviders => this.SelectAllDictByType<MainIndexedConcatenatedChannelProvider>();

		public IEnumerable<(BlockChannelUtils.BlockChannelTypes channelType, K provider)> SelectAllByType<K>()
			where K : ChannelProvider {
			return this.Entries.Where(v => v.Value is K).OrderBy(v => (int) v.Key).Select(v => (v.Key, (K) v.Value));
		}

		public Dictionary<BlockChannelUtils.BlockChannelTypes, (int index, K provider)> SelectAllDictByType<K>()
			where K : ChannelProvider {
			return this.SelectAllByType<K>().Select((e, i) => new {e, i}).ToDictionary(v => v.e.channelType, v => (v.i, v.e.provider));
		}
	}
}