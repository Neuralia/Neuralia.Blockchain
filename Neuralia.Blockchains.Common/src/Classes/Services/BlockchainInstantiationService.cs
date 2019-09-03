using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Common.Classes.Services {
	public interface IBlockchainInstantiationService : IInstantiationService<IBlockchainEventsRehydrationFactory> {
	}

	public class BlockchainInstantiationService : InstantiationService<IBlockchainEventsRehydrationFactory>, IBlockchainInstantiationService {
	}
}