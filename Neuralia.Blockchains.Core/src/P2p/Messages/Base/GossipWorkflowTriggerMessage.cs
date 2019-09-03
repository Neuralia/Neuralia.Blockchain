using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {

	public interface IGossipWorkflowTriggerMessage : IWorkflowTriggerMessage {
	}

	public static class GossipWorkflowTriggerMessageConstants {
		public const ushort GOSSIP_MESSAGE_TRIGGER = 1;
	}

	/// <summary>
	///     base class for gossip messages. they have the ability to hash themselves
	/// </summary>
	/// <remarks>
	///     Gossip messages are special messages that will be sent virally in the p2p network. In order to force players to
	///     play nice,
	///     each gossip message MUST be identified and Signed by this account. in order to achieve this, we force these
	///     messages to ALWAYS
	///     contain a transaction. these always have an account and a signature, which ensures the
	///     message is itself
	///     identified. Peers will only relay gosisp messages that
	///     are valid. any invalid message will die, and mark the account (or IP) as ill intended.
	/// </remarks>
	public interface IGossipWorkflowTriggerMessage<R> : IWorkflowTriggerMessage<R>, IGossipWorkflowTriggerMessage
		where R : IRehydrationFactory {
	}

	public abstract class GossipWorkflowTriggerMessage<R> : WorkflowTriggerMessage<R>, IGossipWorkflowTriggerMessage
		where R : IRehydrationFactory {
	}
}