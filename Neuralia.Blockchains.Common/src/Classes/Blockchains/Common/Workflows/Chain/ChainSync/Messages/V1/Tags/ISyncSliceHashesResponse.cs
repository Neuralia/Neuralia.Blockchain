using System.Collections.Generic;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags {
	public interface ISyncSliceHashesResponse<KEY> : INetworkMessage, ISyncResponse<KEY> {

		List<int> SliceHashes { get; }
	}
}