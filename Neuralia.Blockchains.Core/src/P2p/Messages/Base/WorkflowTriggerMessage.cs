using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {

	public interface IWorkflowTriggerMessage : INetworkMessage {
	}

	public interface IWorkflowTriggerMessage<R> : INetworkMessage<R>, IWorkflowTriggerMessage
		where R : IRehydrationFactory {
	}

	/// <summary>
	///     a special class that identifies network messages that will trigger specific workflows
	/// </summary>
	public abstract class WorkflowTriggerMessage<R> : NetworkMessage<R>, IWorkflowTriggerMessage<R>
		where R : IRehydrationFactory {
	}
}