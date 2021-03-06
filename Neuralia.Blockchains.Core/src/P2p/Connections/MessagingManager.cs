using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;

namespace Neuralia.Blockchains.Core.P2p.Connections {

	public interface IMessagingManager<R> : ILoopThread<MessagingManager<R>>, IColoredRoutedTaskHandler
		where R : IRehydrationFactory {
	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class MessagingManager<R> : LoopThread<MessagingManager<R>>, IMessagingManager<R>
		where R : IRehydrationFactory {
		private const int MAX_SECONDS_BEFORE_NEXT_PEER_LIST_REQUEST = 40; //3*60;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT = 20; //1*60;

		/// <summary>
		///     our message rehydrator for messages that belong to no chain
		/// </summary>
		protected readonly R chainlessBlockchainEventsRehydrationFactory;

		protected readonly IClientWorkflowFactory<R> clientWorkflowFactory;

		protected readonly IConnectionStore connectionStore;

		private readonly IDataAccessService dataAccessService;

		protected readonly DataDispatcher dataDispatcher;

		protected readonly IGlobalsService globalsService;

		protected readonly INetworkingService<R> networkingService;

		/// <summary>
		///     The receiver that allows us to act as a task endpoint mailbox
		/// </summary>
		protected readonly ColoredRoutedTaskReceiver RoutedTaskReceiver;

		protected readonly IServerWorkflowFactory<R> serverWorkflowFactory;

		protected readonly ServiceSet<R> serviceSet;

		protected readonly ITimeService timeService;

		protected ClientMessageGroupManifestWorkflow<R> groupManifestWorkflow;

		private DateTime? nextDatabaseClean;

		public MessagingManager(ServiceSet<R> serviceSet) : base(1000) {

			this.globalsService = serviceSet.GlobalsService;
			this.networkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();
			this.connectionStore = this.networkingService.ConnectionStore;
			this.timeService = serviceSet.TimeService;
			this.dataAccessService = serviceSet.DataAccessService;

			this.clientWorkflowFactory = serviceSet.InstantiationService.GetClientWorkflowFactory(serviceSet);
			this.serverWorkflowFactory = serviceSet.InstantiationService.GetServerWorkflowFactory(serviceSet);

			this.serviceSet = serviceSet;

			this.RoutedTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleTask);
			this.RoutedTaskReceiver.TaskReceived += this.RoutedTaskReceiverOnTaskReceived;

			this.dataDispatcher = new DataDispatcher(serviceSet.TimeService, faultyConnection => {
				// just in case, attempt to remove the connection if it was not already
				NLog.Connections.Verbose($"[{nameof(MessagingManager<IRehydrationFactory>)} removing faulty connection {faultyConnection.NodeAddressInfo}.");
				this.networkingService.ConnectionStore.RemoveConnection(faultyConnection);
			});
		}

		/// <summary>
		///     interface method to receive tasks into our mailbox
		/// </summary>
		/// <param name="task"></param>
		public void ReceiveTask(IColoredTask task) {
			this.RoutedTaskReceiver.ReceiveTask(task);
		}

		public override Task Stop() {

			this.groupManifestWorkflow?.Stop();
			this.groupManifestWorkflow = null;

			return base.Stop();
		}

		public override async Task Start() {

			this.groupManifestWorkflow = this.clientWorkflowFactory.CreateMessageGroupManifest();

			this.groupManifestWorkflow.Success += w => {
				return Task.CompletedTask;

			};

			this.groupManifestWorkflow.Error += (e, ex) => {
				// lets make sure it will be attempted again

				return Task.CompletedTask;
			};

			await this.networkingService.WorkflowCoordinator.AddWorkflow(this.groupManifestWorkflow).ConfigureAwait(false);

			await base.Start().ConfigureAwait(false);
		}

		private void RoutedTaskReceiverOnTaskReceived() {
			this.ClearWait();
		}

		/// <summary>
		///     handle any message (task) that we may have recived
		/// </summary>
		/// <param name="task"></param>
		protected virtual async Task HandleTask(IColoredTask task) {
			try {
				if(task is MessageReceivedTask messageReceivedTask) {

					await this.HandleMessageReceived(messageReceivedTask).ConfigureAwait(false);

				} else if(task is ForwardGossipMessageTask forwardGossipMessageTask) {
					await this.HandleForwardGossipMessageTask(forwardGossipMessageTask).ConfigureAwait(false);
				} else if(task is PostNewGossipMessageTask postNewGossipMessageTask) {
					await this.HandlePostNewGossipMessageTask(postNewGossipMessageTask).ConfigureAwait(false);
				}

			} catch(Exception ex) {
				NLog.Messages.Error(ex, "failed to handle task");
			}
		}

		protected virtual async Task CleanMessageCache() {
			try {
				await this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemStorageDirectoryPath(), this.serviceSet).CleanMessageCache().ConfigureAwait(false);
			} catch(Exception ex) {
				NLog.Messages.Error(ex, "failed to clean the message cache.");
			}
		}

