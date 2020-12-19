using System;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Core.P2p.Workflows {
	public interface IServerWorkflowFactory<R> : IWorkflowFactory<R>
		where R : IRehydrationFactory {
		INetworkingWorkflow<R> CreateResponseWorkflow(ITriggerMessageSet<R> messageSet, PeerConnection peerConnectionn);
	}

	public class ServerWorkflowFactory<R> : WorkflowFactory<R>, IServerWorkflowFactory<R>
		where R : IRehydrationFactory {

		public ServerWorkflowFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public INetworkingWorkflow<R> CreateResponseWorkflow(ITriggerMessageSet<R> messageSet, PeerConnection peerConnectionn) {
			if(!messageSet.MessageCreated) {
				throw new ApplicationException("Message must be created or loaded");
			}

			if(!messageSet.Header.IsWorkflowTrigger) {
				throw new ApplicationException("Message must be both a trigger and set as such");
			}

			if((messageSet.BaseMessage.WorkflowType == WorkflowIDs.HANDSHAKE) && (messageSet.BaseMessage != null) && messageSet.BaseMessage is HandshakeTrigger<R>) {
				return this.CreateServerHandshakeWorkflow(messageSet as TriggerMessageSet<HandshakeTrigger<R>, R>, peerConnectionn);
			}

			if((messageSet.BaseMessage.WorkflowType == WorkflowIDs.PEER_LIST_REQUEST) && (messageSet.BaseMessage != null) && messageSet.BaseMessage is PeerListRequestTrigger<R>) {
				return this.CreateServerPeerListRequestWorkflow(messageSet as TriggerMessageSet<PeerListRequestTrigger<R>, R>, peerConnectionn);
			}

			if((messageSet.BaseMessage.WorkflowType == WorkflowIDs.MESSAGE_GROUP_MANIFEST) && (messageSet.BaseMessage != null) && messageSet.BaseMessage is MessageGroupManifestTrigger<R>) {
				return this.CreateServerMessageGroupManifestWorkflow(messageSet as TriggerMessageSet<MessageGroupManifestTrigger<R>, R>, peerConnectionn);
			}

			return null;
		}

		protected virtual INetworkingWorkflow<R> CreateServerHandshakeWorkflow(TriggerMessageSet<HandshakeTrigger<R>, R> messageSet, PeerConnection peerConnectionn) {
			return new ServerHandshakeWorkflow<R>(messageSet, peerConnectionn, this.serviceSet);
		}

		protected virtual INetworkingWorkflow<R> CreateServerPeerListRequestWorkflow(TriggerMessageSet<PeerListRequestTrigger<R>, R> messageSet, PeerConnection peerConnectionn) {
			return new ServerPeerListRequestWorkflow<R>(messageSet, peerConnectionn, this.serviceSet);
		}

		protected virtual INetworkingWorkflow<R> CreateServerMessageGroupManifestWorkflow(TriggerMessageSet<MessageGroupManifestTrigger<R>, R> messageSet, PeerConnection peerConnectionn) {
			return new ServerMessageGroupManifestWorkflow<R>(messageSet, peerConnectionn, this.serviceSet);
		}
	}
}