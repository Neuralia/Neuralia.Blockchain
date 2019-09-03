using System;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Core.P2p.Workflows {
	public interface IGossipWorkflowFactory<R>
		where R : IRehydrationFactory {
		IWorkflow<R> CreateGossipResponseWorkflow(IGossipMessageSet<R> messageSet, PeerConnection peerConnectionn);
	}

	/// <summary>
	///     Since gossip workflows are security risks, we isolate them from the targeted ones, to minimize risks of errors
	/// </summary>
	public class GossipWorkflowFactory<R> : WorkflowFactory<R>, IGossipWorkflowFactory<R>
		where R : IRehydrationFactory {

		public GossipWorkflowFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public IWorkflow<R> CreateGossipResponseWorkflow(IGossipMessageSet<R> messageSet, PeerConnection peerConnectionn) {
			if(!messageSet.MessageCreated) {
				throw new ApplicationException("Message must be created or loaded");
			}

			// if(messageSet.BaseMessage.workflowType == WorkflowIDs.HANDSHAKE && messageSet.BaseMessage != null && messageSet.BaseMessage is HandshakeTrigger){
			//     return new ServerHandshakeWorkflow(messageSet as TriggerMessageSet<HandshakeTrigger>, clientConnection);
			// }

			// if(messageSet.BaseMessage.workflowType == WorkflowIDs.PEER_LIST_REQUEST && messageSet.BaseMessage != null && messageSet.BaseMessage is PeerListRequestTrigger){
			//     return new ServerPeerListRequestWorkflow(messageSet as TriggerMessageSet<PeerListRequestTrigger>, clientConnection);
			// }

			// if(messageSet.BaseMessage.workflowType == WorkflowIDs.MESSAGE_GROUP_MANIFEST && messageSet.BaseMessage != null && messageSet.BaseMessage is MessageGroupManifestTrigger){
			//     return new ServerMessageGroupManifestWorkflow(messageSet as TriggerMessageSet<MessageGroupManifestTrigger>, clientConnection);
			// }
			return null;
		}
	}
}