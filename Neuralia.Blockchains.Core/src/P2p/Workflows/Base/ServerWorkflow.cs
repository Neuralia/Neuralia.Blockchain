using System.Threading.Tasks;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public interface IServerWorkflow<R> : INetworkWorkflow<R>
		where R : IRehydrationFactory {
	}

	public abstract class ServerWorkflow<WORKFLOW_TRIGGER_MESSAGE, MESSAGE_FACTORY, R> : NetworkWorkflow<MESSAGE_FACTORY, R>, IServerWorkflow<R>
		where WORKFLOW_TRIGGER_MESSAGE : WorkflowTriggerMessage<R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {
		protected readonly PeerConnection ClientConnection;

		/// <summary>
		///     the trigger message that prompted the creation of this server workflow
		/// </summary>
		protected readonly TriggerMessageSet<WORKFLOW_TRIGGER_MESSAGE, R> triggerMessage;

		public ServerWorkflow(TriggerMessageSet<WORKFLOW_TRIGGER_MESSAGE, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(serviceSet) {
			this.triggerMessage = triggerMessage;
			this.ClientConnection = clientConnection;

			// we give ourselves the same ID as the other side.
			this.CorrelationId = this.triggerMessage.Header.WorkflowCorrelationId;

			this.SessionId = this.triggerMessage.Header.WorkflowSessionId;

			// we need this also, to scope this workflow as a response to a certain client
			this.ClientId = clientConnection.ClientUuid;
		}

		protected Task<bool> Send(INetworkMessageSet message) {
			return this.SendMessage(this.ClientConnection, message);
		}

		protected Task<bool> SendFinal(INetworkMessageSet message) {
			return this.SendFinalMessage(this.ClientConnection, message);
		}

		private Task<bool> SendBytes(SafeArrayHandle data) {
			return this.SendBytes(this.ClientConnection, data);
		}

		private Task<bool> SendFinalBytes(SafeArrayHandle data) {
			return this.SendFinalBytes(this.ClientConnection, data);
		}
	}
}