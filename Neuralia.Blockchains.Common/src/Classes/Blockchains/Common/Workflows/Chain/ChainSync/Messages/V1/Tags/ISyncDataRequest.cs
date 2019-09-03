using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags {
	public interface ISyncDataRequest<out CHANNEL_INFO_SET, T, KEY, SLICE_KEY> : INetworkMessage, ISyncRequest<KEY>
		where CHANNEL_INFO_SET : ChannelsInfoSet<SLICE_KEY, T>
		where T : DataSliceInfo, new() {
		CHANNEL_INFO_SET SlicesInfo { get; }
	}
}