		/// <summary>
		///     here we handle the forwarding of a valid gossip message we have received, and will move ahead
		/// </summary>
		/// <param name="forwardGossipMessageTask"></param>
		protected Task HandleForwardGossipMessageTask(ForwardGossipMessageTask forwardGossipMessageTask) {
			// ok, this is a valid message, it went through our hoops. so lets be nice and forward it to whoever will want it
			return this.ForwardValidGossipMessage(forwardGossipMessageTask.gossipMessageSet);
		}

		/// <summary>
		///     This method allows us to post a brand new gossip message to our peers.
		/// </summary>
		/// <remarks>USE WITH CAUTION!! our peers can blacklist us if we abuse it.</remarks>
		/// <param name="postNewGossipMessageTask"></param>
		protected async Task HandlePostNewGossipMessageTask(PostNewGossipMessageTask sendGossipMessageTask) {
			if(sendGossipMessageTask == null) {
				throw new ApplicationException("Cannot send null gossip message");
			}

			// ok, lets send it out. first we prepare it

			// lets hash it if it was not already
			if(sendGossipMessageTask.gossipMessageSet.BaseHeader.Hash == 0) {
				HashingUtils.SetHashGossipMessageSet(sendGossipMessageTask.gossipMessageSet);
			}

			// now we add it to our database as already received, we dont need to get it back from other peers. we set it as valid, since this is our own message
			await this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemFilesDirectoryPath(), this.serviceSet).AddMessageToCache(sendGossipMessageTask.gossipMessageSet.BaseHeader.Hash, true, true).ConfigureAwait(false);

