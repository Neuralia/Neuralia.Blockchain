using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets {

	public interface ITriggerMessageSet : ITargettedMessageSet {
	}

	public interface ITriggerMessageSet<R> : ITriggerMessageSet, ITargettedMessageSet<R>
		where R : IRehydrationFactory {
		new WorkflowTriggerMessage<R> BaseMessage { get; }
	}

	public interface ITriggerMessageSet<T, R> : ITargettedMessageSet<T, R>, ITriggerMessageSet<R>
		where T : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {
	}

	public class TriggerMessageSet<T, R> : TargettedMessageSet<T, R>, ITriggerMessageSet<T, R>
		where T : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {
		public new WorkflowTriggerMessage<R> BaseMessage => this.Message;
	}
}