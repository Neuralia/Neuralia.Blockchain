using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest {
	public class ClientMessageGroupManifestWorkflow<R> : OneToManyClientWorkflow<MessageGroupManifestMessageFactory<R>, R>
		where R : IRehydrationFactory {

		private const int DISCONNECTED_RETRY_ATTEMPS = 2;
		private const int TIMEOUT_STRIKES_COUNT = 2;
		private readonly AsyncManualResetEventSlim autoResetEvent = new AsyncManualResetEventSlim(false);

		private readonly IDataAccessService dataAccessService;
		protected readonly IGlobalsService globalsService;

		private readonly TimeSpan GOSSIP_REST_TIME = TimeSpan.FromSeconds(5);

		private readonly TimeSpan MESSAGE_TIMEOUT_SPAN = TimeSpan.FromSeconds(13);

		/// <summary>
		///     gossip messages that are ready to go out
		/// </summary>
		/// <returns></returns>
		protected readonly ConcurrentDictionary<Guid, PeerMessageQueue> peerMessageQueues = new ConcurrentDictionary<Guid, PeerMessageQueue>();

		private bool loop = true;

		private DateTime? nextGossipMessageDispatch;
		private DateTime? nextincomingGossipMessageCheck;

		public ClientMessageGroupManifestWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {

			this.dataAccessService = serviceSet.DataAccessService;
			this.globalsService = serviceSet.GlobalsService;

			// we only run one of these
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			this.IsLongRunning = true;

			// very high priority
			this.Priority = Workflow.Priority.High;

			this.networkMessageReceiver.MessageReceived += this.NetworkMessageReceiverOnMessageReceived;
		}

		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.LongRunning;

		private void NetworkMessageReceiverOnMessageReceived() {
			this.autoResetEvent.Set();
		}

		/// <summary>
		///     this method will forward a gossip message to any connected peer that our cache indicates has never received it and
		///     update the cache to reflect so
		/// </summary>
		public Task ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet) {

			// gossip messages are only sent to nodes that support them
			List<PeerConnection> gossipConnections = this.networkingService.ConnectionStore.BasicGossipConnectionsList;

			// if its a block, then we send it only to the full types of nodes
			if(gossipMessageSet.MinimumNodeGossipSupport == Enums.GossipSupportTypes.Full) {
				gossipConnections = this.networkingService.ConnectionStore.FullGossipConnectionsList;
			}

			IMessageRegistryDal dal = this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemFilesDirectoryPath(), this.serviceSet);

			return dal.ForwardValidGossipMessage(gossipMessageSet.BaseHeader.Hash, gossipConnections.Select(c => c.ScopedIp).ToList(), peerNotReceived => {
				// first update our peers to match any new connection since
				this.UpdatePeerConnections();

				List<string> sentIps = new List<string>();

				// and send the message to those who have not received it (as far as we know)
				foreach(string sendpeer in peerNotReceived) // thats it, now we add the outbound message to this peer
				{
					try {
						List<PeerConnection> peers = gossipConnections.Where(p => p.ScopedIp == sendpeer).ToList();

						if(!peers.Any()) {
							continue;
						}

						foreach(PeerConnection peer in peers) {
							if(!this.peerMessageQueues.ContainsKey(peer.ClientUuid)) {
								continue;
							}

							this.peerMessageQueues[peer.ClientUuid].outboundMessagesQueue.Add(gossipMessageSet);
						}

						sentIps.Add(sendpeer);
					} catch(Exception ex) {
						//not much to do here, just eat it up. what matters is that we send it to others
						NLog.Default.Error(ex, "Failed to forward valid gossip message");
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
			List<(Guid Key, Guid ClientUuid)> connections = this.networkingService.ConnectionStore.BasicGossipConnections.Where(c => !c.Value.IsDisposed && c.Value.IsFullyConfirmed).Select(c => (c.Key, c.Value.ClientUuid)).ToList();

			foreach((Guid key, Guid clientUuid) in connections) {

				if(!this.peerMessageQueues.ContainsKey(clientUuid)) {
					PeerMessageQueue peerMessageQueue = new PeerMessageQueue();
					peerMessageQueue.Connection = this.networkingService.ConnectionStore.BasicGossipConnections[key];

					this.peerMessageQueues.TryAdd(clientUuid, peerMessageQueue);
				}
			}

			// remove obsolete ones
			List<Guid> connectionIps = connections.Select(c => c.ClientUuid).ToList();

			foreach(KeyValuePair<Guid, PeerMessageQueue> uuid in this.peerMessageQueues.Where(c => !connectionIps.Contains(c.Key)).ToArray()) {
				this.peerMessageQueues.TryRemove(uuid.Key, out _);
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

		protected override Task DisposeAllAsync() {

			try {
				this.autoResetEvent?.Dispose();
			} catch {

			}

			return base.DisposeAllAsync();
		}

		protected override async Task<bool> PerformWork(LockContext lockContext) {

			while(this.loop) {

				if(this.ShouldAct(ref this.nextGossipMessageDispatch)) {
					this.CheckShouldCancel();

					try {
						this.CleanTimeouts();

						await this.DispatchMessageQueues().ConfigureAwait(false);
					} 
					catch(OutOfMemoryException oex) {
						// thats bad, lets clear everything

						this.peerMessageQueues.Clear();
						GC.Collect();
					
						throw;
					}
					catch(Exception ex) {
						//TODO: what shouldwe do here?
					}

					this.CheckShouldCancel();

					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextGossipMessageDispatch = DateTimeEx.CurrentTime + this.GOSSIP_REST_TIME;
				}

				try {
					await this.ProcessIncomingResponses().ConfigureAwait(false);
				} catch(Exception ex) {
					//TODO: what shouldwe do here?
				}

				if(await autoResetEvent.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false)) {
					this.autoResetEvent.Reset();
				}
			}
			
			return true;
		}

		/// <summary>
		///     this method allows to check if its time to act, or if we should sleep more
		/// </summary>
		/// <returns></returns>
		protected bool ShouldAct(ref DateTime? action) {
			if(!action.HasValue) {
				return true;
			}

			if(action.Value < DateTimeEx.CurrentTime) {
				action = null;

				return true;
			}

			return false;
		}

		public override Task Stop() {

			this.loop = false;
			this.autoResetEvent.Set();
			this.CancelTokenSource?.Cancel();

			return base.Stop();
		}

		private void CleanTimeouts() {

			foreach(KeyValuePair<Guid, PeerMessageQueue> queue in this.peerMessageQueues.ToArray()) {

				if(queue.Value.Connection.IsDisposed) {

					NLog.Default.Debug($"Client {queue.Value.Connection.ScopedIp} seems to have disconnected and the connection is dead. it will be removed from the message queue");
					this.peerMessageQueues.TryRemove(queue.Key, out var _);

					continue;
				}

				foreach(KeyValuePair<uint, PeerMessageQueue.MessageSendSession> expired in queue.Value.sentSessions.Where(ms => ms.Value.initiated && ((ms.Value.manifestSentTime + this.MESSAGE_TIMEOUT_SPAN) < DateTimeEx.CurrentTime)).ToArray()) {

					expired.Value.timeoutStrikes++;

					if(expired.Value.timeoutStrikes >= TIMEOUT_STRIKES_COUNT) {
						NLog.Default.Debug($"Client {queue.Value.Connection.ScopedIp} seems to have disconnected and never replied to the group message. it will be removed from the message queue");
						queue.Value.sentSessions.TryRemove(expired.Key, out var _);
					} else {
						NLog.Default.Debug($"Client {queue.Value.Connection.ScopedIp} never replied to it's group message request. A retry will be attempted");

						// give it another chance, it will be attempted a resend
						expired.Value.initiated = false;
						expired.Value.manifestSentTime = null;
					}
				}
			}
		}

		private async Task ProcessIncomingResponses() {

			while(this.HasMessages) {

				List<ITargettedMessageSet<MessageGroupManifestServerReply<R>, R>> serverMessageGroupManifests;

				try {
					serverMessageGroupManifests = await WaitNetworkMessages<MessageGroupManifestServerReply<R>, R>(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
				} catch {
					// seems we have no messages
					return;
				}

				// process latency checks
				foreach(ITargettedMessageSet<MessageGroupManifestServerReply<R>, R> groupReply in serverMessageGroupManifests) {
					if(this.peerMessageQueues.TryGetValue(groupReply.Header.ClientId, out PeerMessageQueue queue)) {

						queue.Connection.connection.AddLatency(queue.LatencyStart);
						queue.LatencyStart = DateTimeEx.MinValue;
					}
				}
					
				foreach(ITargettedMessageSet<MessageGroupManifestServerReply<R>, R> groupReply in serverMessageGroupManifests) {

					if(!groupReply.Header.WorkflowSessionId.HasValue) {
						continue;
					}

					

					// we got a reply, lets process it
					if(this.peerMessageQueues.TryGetValue(groupReply.Header.ClientId, out PeerMessageQueue queue)) {
						try {
							if(queue.sentSessions.TryRemove(groupReply.Header.WorkflowSessionId.Value, out PeerMessageQueue.MessageSendSession messageSendSession)) {

								try {
									// ok, now we know which messages to send back
									List<bool> approvals = groupReply.Message.messageApprovals;

									if((approvals.Count == 0) || !approvals.Any(a => a)) {
										continue; // the client doesnt want anything, its the end
									}

									TargettedMessageSet<ClientMessageGroupReply<R>, R> messageSetGroup = this.MessageFactory.CreateClientMessageGroupReplySet(messageSendSession.trigger.Header);

									// ok, lets add the gossip messages that the server selected
									for(int i = 0; i < approvals.Count; i++) {
										if(approvals[i]) {
											// here we send the message. if we stored the byte array, we can just reuse it
											messageSetGroup.Message.gossipMessageSets.Add(messageSendSession.messages[i].HasDeserializedData ? messageSendSession.messages[i].DeserializedData : messageSendSession.messages[i].Dehydrate());
										}
									}

									NLog.Default.Debug($"Sending {messageSetGroup.Message.gossipMessageSets.Count} gossip messages targeted to peer {messageSendSession.Connection.ScopedAdjustedIp}");

									try {

										if(messageSendSession.Connection.IsDisposed) {
											throw new ApplicationException();
										}

										await Repeater.RepeatAsync(async () => {
											if(!await SendMessage(messageSendSession.Connection, messageSetGroup).ConfigureAwait(false)) {
												throw new ApplicationException();
											}
										}).ConfigureAwait(false);
									} catch {
										NLog.Default.Debug($"Failed to send gossip group message reply message to peer {messageSendSession.Connection.ScopedAdjustedIp}");

										throw;
									} finally {
										//setMessageSet.trigger.Message?.Dispose();
										foreach(IGossipMessageSet message in messageSendSession.messages) {
											//	message.BaseMessage?.Dispose();
										}
									}
									
								} catch {
									NLog.Default.Warning("Failed to send response gossip message.");

									throw;
								}
							}
						} catch {
							queue.sentSessions.TryRemove(groupReply.Header.WorkflowSessionId.Value, out _);
						}
					}

				}
			}
		}

		private async Task DispatchMessageQueues() {
			this.CheckShouldCancel();

		
			List<Func<LockContext, Task>> actions = new List<Func<LockContext, Task>>();

			// let's retry those who were not sent
			foreach(KeyValuePair<Guid, PeerMessageQueue> messageQueue in this.peerMessageQueues.Where(q => q.Value.sentSessions.Any(s => !s.Value.initiated))) {
				actions.Add(async (lc) => {

					foreach(KeyValuePair<uint, PeerMessageQueue.MessageSendSession> messageSendSession in messageQueue.Value.sentSessions.Where(s => !s.Value.initiated)) {
						// send the messages for processing
						await SendPeerGossipMessageGroup(messageSendSession.Value, messageQueue.Value).ConfigureAwait(false);
					}
				});
			}

			// now the new messages
			foreach(KeyValuePair<Guid, PeerMessageQueue> queuesGroup in this.peerMessageQueues.Where(q => q.Value.outboundMessagesQueue.Any()).ToArray()) {

				actions.Add(async (lc) => {

					this.CheckShouldCancel();

					PeerMessageQueue queue = queuesGroup.Value;

					if(queue.Connection.IsDisposed || !queue.Connection.GeneralSettings.GossipEnabled) {
						// this peer does not want gossip messages at all
						return;
					}

					// lets mark it all as we have a manifest in progress
					var messageSendSession = new PeerMessageQueue.MessageSendSession();
					messageSendSession.Connection = queue.Connection;

					List<IGossipMessageSet> messages = new List<IGossipMessageSet>();

					while(queue.outboundMessagesQueue.TryTake(out IGossipMessageSet message)) {
						messages.Add(message);
					}

					// make sure the peer supports these gossip messages
					messageSendSession.messages.AddRange(messages.Where(m => NodeInfo.DoesPeerTypeSupport(messageSendSession.Connection.NodeInfo, m.MinimumNodeGossipSupport)));

					if(!messageSendSession.messages.Any()) {
						return;
					}

					bool contains = true;

					// make sure we have a unique session Id with the peer
					do {
						messageSendSession.sessionId = GlobalRandom.GetNextUInt();

						contains = queue.sentSessions.ContainsKey(messageSendSession.sessionId);

					} while(contains);

					queue.sentSessions.TryAdd(messageSendSession.sessionId, messageSendSession);

					// send the messages for processing
					await SendPeerGossipMessageGroup(messageSendSession, queue).ConfigureAwait(false);

				});
			}

			LockContext lockContext = null;
			await IndependentActionRunner.RunAsync(lockContext, actions.ToArray()).ConfigureAwait(false);
		}

		protected async Task SendPeerGossipMessageGroup(PeerMessageQueue.MessageSendSession messageSendSession, PeerMessageQueue queue) {
			// first, see if we have any gossip messages. if we do, we will ask the server which one it wants.

			messageSendSession.trigger = this.MessageFactory.CreateMessageGroupManifestWorkflowTriggerSet(this.CorrelationId, messageSendSession.sessionId);

			messageSendSession.sendAttempts += 1;

			NLog.Default.Debug($"Sending message manifest with {messageSendSession.messages.Count} messages to peer {messageSendSession.Connection.ScopedAdjustedIp}");

			// first hash our messages and see if we have any to send out
			// hash only new gossip messages. we dont hash targeted messages, and received forwards are already hashed and there is nothing to do

			foreach(IGossipMessageSet newGossipMessage in messageSendSession.messages.Where(gm => gm.BaseHeader.Hash == 0)) {
				newGossipMessage.BaseHeader.Hash = HashingUtils.Generate_xxHash(newGossipMessage);
			}

			messageSendSession.trigger.Message.messageInfos.AddRange(messageSendSession.messages.Select(gm => new GossipGroupMessageInfo<R> {Hash = gm.BaseHeader.Hash, GossipMessageMetadata = gm.MessageMetadata}));

			void RemoveQueue(bool force = false) {
				if(force || messageSendSession.Connection.IsDisposed) {
					if(this.peerMessageQueues.TryRemove(messageSendSession.Connection.ClientUuid, out _)) {
						this.networkingService.ConnectionStore.RemoveConnection(messageSendSession.Connection);
					}
				}
			}

			try {
				if(messageSendSession.Connection.IsDisposed) {
					throw new ApplicationException();
				}
				await Repeater.RepeatAsync(async () => {

					queue.LatencyStart = DateTimeEx.CurrentTime;
					if(await SendMessage(messageSendSession.Connection, messageSendSession.trigger).ConfigureAwait(false)) {

						messageSendSession.sendAttempts = 1;
						messageSendSession.initiated = true;
						messageSendSession.manifestSentTime = DateTimeEx.CurrentTime;
					} else {
						throw new ApplicationException();
					}
				}).ConfigureAwait(false);

			} catch {

				if(messageSendSession.Connection.IsDisposed || messageSendSession.sendAttempts >= DISCONNECTED_RETRY_ATTEMPS) {
					NLog.Default.Debug($"Client {queue.Connection.ScopedIp} seems to have disconnected and has messages to be sent. we will now be removing it permanently.");
					RemoveQueue(true);
				} else {
					NLog.Default.Debug($"Client {queue.Connection.ScopedIp} seems to have disconnected and has messages to be sent. A retry will be attempted");
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

			/// <summary>
			///     messages that are waiting to be processed and sent out
			/// </summary>
			/// <returns></returns>
			public readonly ConcurrentBag<IGossipMessageSet> outboundMessagesQueue = new ConcurrentBag<IGossipMessageSet>();

			public readonly ConcurrentDictionary<uint, MessageSendSession> sentSessions = new ConcurrentDictionary<uint, MessageSendSession>();

			public PeerConnection Connection;
			public DateTime LatencyStart = DateTimeEx.MinValue;
			
			public class MessageSendSession {

				public readonly List<IGossipMessageSet> messages = new List<IGossipMessageSet>();
				public PeerConnection Connection;

				/// <summary>
				///     true if it was actually sent, otherwise false
				/// </summary>
				public bool initiated;

				/// <summary>
				///     when we sent the manifest, in case we need to timeout
				/// </summary>
				public DateTime? manifestSentTime;

				public int sendAttempts;

				public uint sessionId;

				public int timeoutStrikes;

				public TriggerMessageSet<MessageGroupManifestTrigger<R>, R> trigger;
			}
		}
	}
}