using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.BlockchainMessageReceivedGossip.Messages.V1 {
	public interface IBlockchainMessageCreatedGossipMessage : IBlockchainGossipWorkflowTriggerMessage {
	}

	public abstract class BlockchainMessageCreatedGossipMessage<EVENT_ENVELOPE_TYPE> : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, IBlockchainMessageCreatedGossipMessage
		where EVENT_ENVELOPE_TYPE : class, IMessageEnvelope {

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
			return GossipWorkflowIDs.MESSAGE_RECEIVED;
		}
	}
}