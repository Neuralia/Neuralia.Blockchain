using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest {
	public interface IMessageGroupGossipMetadataAnalyser {
		bool AnalyzeGossipMessageInfo(IGossipGroupMessageInfo gossipGroupMessageInfo);
	}
}