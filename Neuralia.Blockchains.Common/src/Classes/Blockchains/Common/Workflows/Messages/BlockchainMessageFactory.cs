using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {

	public interface IBlockchainMessageFactory : IMessageFactory<IBlockchainEventsRehydrationFactory> {
	}

	public abstract class BlockchainMessageFactory : MessageFactory<IBlockchainEventsRehydrationFactory>, IBlockchainMessageFactory {

		protected BlockchainMessageFactory(BlockchainServiceSet serviceSet) : base(serviceSet) {
		}
	}
}