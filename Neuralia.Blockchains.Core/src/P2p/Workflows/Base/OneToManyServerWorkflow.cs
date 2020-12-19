using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {

	/// <summary>
	///     a special server workflow class that is designed to have one client and multiple server instances
	/// </summary>
	/// <typeparam name="WORKFLOW_TRIGGER_MESSAGE"></typeparam>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	/// <typeparam name="R"></typeparam>
	public abstract class OneToManyServerWorkflow<WORKFLOW_TRIGGER_MESSAGE, MESSAGE_FACTORY, R> : ServerWorkflow<WORKFLOW_TRIGGER_MESSAGE, MESSAGE_FACTORY, R>
		where WORKFLOW_TRIGGER_MESSAGE : WorkflowTriggerMessage<R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {

		protected OneToManyServerWorkflow(TriggerMessageSet<WORKFLOW_TRIGGER_MESSAGE, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(triggerMessage, clientConnection, serviceSet) {
		}
	}
}