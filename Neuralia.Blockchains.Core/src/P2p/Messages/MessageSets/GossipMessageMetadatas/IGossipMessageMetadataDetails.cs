using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas {
	public interface IGossipMessageMetadataDetails : IBinarySerializable, ITreeHashable {
		byte Type { get; }
	}
}