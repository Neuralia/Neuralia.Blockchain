using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Exceptions;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest {
	public class ClientMessageGroupManifestWorkflow<R> : ClientWorkflow<MessageGroupManifestMessageFactory<R>, R>
		where R : IRehydrationFactory {

		/// <summary>
		///     gossip messages that are ready to go out
		/// </summary>
		/// <returns></returns>
		protected readonly ConcurrentDictionary<Guid, PeerMessageQueue> peerMessageQueues = new ConcurrentDictionary<Guid, PeerMessageQueue>();


		private readonly IDataAccessService dataAccessService;
		protected readonly IGlobalsService globalsService;

		private const int DISCONNECTED_RETRY_ATTEMPS = 3;
		private const int TIMEOUT_STRIKES_COUNT = 2;
		
		private readonly TimeSpan GOSSIP_REST_TIME = TimeSpan.FromSeconds(3);
		
		private readonly TimeSpan GOSSIP_INCOMING_TIME = TimeSpan.FromSeconds(1);
		
		private readonly TimeSpan MESSAGE_TIMEOUT_SPAN = TimeSpan.FromSeconds(10);
		
		
		public ClientMessageGroupManifestWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {

			this.dataAccessService = serviceSet.DataAccessService;
			this.globalsService = serviceSet.GlobalsService;
			
			// we only run one of these
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			// very high priority
			this.Priority = Workflow.Priority.High;
		}

		/// <summary>
		///     this method will forward a gossip message to any connected peer that our cache indicates has never received it and
		///     update the cache to reflect so
		/// </summary>
		public void ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet) {

			// gossip messages are only sent to nodes that support them
			var gossipConnections = this.networkingService.ConnectionStore.BasicGossipConnectionsList;

			// if its a block, then we send it only to the full types of nodes
			if(gossipMessageSet.MinimumNodeTypeSupport.HasFlag(Enums.PeerTypeSupport.FullGossip)) {
				gossipConnections = this.networkingService.ConnectionStore.FullGossipConnectionsList;
			}

			this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemFilesDirectoryPath(), this.serviceSet).ForwardValidGossipMessage(gossipMessageSet.BaseHeader.Hash, gossipConnections.Select(c => c.ScoppedIp).ToList(), peerNotReceived => {
				// first update our peers to match any new connection since
				this.UpdatePeerConnections();

				var sentIps = new List<string>();

				// and send the message to those who have not received it (as far as we know)
				foreach(string sendpeer in peerNotReceived) // thats it, now we add the outbound message to this peer
				{
					try {
						var peers = gossipConnections.Where(p => p.ScoppedIp == sendpeer).ToList();

						if(!peers.Any()) {
							continue;
						}

						lock(this.locker) {
							foreach(var peer in peers) {
								if(!this.peerMessageQueues.ContainsKey(peer.ClientUuid)) {
									continue;
								}

								this.peerMessageQueues[peer.ClientUuid].outboundMessagesQueue.Add(gossipMessageSet);
							}
						}
						sentIps.Add(sendpeer);
					} catch(Exception ex) {
						//not much to do here, just eat it up. what matters is that we send it to others
						Log.Error(ex, "Failed to forward valid gossip message");
					}
				}

				// return the list of peers we forwarded it to
				return sentIps;
			});
		}
		
		/// <summary>
		///     make sure that we update our peer message queues to reflect any new peer connection we may have now
		/// </summary>
		private void UpdatePeerConnections() {
			var connections = this.networkingService.ConnectionStore.AllConnections.Select(c => (c.Key, c.Value.ClientUuid)).ToList();

			foreach((Guid Key, Guid ClientUuid) conn in connections) {

				if(!this.peerMessageQueues.ContainsKey(conn.ClientUuid)) {
					PeerMessageQueue peerMessageQueue = new PeerMessageQueue();
					peerMessageQueue.Connection = this.networkingService.ConnectionStore.AllConnections[conn.Key];

					this.peerMessageQueues.AddSafe(conn.ClientUuid, peerMessageQueue);
				}
			}

			// remove obsolete ones
			var connectionIps = connections.Select(c => c.ClientUuid).ToList();
			
			foreach(var uuid in this.peerMessageQueues.Where(c => !connectionIps.Contains(c.Key)).ToArray()) {
				this.peerMessageQueues.RemoveSafe(uuid.Key);
			}
		}
		
		protected virtual void HandleMessages(IColoredTask task) {
			if(task is QueueMessageListTask messageTask) {
				this.HandleGossipMessageReceived(messageTask);
			}
		}

		/// <summary>
		///     an external sourced gossip message was reiceved, lets handle it
		/// </summary>
		/// <param name="gossipMessageTask"></param>
		/// <exception cref="ApplicationException"></exception>
		protected virtual void HandleGossipMessageReceived(QueueMessageListTask gossipMessageTask) {

		}

		private bool loop = true;
		private readonly AutoResetEvent autoResetEvent = new AutoResetEvent(false);
		private DateTime? nextGossipMessageDispatch;
		private DateTime? nextincomingGossipMessageCheck;
		
		protected override void PerformWork() {

			while(this.loop) {
				
				if(this.ShouldAct(ref this.nextGossipMessageDispatch)) {
					this.CheckShouldCancel();

					try {
						this.DisatchMessageQueues();

						this.CleanTimeouts();
					} catch(Exception ex) {
						//TODO: what shouldwe do here?
					}

					this.CheckShouldCancel();
					
					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextGossipMessageDispatch = DateTime.UtcNow + this.GOSSIP_REST_TIME;
				}
				
				if(this.ShouldAct(ref this.nextincomingGossipMessageCheck)) {
					this.CheckShouldCancel();

					try {
						this.ProcessIncomingResponses();
					} catch(Exception ex) {
						//TODO: what shouldwe do here?
					}

					this.CheckShouldCancel();
					
					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextincomingGossipMessageCheck = DateTime.UtcNow + this.GOSSIP_INCOMING_TIME;
				}
				
				this.autoResetEvent.WaitOne(1000);
			}
		}
		
		/// <summary>
		///     this method allows to check if its time to act, or if we should sleep more
		/// </summary>
		/// <returns></returns>
		protected bool ShouldAct(ref DateTime? action) {
			if(!action.HasValue) {
				return true;
			}

			if(action.Value < DateTime.UtcNow) {
				action = null;

				return true;
			}

			return false;
		}

		public override void Stop() {

			this.loop = false;
			this.autoResetEvent.Set();
			this.CancelTokenSource.Cancel();
			
			base.Stop();
		}

		private void CleanTimeouts() {
			foreach(var queue in this.peerMessageQueues.ToArray()) {
				foreach(var expired in queue.Value.sentMessages.Where(ms => ms.Value.initiated && (ms.Value.manifestSentTime + this.MESSAGE_TIMEOUT_SPAN) < DateTime.UtcNow).ToArray()) {

					expired.Value.timeoutStrikes++;

					if(expired.Value.timeoutStrikes >= TIMEOUT_STRIKES_COUNT) {
						Log.Warning($"Client {queue.Value.Connection.ClientUuid} seems to have disconnected and never replied to the group message. it will be removed from the message queue");
						queue.Value.sentMessages.RemoveSafe(expired.Key);
					} else {
						Log.Warning($"Client {queue.Value.Connection.ClientUuid} never replied to it's group message request. A retry will be attempted");
						// give it another chance, it will be attempted a resend
						expired.Value.initiated = false;
						expired.Value.manifestSentTime = null;
					}
				}
			}
		}
		
		private void ProcessIncomingResponses() {

			while(this.HasMessages) {

				List<TargettedMessageSet<MessageGroupManifestServerReply<R>, R>> serverMessageGroupManifests;

				try {
					serverMessageGroupManifests = this.WaitNetworkMessages<MessageGroupManifestServerReply<R>, R>(TimeSpan.FromSeconds(1));
				} 
				catch {
					// seems we have no messages
					return;
				}
				
				foreach(var groupReply in serverMessageGroupManifests) {
					
					// we got a reply, lets process it
					if(this.peerMessageQueues.TryGetValue(groupReply.Header.ClientId, out var queue)) {

						try {
							if(queue.sentMessages.TryRemove(groupReply.Message.sessionId, out var setMessageSet)) {

								// ok, now we know which messages to send back
								var approvals = groupReply.Message.messageApprovals;

								var messageSetGroup = this.MessageFactory.CreateClientMessageGroupReplySet(setMessageSet.trigger.Header);

								if(((approvals.Count == 0) || !approvals.Any(a => a))) {
									continue; // the client doesnt want anything, its the end
								}

								// ok, lets add the gossip messages that the server selected
								for(int i = 0; i < approvals.Count; i++) {
									if(approvals[i]) {
										// here we send the message. if we stored the byte array, we can just reuse it
										messageSetGroup.Message.gossipMessageSets.Add(setMessageSet.messages[i].HasDeserializedData ? setMessageSet.messages[i].DeserializedData : setMessageSet.messages[i].Dehydrate());
									}
								}

								Log.Verbose($"Sending {messageSetGroup.Message.gossipMessageSets.Count} gossip messages targeted to peer {setMessageSet.Connection.ScoppedAdjustedIp}");

								if(!this.SendMessage(setMessageSet.Connection, messageSetGroup)) {
									Log.Verbose($"Connection with peer  {setMessageSet.Connection.ScoppedAdjustedIp} was terminated");
								}

								//setMessageSet.trigger.Message?.Dispose();
								foreach(var message in setMessageSet.messages) {
									//	message.BaseMessage?.Dispose();
								}
							}
						} catch {
							if(queue.sentMessages.ContainsKey(groupReply.Message.sessionId)) {
								queue.sentMessages.RemoveSafe(groupReply.Message.sessionId);
							}
						}
					}
					
				}
			}
		}

		private void DisatchMessageQueues() {
			this.CheckShouldCancel();

			var actions = new List<Action>();

			// let's retry those who were not sent
			foreach(var messageQueue in this.peerMessageQueues.Where(q => q.Value.sentMessages.Any(s => !s.Value.initiated))) {
				actions.Add(() => {

					foreach(var messageSendSession in messageQueue.Value.sentMessages.Where(s => !s.Value.initiated)) {
						// send the messages for processing
						this.SendPeerGossipGroup(messageSendSession.Value, messageQueue.Value);
					}
				});
			}
			
			// now the new messages
			foreach(var messageGroup in this.peerMessageQueues.Where(q => q.Value.outboundMessagesQueue.Any()).ToArray()) {

				actions.Add(() => {

					this.CheckShouldCancel();
					PeerMessageQueue queue = messageGroup.Value;
					
					// lets mark it all as we have a manifest in progress
					PeerMessageQueue.MessageSendSession messageSendSession = new PeerMessageQueue.MessageSendSession();
					messageSendSession.Connection = queue.Connection;
					
					lock(this.locker) {
						while(queue.outboundMessagesQueue.TryTake(out var message)) {
							messageSendSession.messages.Add(message);
						}
					}

					if(!messageSendSession.messages.Any()) {
						return;
					}
					
					bool contains = true;
					// make sure we have a unique session Id with the peer
					do {
						messageSendSession.sessionId = GlobalRandom.GetNext();

						contains = queue.sentMessages.ContainsKey(messageSendSession.sessionId);

					} while(contains);
					
					queue.sentMessages.AddSafe(messageSendSession.sessionId, messageSendSession);
					
					// send the messages for processing
					this.SendPeerGossipGroup(messageSendSession, queue);
				});
			}

			IndependentActionRunner.Run(actions.ToArray());
		}

		protected void SendPeerGossipGroup(PeerMessageQueue.MessageSendSession messageSendSession, PeerMessageQueue queue) {
			// first, see if we have any gossip messages. if we do, we will ask the server which one it wants. if we dont, then we send all messages in the trigger and end it there
			var gossipMessages = messageSendSession.messages.Where(m => Enums.DoesPeerTypeSupport(messageSendSession.Connection.PeerType, m.MinimumNodeTypeSupport)).ToList();

			messageSendSession.trigger = this.MessageFactory.CreateMessageGroupManifestWorkflowTriggerSet(this.CorrelationId);

			//we need to keep track of our tries
			messageSendSession.sendAttempts += 1;

			// lets establish our correlation
			messageSendSession.trigger.Message.sessionId = messageSendSession.sessionId;
			
			Log.Verbose($"Sending message manifest with {messageSendSession.messages.Count} messages to peer {messageSendSession.Connection.ScoppedAdjustedIp}");

			// first hash our messages and see if we have any to send out
			// hash only new gossip messages. we dont hash targeted messages, and received forwards are already hashed and there is nothing to do

			foreach(IGossipMessageSet newGossipMessage in gossipMessages.Where(gm => gm.BaseHeader.Hash == 0)) {
				newGossipMessage.BaseHeader.Hash = HashingUtils.Generate_xxHash(newGossipMessage);
			}

			messageSendSession.trigger.Message.messageInfos.AddRange(gossipMessages.Select(gm => new GossipGroupMessageInfo<R> {Hash = gm.BaseHeader.Hash, GossipMessageMetadata = gm.MessageMetadata}));

			try {
				if(this.SendMessage(messageSendSession.Connection, messageSendSession.trigger)) {

					messageSendSession.sendAttempts = 1;
					messageSendSession.initiated = true;
					messageSendSession.manifestSentTime = this.timeService.CurrentRealTime;
				} else {
					Log.Verbose($"Connection with peer {messageSendSession.Connection.ScoppedAdjustedIp} failed to send message");
					throw new ApplicationException();
				}
			} catch {
				if(messageSendSession.sendAttempts >= DISCONNECTED_RETRY_ATTEMPS) {
					Log.Warning($"Client {queue.Connection.ClientUuid} seems to have disconnected and has messages to be sent. we will now be removing it permanently.");
					queue.sentMessages.RemoveSafe(messageSendSession.sessionId);
				} else {
					Log.Warning($"Client {queue.Connection.ClientUuid} seems to have disconnected and has messages to be sent. A retry will be attempted");
				}
			}
		}
		
		protected override MessageGroupManifestMessageFactory<R> CreateMessageFactory() {
			return new MessageGroupManifestMessageFactory<R>(this.serviceSet);
		}

		public class QueueMessageListTask : ColoredTask {
			public readonly List<IGossipMessageSet> messages;
			public readonly PeerConnection peerConnection;

			public QueueMessageListTask(List<IGossipMessageSet> messages, PeerConnection peerConnection) {
				this.messages = messages;
				this.peerConnection = peerConnection;
			}
		}
		
		public class PeerMessageQueue {
			
			public PeerConnection Connection;
			
			
			/// <summary>
			///     messages that are waiting to be processed and sent out
			/// </summary>
			/// <returns></returns>
			public readonly BlockingCollection<IGossipMessageSet> outboundMessagesQueue = new BlockingCollection<IGossipMessageSet>();
			
			public readonly ConcurrentDictionary<int, MessageSendSession> sentMessages = new ConcurrentDictionary<int, MessageSendSession>();
			
			public class MessageSendSession {

				public int sessionId;

				public int sendAttempts;
				public int timeoutStrikes;

				public TriggerMessageSet<MessageGroupManifestTrigger<R>, R> trigger;
				public PeerConnection Connection;
				
				public readonly List<IGossipMessageSet> messages = new List<IGossipMessageSet>();
				/// <summary>
				///     when we sent the manifest, in case we need to timeout
				/// </summary>
				public DateTime? manifestSentTime;

				/// <summary>
				/// true if it was actually sent, otherwise false
				/// </summary>
				public bool initiated;
			}
		}
	}
}