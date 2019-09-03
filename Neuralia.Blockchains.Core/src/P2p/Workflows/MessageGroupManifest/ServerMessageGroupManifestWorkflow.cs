using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest {
	public static class ServerMessageGroupManifestWorkflow {
		public static readonly Dictionary<BlockchainType, IMessageGroupGossipMetadataAnalyser> GossipMetadataAnalysers = new Dictionary<BlockchainType, IMessageGroupGossipMetadataAnalyser>();
		public static readonly Dictionary<BlockchainType, IRehydrationFactory> ChainRehydrationFactories = new Dictionary<BlockchainType, IRehydrationFactory>();
	}

	public class ServerMessageGroupManifestWorkflow<R> : ServerWorkflow<MessageGroupManifestTrigger<R>, MessageGroupManifestMessageFactory<R>, R>
		where R : IRehydrationFactory {

		private readonly IDataAccessService dataAccessService;

		protected readonly IGlobalsService globalsService;

		public ServerMessageGroupManifestWorkflow(TriggerMessageSet<MessageGroupManifestTrigger<R>, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(triggerMessage, clientConnection, serviceSet) {
			this.globalsService = serviceSet.GlobalsService;
			this.dataAccessService = serviceSet.DataAccessService;

			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			this.PeerUnique = true;

			// very high priority
			this.Priority = Workflow.Priority.High;
		}

		protected override void PerformWork() {
			this.CheckShouldCancel();

			// ok, we just received a trigger, lets examine it

			// ok, if there are messages in the trigger, we end there
			if((this.triggerMessage.Message.targettedMessageSets.Count != 0) || (this.triggerMessage.Message.messageInfos.Count == 0)) {
				//send the targeted messages
				foreach(IByteArray message in this.triggerMessage.Message.targettedMessageSets) // thats it, we formally receive the messages and send them to our message manager.
				{
					this.networkingService.PostNetworkMessage(message, this.ClientConnection);
				}

				return;
			}

			var reply = this.MessageFactory.CreateServerMessageGroupManifestSet(this.triggerMessage.Header);

			Log.Verbose($"Received {this.triggerMessage.Message.messageInfos.Count} gossip message hashes from peer {this.ClientConnection.ScopedAdjustedIp}");

			// here we check which messages in the group we have already received, and which ones are new
			(var messageReceived, int alreadyReceivedCount) = this.PerpareGossipMessageAcceptations();

			int refusingCount = messageReceived.Count(m => !m);

			Log.Verbose($"We already previously received {alreadyReceivedCount} out of {this.triggerMessage.Message.messageInfos.Count} messages just received. {refusingCount} messages will be ignored from peer {this.ClientConnection.ScopedAdjustedIp}.");

			reply.Message.messageApprovals.AddRange(messageReceived);

			if(!this.Send(reply)) {
				Log.Verbose($"Connection with peer  {this.ClientConnection.ScopedAdjustedIp} was terminated");

				return;
			}

			if(!reply.Message.messageApprovals.Any(a => a) && (this.triggerMessage.Message.targettedMessageCount == 0)) {
				return;
			}

			var serverMessageGroupManifest = this.WaitSingleNetworkMessage<ClientMessageGroupReply<R>, TargettedMessageSet<ClientMessageGroupReply<R>, R>, R>();

			Log.Verbose($"We received {serverMessageGroupManifest.Message.gossipMessageSets.Count} gossip messages and {serverMessageGroupManifest.Message.targettedMessageSets.Count} targeted messages from peer {this.ClientConnection.ScopedAdjustedIp}");

			// ok, these are really messages, lets handle them as such
			foreach(IByteArray message in serverMessageGroupManifest.Message.gossipMessageSets) // thats it, we formally receive the messages and send them to our message manager.
			{
				this.networkingService.PostNetworkMessage(message, this.ClientConnection);
			}

			foreach(IByteArray message in serverMessageGroupManifest.Message.targettedMessageSets) // thats it, we formally receive the messages and send them to our message manager.
			{
				this.networkingService.PostNetworkMessage(message, this.ClientConnection);
			}
		}

		protected virtual (List<bool> messageReceived, int alreadyReceivedCount) PerpareGossipMessageAcceptations() {
			IMessageRegistryDal sqliteDal = this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemFilesDirectoryPath(), this.serviceSet);

			var gossipMessages = this.triggerMessage.Message.messageInfos.Select((mi, index) => (mi, index, mi.Hash)).ToList();

			var messageReceived = sqliteDal.CheckMessagesReceived(gossipMessages.Select(mi => mi.Hash).ToList(), this.ClientConnection);

			//TODO: here we can add rate limiting on messages by refusing messages if they come too quickly
			int alreadyReceivedCount = messageReceived.Count(m => !m);

			// now we do extra processing by verifying the metadata associated with each message.
			foreach(var metadataEntry in gossipMessages.Where(e => e.mi.GossipMessageMetadata != null)) {

				// check the metadata and determine if we still want the message
				if(messageReceived[metadataEntry.index] && ServerMessageGroupManifestWorkflow.GossipMetadataAnalysers.ContainsKey(metadataEntry.mi.GossipMessageMetadata.BlockchainType)) {
					// we wanted the message. lets confirm we still want it

					// let's check the metadata
					messageReceived[metadataEntry.index] = ServerMessageGroupManifestWorkflow.GossipMetadataAnalysers[metadataEntry.mi.GossipMessageMetadata.BlockchainType].AnalyzeGossipMessageInfo(metadataEntry.mi);
				}
			}

			return (messageReceived, alreadyReceivedCount);
		}

		protected override MessageGroupManifestMessageFactory<R> CreateMessageFactory() {
			return new MessageGroupManifestMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ServerMessageGroupManifestWorkflow<R> otherWorkflow) {
				return this.triggerMessage.Header.originatorId == otherWorkflow.triggerMessage.Header.originatorId;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}