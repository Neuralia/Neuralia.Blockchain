using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.BlockReceivedGossip.Messages.V1 {
	public interface IBlockCreatedGossipMessage : IBlockchainGossipWorkflowTriggerMessage {
	}

	public abstract class BlockCreatedGossipMessage<EVENT_ENVELOPE_TYPE> : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, IBlockCreatedGossipMessage
		where EVENT_ENVELOPE_TYPE : class, IBlockEnvelope {

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

		}

		protected override (ushort major, ushort minor) SetGossipIdentity() {
			return (1, 0);
		}

		protected override short SetWorkflowType() {
			return GossipWorkflowIDs.BLOCK_RECEIVED;
		}
	}
}