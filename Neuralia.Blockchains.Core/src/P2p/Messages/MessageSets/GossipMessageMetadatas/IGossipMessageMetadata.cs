using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas {
	public interface IGossipMessageMetadata : IBinarySerializable, ITreeHashable {
		byte Type { get; }
		BlockchainType BlockchainType { get; }
		IGossipMessageMetadataDetails GossipMessageMetadataDetails { get; }
	}
}