using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {
	public interface IBlockchainTriggerMessageSet : ITriggerMessageSet<IBlockchainEventsRehydrationFactory>, IBlockchainTargettedMessageSet {
	}

	public interface IBlockchainTriggerMessageSet<T> : IBlockchainTriggerMessageSet, ITriggerMessageSet<T, IBlockchainEventsRehydrationFactory>, IBlockchainTargettedMessageSet<T>
		where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory> {
	}

	public class BlockchainTriggerMessageSet<T> : TriggerMessageSet<T, IBlockchainEventsRehydrationFactory>, IBlockchainTriggerMessageSet<T>
		where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory> {
	}
}