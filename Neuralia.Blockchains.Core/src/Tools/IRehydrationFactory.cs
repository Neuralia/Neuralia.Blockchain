using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Tools {
	public interface IRehydrationFactory {
		IGossipMessageMetadataDetails RehydrateGossipMessageMetadataDetails(byte type, IDataRehydrator dr);
	}
}