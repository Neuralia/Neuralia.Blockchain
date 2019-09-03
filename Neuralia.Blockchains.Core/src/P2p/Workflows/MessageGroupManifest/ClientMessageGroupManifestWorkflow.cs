using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest {
	public class ClientMessageGroupManifestWorkflow<R> : ClientWorkflow<MessageGroupManifestMessageFactory<R>, R>
		where R : IRehydrationFactory {
		private readonly List<INetworkMessageSet> messages;
		private readonly PeerConnection peerConnection;

		public ClientMessageGroupManifestWorkflow(List<INetworkMessageSet> messages, PeerConnection peerConnection, ServiceSet<R> serviceSet) : base(serviceSet) {
			this.peerConnection = peerConnection;
			this.messages = messages;

			// if there are many per peer at a time, ensure they are sequential
			this.ExecutionMode = Workflow.ExecutingMode.Sequential;

			this.PeerUnique = true;

			// very high priority
			this.Priority = Workflow.Priority.High;
		}

		public NodeAddressInfo NodeAddressInfo => this.peerConnection.NodeAddressInfoInfo;

		protected override void PerformWork() {
			this.CheckShouldCancel();

			// first, see if we have any gossip messages. if we do, we will ask the server which one it wants. if we dont, then we send all messages in the trigger and end it there
			var gossipMessages = this.messages.OfType<IGossipMessageSet>().Where(m => Enums.DoesPeerTypeSupport(this.peerConnection.PeerType, m.MinimumNodeTypeSupport)).ToList();
			var targettedMessages = this.messages.OfType<ITargettedMessageSet>().ToList();

			var trigger = this.MessageFactory.CreateMessageGroupManifestWorkflowTriggerSet(this.CorrelationId);

			Log.Verbose($"Sending message manifest with {this.messages.Count} messages to peer {this.peerConnection.ScopedAdjustedIp}");

			// ok, lets build our message group and send the manifest
			if(!gossipMessages.Any()) {
				// well, it seems we have no gossip messages, so we just send all our targeted messages in the trigger and end it there

				// here we force a cast to ENSURE that they are all targeted messages, otherwise get an exception
				foreach(IByteArray message in this.messages.Select(m => (ITargettedMessageSet) m).Select(tm => tm.Dehydrate())) {
					trigger.Message.targettedMessageSets.Add(message);
				}

				if(!this.SendMessage(this.peerConnection, trigger)) {
					Log.Verbose($"Connection with peer  {this.peerConnection.ScopedAdjustedIp} was terminated");

					return;
				}

				return;
			}

			// first hash our messages and see if we have any to send out
			// hash only new gossip messages. we dont hash targeted messages, and received forwards are already hashed and there is nothing to do

			foreach(IGossipMessageSet newGossipMessage in gossipMessages.Where(gm => gm.BaseHeader.Hash == 0)) {
				newGossipMessage.BaseHeader.Hash = HashingUtils.Generate_xxHash(newGossipMessage);
			}

			trigger.Message.messageInfos.AddRange(gossipMessages.Select(gm => new GossipGroupMessageInfo<R> {Hash = gm.BaseHeader.Hash, GossipMessageMetadata = gm.MessageMetadata}));
			trigger.Message.targettedMessageCount = targettedMessages.Count();

			if(!this.SendMessage(this.peerConnection, trigger)) {
				Log.Verbose($"Connection with peer  {this.peerConnection.ScopedAdjustedIp} was terminated");

				return;
			}

			var serverMessageGroupManifest = this.WaitSingleNetworkMessage<MessageGroupManifestServerReply<R>, TargettedMessageSet<MessageGroupManifestServerReply<R>, R>, R>();

			// ok, now we know which messages to send back
			var approvals = serverMessageGroupManifest.Message.messageApprovals;

			var messageSetGroup = this.MessageFactory.CreateClientMessageGroupReplySet(trigger.Header);

			if(((approvals.Count == 0) || !approvals.Any(a => a)) && !targettedMessages.Any()) {
				return; // the server doesnt want anything, its the end
			}

			// ok, lets add the gossip messages that the server selected
			for(int i = 0; i < approvals.Count; i++) {
				if(approvals[i]) {
					// here we send the message. if we stored the byte array, we can just reuse it
					messageSetGroup.Message.gossipMessageSets.Add(gossipMessages[i].HasDeserializedData ? gossipMessages[i].DeserializedData : gossipMessages[i].Dehydrate());
				}
			}

			// of course, lets also add all our targeted messages
			messageSetGroup.Message.targettedMessageSets.AddRange(targettedMessages.Select(m => m.Dehydrate()));

			Log.Verbose($"Sending {messageSetGroup.Message.gossipMessageSets.Count} gossip messages and {messageSetGroup.Message.targettedMessageSets.Count} targeted to peer {this.peerConnection.ScopedAdjustedIp}");

			if(!this.SendMessage(this.peerConnection, messageSetGroup)) {
				Log.Verbose($"Connection with peer  {this.peerConnection.ScopedAdjustedIp} was terminated");

			}

		}

		protected override MessageGroupManifestMessageFactory<R> CreateMessageFactory() {
			return new MessageGroupManifestMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ClientMessageGroupManifestWorkflow<R> otherWorkflow) {
				return this.NodeAddressInfo == otherWorkflow.NodeAddressInfo;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}