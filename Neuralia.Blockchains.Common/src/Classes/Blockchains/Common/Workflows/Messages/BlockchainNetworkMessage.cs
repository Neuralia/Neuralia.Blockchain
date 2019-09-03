using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {
	public interface IBlockchainNetworkMessage : INetworkMessage<IBlockchainEventsRehydrationFactory> {
	}

	public abstract class BlockchainNetworkMessage : NetworkMessage<IBlockchainEventsRehydrationFactory>, IBlockchainNetworkMessage {
	}
}