			// ok, now we can forward it to our peers
			await this.ForwardValidGossipMessage(sendGossipMessageTask.gossipMessageSet).ConfigureAwait(false);
		}

		/// <summary>
		///     this method ensures the verification of a received gossip message and if necessary, its forwarding to other peers.
		///     we also determine if we should process it, or ignore it
		/// </summary>
		/// <param name="gossipHeader"></param>
		/// <param name="task"></param>
		/// <returns>result true if we should process the </returns>
		protected async Task<(bool messageInCache, bool messageValid, IGossipMessageSet gossipMessageSet)> ProcessReceivedGossipMessage(GossipHeader gossipHeader, MessageReceivedTask task) {
			// ok, a gossip message, these are special, we must forward them if they are new

			bool returnMessageToSender = false;

			R chainFactory = ((NetworkingService<R>) this.networkingService).ChainRehydrationFactories[gossipHeader.chainId];

			if(chainFactory == null) {
				throw new ApplicationException("Failed to obtain the chain's registered rehydration factory. we can not inspect the chain specific gossip message and validate it");
			}

			IGossipMessageSet gossipMessageSet = this.networkingService.MessageFactory.RehydrateGossipMessage(task.data, gossipHeader, chainFactory);

			if(gossipMessageSet == null) {
				throw new ApplicationException("Failed to rehydrate the chain gossip message");
			}

			// first step, validate the hash

			if(!HashingUtils.ValidateGossipMessageSetHash(gossipMessageSet)) {
				throw new ApplicationException("Invalid gossip message hash");
			}

			// ok at this point, the hash is valid, lets keep going

			// lets take in the potential optionsBase in the message
			if(gossipMessageSet.BaseHeader.NetworkOptions.HasOption((byte) GossipHeader.GossipNetworkMessageOptions.ReturnMeMessage)) {
				// ok, they want us to resend the message to them, if its valid. we do this so that when we send a new transaction to our peers, we will get the message from them
				// if they consider it to be valid. we can know that they agreed about our message
				returnMessageToSender = true;

				// for sure, we remove this option, since WE dont pass it on other peers.
				gossipMessageSet.BaseHeader.NetworkOptions.RemoveOption((byte) GossipHeader.GossipNetworkMessageOptions.ReturnMeMessage);

				// also remove it from the deserialized version, since odds are high we will forward it.
				// we can do this as the network optionsBase is the only byte that is not part of the message hash. hence, it is designed to be changed.
				if(gossipMessageSet.HasDeserializedData) {
					// its always the first byte

					ByteExclusiveOption options = gossipMessageSet.DeserializedData[0];
					options.RemoveOption((byte) GossipHeader.GossipNetworkMessageOptions.ReturnMeMessage);
					gossipMessageSet.DeserializedData[0] = options;
				}
			}

			// next lets confirm we have not processed this message before, and record the gossip message so we dont process it again
			(bool messageInCache, bool messageValid) = await this.dataAccessService.CreateMessageRegistryDal(this.globalsService.GetSystemFilesDirectoryPath(), this.serviceSet).CheckRecordMessageInCache(gossipMessageSet.BaseHeader.Hash, task, returnMessageToSender).ConfigureAwait(false);

			if(messageInCache) {

			}

			if(!messageInCache && messageValid) {
				// if we get here, its because we had already processed it before, and it was valid. lets forward it to any peer that may not have received it since
				await this.ForwardValidGossipMessage(gossipMessageSet).ConfigureAwait(false);
			}

			return (messageInCache, messageValid, gossipMessageSet);

		}

		/// <summary>
		///     this method will forward a gossip message to any connected peer that our cache indicates has never received it and
		///     update the cache to reflect so
		/// </summary>
		private Task ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet) {

			return this.groupManifestWorkflow.ForwardValidGossipMessage(gossipMessageSet);
		}

		protected virtual IRoutingHeader RehydrateHeader(MessageReceivedTask task) {

			try {
				IRoutingHeader header = this.networkingService.MessageFactory.RehydrateMessageHeader(task.data);

				if(header == null) {
					throw new ApplicationException("Null message header");
				}

				return header;
			} catch(Exception ex) {
				NLog.Messages.Error(ex, "Fail to rehydrate message set header.");

				throw;
			}
		}

		/// <summary>
		///     lets handle the message and redirect it where it needs to go
		/// </summary>
		/// <param name="task"></param>
		protected virtual async Task HandleMessageReceived(MessageReceivedTask task) {

			// lets see what we just received
			IRoutingHeader header = this.RehydrateHeader(task);

			// set the client Scope of the client who sent us this message
			header.ClientId = task.Connection.ClientUuid;

			// first, for gossip messages, we must forward them to other peers, so lets do that
			if(header is GossipHeader gossipHeader) {

				if(!task.Connection.IsConfirmed) {
					throw new ApplicationException("An unconfirmed connection cannot send us a gossip message");
				}

				if(header.ChainId == BlockchainTypes.Instance.None) {
					// we do not allow null chain gossip messages, so lets end here
					throw new ApplicationException("A null chain gossip message is not allowed");
				}

				if(!header.IsWorkflowTrigger) {
					// we do not allow null chain gossip messages, so lets end here
					throw new ApplicationException("A gossip message is not marked as a workflow trigger, which is not allowed");
				}

				bool messageInCache = false;
				IGossipMessageSet gossipMessageSet = null;
				(messageInCache, _, gossipMessageSet) = await this.ProcessReceivedGossipMessage(gossipHeader, task).ConfigureAwait(false);

				if(messageInCache) {
					return; // we do not process any further
				}

				// now the message will be sent to the chains for validation, and if valid, will come back for a forward
				((NetworkingService<R>) this.networkingService).RouteNetworkGossipMessage(gossipMessageSet, task.Connection);

				return;

			}

			// if we get any further, then they are targeted messages
			if(header.ChainId == BlockchainTypes.Instance.None) {
				// this is a null chain, this is our message

				if(header is TargettedHeader targettedHeader) {
					// this is a targeted header, its meant only for us

					ITargettedMessageSet<R> messageSet = this.networkingService.MessageFactory.Rehydrate(task.data, targettedHeader, this.chainlessBlockchainEventsRehydrationFactory);

					WorkflowTracker<IWorkflow<R>, R> workflowTracker = new WorkflowTracker<IWorkflow<R>, R>(task.Connection, messageSet.Header.WorkflowCorrelationId, messageSet.Header.WorkflowSessionId, messageSet.Header.OriginatorId, this.networkingService.ConnectionStore.MyClientUuid, this.networkingService.WorkflowCoordinator);

					if(messageSet.Header.IsWorkflowTrigger && messageSet is ITriggerMessageSet<R> triggeMessageSet) {
						// route the message
						Type messageType = triggeMessageSet.BaseMessage.GetType();

						if(task.acceptedTriggers.Any(t => t.IsInstanceOfType(triggeMessageSet.BaseMessage) != t.IsAssignableFrom(messageType))) {
							throw new ApplicationException("Jetbrains refactoring error, the suggested fix is not equal to the previous");
						}

						if(task.acceptedTriggers.Any(t => t.IsInstanceOfType(triggeMessageSet.BaseMessage))) {

							// let's check if a workflow already exists for this trigger
							if(!workflowTracker.WorkflowExists()) {
								// create a new workflow
								ITargettedNetworkingWorkflow<R> workflow = (ITargettedNetworkingWorkflow<R>) this.serverWorkflowFactory.CreateResponseWorkflow(triggeMessageSet, task.Connection);

								if(!task.Connection.IsConfirmed && !(workflow is IServerHandshakeWorkflow)) {
									throw new ApplicationException("An unconfirmed connection must initiate a handshake");
								}

								await this.networkingService.WorkflowCoordinator.AddWorkflow(workflow).ConfigureAwait(false);
							}
						} else {
							if(triggeMessageSet.BaseMessage is WorkflowTriggerMessage<R>) {
								// this means we did not pass the trigger filter above, it could be an evil trigger and we default
								throw new ApplicationException("An invalid trigger was sent");
							}
						}
					} else {

						if(messageSet.BaseMessage is WorkflowTriggerMessage<R>) {
							//messageSet.BaseMessage?.Dispose();

							throw new ApplicationException("We have a cognitive dissonance here. The trigger flag is not set, but the message type is a workflow trigger");
						}

						if(messageSet.Header.IsWorkflowTrigger) {
							//messageSet.BaseMessage?.Dispose();

							throw new ApplicationException("We have a cognitive dissonance here. The trigger flag is set, but the message type is not a workflow trigger");
						}

						// forward the message to the right Verified workflow
						// this method will ensure we get the right workflow id for our connection

						//----------------------------------------------------

						if(workflowTracker.GetActiveWorkflow() is ITargettedNetworkingWorkflow<R> workflow) {

							if(!task.Connection.IsConfirmed && !(workflow is IHandshakeWorkflow)) {
								throw new ApplicationException("An unconfirmed connection must initiate a handshake");
							}

							workflow.ReceiveNetworkMessage(messageSet);
						} else {
							//messageSet.BaseMessage?.Dispose();
							NLog.Messages.Verbose($"The message references a workflow correlation ID '{messageSet.Header.WorkflowCorrelationId}' and session ID '{messageSet.Header.WorkflowSessionId}' which does not exist");
						}
					}

				}
			} else {
				if(!task.Connection.IsConfirmed) {

					throw new ApplicationException("An unconfirmed connection cannot send us a chain scoped targeted message");
				}

				// this message is targeted at a specific chain, so we route it over there
				// first confirm that we support this chain
				((NetworkingService<R>) this.networkingService).RouteNetworkMessage(header, task.data.Branch(), task.Connection);
			}

		}

		protected override async Task ProcessLoop(LockContext lockContext) {
			try {
				this.CheckShouldCancel();

				// first thing, lets check if we have any tasks received to process
				await this.CheckTasks().ConfigureAwait(false);

				this.CheckShouldCancel();

				if(this.ShouldAct(ref this.nextDatabaseClean)) {
					this.CheckShouldCancel();

					// lets keep our database clean
					await this.CleanMessageCache().ConfigureAwait(false);

					this.CheckShouldCancel();

					// ok, its time to act
					int secondsToWait = 5 * 60; // default next action time in seconds. we can play on this

					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextDatabaseClean = DateTimeEx.CurrentTime.AddSeconds(secondsToWait);
				}
			} 
			catch(OutOfMemoryException oex) {
				// thats bad, lets clear everything

				this.groupManifestWorkflow = null;
				
				GC.Collect();
					
				throw;
			}
			catch(OperationCanceledException) {
				throw;
			} catch(Exception ex) {
				NLog.Messages.Error(ex, "Failed to process connections");
			}
		}

		/// <summary>
		///     Check if we received any tasks and process them
		/// </summary>
		/// <param name="lockContext"></param>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns></returns>
		protected Task<List<Guid>> CheckTasks() {
			return this.RoutedTaskReceiver.CheckTasks(async () => {
				// check this every loop, for responsiveness
				this.CheckShouldCancel();
			});
		}

		protected override async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);

			if(!this.connectionStore.GetIsNetworkAvailable && !GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.LocalhostOnly) {
				throw new NetworkInformationException();
			}
		}

		public class MessageReceivedTask : ColoredTask {
			public readonly List<Type> acceptedTriggers;
			public readonly PeerConnection Connection;
			public readonly SafeArrayHandle data = SafeArrayHandle.Create();

			public MessageReceivedTask(SafeArrayHandle data, PeerConnection connection, List<Type> acceptedTriggers) {
				this.data = data.Branch();
				this.Connection = connection;
				this.acceptedTriggers = acceptedTriggers;

			}

			public MessageReceivedTask(SafeArrayHandle data, PeerConnection connection) : this(data, connection, new List<Type>(new[] {typeof(WorkflowTriggerMessage<R>)})) {

			}

			public MessageReceivedTask(MessageReceivedTask task, PeerConnection connection) : this(task.data, connection, task.acceptedTriggers.ToList()) {

			}
		}

		public class ForwardGossipMessageTask : ColoredTask {
			public readonly PeerConnection Connection;
			public readonly IGossipMessageSet gossipMessageSet;

			public ForwardGossipMessageTask(IGossipMessageSet gossipMessageSet, PeerConnection connection) {
				this.gossipMessageSet = gossipMessageSet;
				this.Connection = connection;
			}
		}

		public class PostNewGossipMessageTask : ColoredTask {

			public readonly IGossipMessageSet gossipMessageSet;

			public PostNewGossipMessageTask(IGossipMessageSet gossipMessageSet) {
				this.gossipMessageSet = gossipMessageSet;
			}
		}
	}
}