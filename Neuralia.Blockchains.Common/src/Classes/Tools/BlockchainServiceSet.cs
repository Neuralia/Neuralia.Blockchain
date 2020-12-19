using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Tools {

	public class BlockchainServiceSet<R> : ServiceSet<R>
		where R : IRehydrationFactory {

		public BlockchainServiceSet(BlockchainType chainType) : base(chainType) {
		}

		public IBlockchainTimeService BlockchainTimeService => this.TimeService as IBlockchainTimeService;
		public IBlockchainGuidService BlockchainGuidService => this.GuidService as IBlockchainGuidService;
	}

	public class BlockchainServiceSet : BlockchainServiceSet<IBlockchainEventsRehydrationFactory> {

		public BlockchainServiceSet(BlockchainType chainType) : base(chainType) {
		}
	}
}