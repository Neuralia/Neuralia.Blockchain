using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {
	/// <summary>
	///     the base class for all chain based message factories
	/// </summary>
	public abstract class ChainMessageFactory : MessageFactory<IBlockchainEventsRehydrationFactory> {

		protected readonly IMainChainMessageFactory mainChainMessageFactory;

		public ChainMessageFactory(IMainChainMessageFactory mainChainMessageFactory, BlockchainServiceSet serviceSet) : base(serviceSet) {
			this.mainChainMessageFactory = mainChainMessageFactory;
		}
	}
}