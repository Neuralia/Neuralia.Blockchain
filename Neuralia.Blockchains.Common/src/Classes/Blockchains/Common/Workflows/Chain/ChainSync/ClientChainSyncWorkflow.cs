using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Digest;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Exceptions;
using Newtonsoft.Json;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync {
	public interface IClientChainSyncWorkflow : IClientWorkflow<IBlockchainEventsRehydrationFactory> {
	}

	/// <summary>
	///     this is probably our most complicated workflow of all. Perform syncronization of our chain between peers
	/// </summary>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	public abstract partial class ClientChainSyncWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, FINISH_SYNC, REQUEST_BLOCK, REQUEST_DIGEST, SEND_BLOCK, SEND_DIGEST, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES> : ClientChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IClientChainSyncWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply
		where FINISH_SYNC : FinishSync
		where REQUEST_BLOCK : ClientRequestBlock
		where REQUEST_DIGEST : ClientRequestDigest
		where SEND_BLOCK : ServerSendBlock
		where SEND_DIGEST : ServerSendDigest
		where REQUEST_BLOCK_INFO : ClientRequestBlockInfo
		where SEND_BLOCK_INFO : ServerSendBlockInfo
		where REQUEST_DIGEST_FILE : ClientRequestDigestFile
		where SEND_DIGEST_FILE : ServerSendDigestFile
		where REQUEST_DIGEST_INFO : ClientRequestDigestInfo
		where SEND_DIGEST_INFO : ServerSendDigestInfo
		where REQUEST_BLOCK_SLICE_HASHES : ClientRequestBlockSliceHashes
		where SEND_BLOCK_SLICE_HASHES : ServerRequestBlockSliceHashes {

		public enum ResultsState {
			None,
			OK,
			NoSyncingConnections,
			Error
		}

		private const string DOWNLOAD_TEMP_DIR_NAME = "files";

		private const int MAXIMUM_UNIFIED_BLOCK_SIZE = 100_000;
		private const int MINIMUM_USEFUL_SLICE_SIZE = 1000;

		/// <summary>
		///     This defines how many block context history to keep. anything over will be thrown out.
		/// </summary>
		private const int MAXIMUM_CONTEXT_HISTORY = 10;

		/// <summary>
		///     How many times do we wish to try
		/// </summary>
		protected const int MAX_RETRY_ATTEMPTS = 3;

		/// <summary>
		///     How many seconds do we use as absolute sliding timeout for a single block fetch
		/// </summary>
		protected const int FETCH_SINGLE_BLOCK_TIMEOUT = 20;

		/// <summary>
		///     the amount of time we give a peer to return a single slice
		/// </summary>
		protected const int PEER_SLICE_TIMEOUT = 10;

		/// <summary>
		///     How many times do we expect to retry if blocks fail
		/// </summary>
		protected const int FETCH_SINGLE_BLOCK_RETRY_COUNT = 3;

		/// <summary>
		///     Amount of seconds we give for a peer to reply to a sync request before we timeout
		/// </summary>
		protected const int PEER_SYNC_ATTEMPT_TIMEOUT = 15;

		/// <summary>
		///     HOw long to wait for messages before we give up and continue
		/// </summary>

		//TODO: ensure this is a right value. 10 seconds maybe?
		protected const int WAIT_MESSAGES_TIME = 10;

		protected readonly AppSettingsBase.BlockSavingModes blockchainSavingMode;

		/// <summary>
		///     This is the history of block contexts, which we can use for analytics
		/// </summary>
		protected readonly Queue<SingleEntryContext<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, PeerBlockSpecs, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, BlockFilesetSyncManifest.BlockSyncingDataSlice>> BlockContexts = new Queue<SingleEntryContext<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, PeerBlockSpecs, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, BlockFilesetSyncManifest.BlockSyncingDataSlice>>();

		protected readonly IChainSyncMessageFactory chainSyncMessageFactory;

		protected readonly BlockchainType chainType;

		protected readonly IFileSystem fileSystem;

		// keep the history to establish statistics
		private readonly Queue<SyncHistory> insertionHistory = new Queue<SyncHistory>();

		private bool canFetchNewPeers;

		private Task<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> newPeerTask;

		/// <summary>
		///     all should be the same, so we keep a sample
		/// </summary>
		protected IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> trigger;

		public ClientChainSyncWorkflow(BlockchainType chainType, CENTRAL_COORDINATOR centralCoordinator, IFileSystem fileSystem) : base(centralCoordinator) {

			this.fileSystem = fileSystem;
			this.chainType = chainType;

			// this is a special workflow, and we make sure we are generous in waiting times, to accomodate everything that can happen
			this.hibernateTimeoutSpan = TimeSpan.FromMinutes(1);

			this.chainSyncMessageFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.GetChainSyncMessageFactory();
			this.blockchainSavingMode = this.ChainConfiguration.BlockSavingMode;

			// allow only one at a time
			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;
			this.IsLongRunning = true;

			this.CentralCoordinator.ShutdownStarting += () => {
				// thats it, we request a hard cancel

				try {
					if(!this.CancelTokenSource.IsCancellationRequested) {
						this.CancelTokenSource.Cancel();
					}
				} catch {
					// we can just eat it
				}
			};

		}

		protected bool UseDigest => BlockchainUtilities.UsesDigests(this.blockchainSavingMode);
		protected bool UseAllBlocks => BlockchainUtilities.UsesAllBlocks(this.blockchainSavingMode);
		protected bool UsePartialBlocks => BlockchainUtilities.UsesPartialBlocks(this.blockchainSavingMode);

		protected IChainStateProvider ChainStateProvider => this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;

		protected ChainConfigurations ChainConfiguration => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

		protected int MinimumSyncPeerCount => this.ChainConfiguration.MinimumSyncPeerCount;

		private bool IsBusy { get; set; }

		private void AddBlocHistoryEntry(long blockId) {
			this.insertionHistory.Enqueue(new SyncHistory {blockId = blockId, timestamp = DateTime.Now});

			while(this.insertionHistory.Count > 100) {
				this.insertionHistory.Dequeue();
			}
		}

		private string CalculateSyncingRate() {

			if(this.insertionHistory.Any()) {
				var entries = this.insertionHistory.ToArray();

				SyncHistory min = entries.MinBy(e => e.timestamp).First();
				SyncHistory max = entries.MaxBy(e => e.timestamp).First();

				TimeSpan timeRange = max.timestamp - min.timestamp;
				long blockRange = max.blockId - min.blockId;

				long remaining = this.ChainStateProvider.PublicBlockHeight - this.ChainStateProvider.DownloadBlockHeight;

				decimal multiplier = 0;

				if(blockRange > 0) {
					multiplier = (decimal) remaining / blockRange;
				}

				timeRange = TimeSpan.FromSeconds((int) ((decimal) timeRange.TotalSeconds * multiplier));

				string tail = "";

				if((timeRange.Days == 0) && (timeRange.Hours == 0) && (timeRange.Minutes == 0)) {
					return $"{timeRange.Seconds} second{(timeRange.Seconds == 1 ? "" : "s")}";
				}

				return $"{timeRange.Days} day{(timeRange.Days == 1 ? "" : "s")}, {timeRange.Hours} hour{(timeRange.Hours == 1 ? "" : "s")}, {timeRange.Minutes} minute{(timeRange.Minutes == 1 ? "" : "s")}";

			}

			return "";
		}

		protected override void PerformWork() {
			this.CheckShouldCancel();

			if(GlobalSettings.ApplicationSettings.DisableP2p) {
				// we have no p2p connection, we simply can not do anything. 
				Log.Warning("P2p is disabled. can not sync.");

				return;
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused) {
				// network is paused, obvous we wont sync much
				return;
			}
			
			// very first thing, lets spend some time computing the hourly hashes for our dates, if we can

			if(this.centralCoordinator.IsChainSynchronized) {
				// we are ok for now
				return;
			}

			this.CentralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;

			// This will be our active connections to work on
			var connections = new ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>();

			try {
				//TODO: should this be more granular?  perhaps it should be set at each loop...
				this.IsBusy = true;

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.BlockchainSyncStarted);

				// this will be our parallel fetch new peers task
				PeerBlockSpecs nextBlockSpecs = null;
				PeerBlockSpecs previousBlockSpecs = null;

				int attempts = 0;

				
				bool syncGenesis = this.ChainStateProvider.DownloadBlockHeight == 0;
				
				if(!syncGenesis && (this.ChainStateProvider.DiskBlockHeight == 0)) {
					// let's confirm 
					ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(1);

					if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
						this.ChainStateProvider.DownloadBlockHeight = 0;
						syncGenesis = true;
					}

					if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.InProgress) {

						this.ClearBlockSyncManifest(1);
						this.ChainStateProvider.DownloadBlockHeight = 0;
						syncGenesis = true;
					}
				}

				bool syncDigest = this.UseDigest;

				bool synched = false;
				if(syncGenesis) {

					ResultsState result = this.RunBlockSyncingAction(connectionsSet => {
						this.SynchronizeGenesisBlock(connectionsSet);

						this.AddBlocHistoryEntry(1);

						this.UpdateDownloadBlockHeight(1);
						syncGenesis = false;
						synched = true;

						return ResultsState.OK;
					}, 3, connections);

					if(result != ResultsState.OK || this.ChainStateProvider.DiskBlockHeight != 1) {

						if(!connections.HasSyncingConnections) {
							Log.Verbose("Failed to sync genesis block, we had no syncing connections");
						} else {
							Log.Warning("Failed to sync genesis block");
						}

						return;
					}
					
					Log.Information("Genesis block has been synced successfully");
				}

				// then its always the digest if applicable
				if(syncDigest) {

					
					ResultsState result = this.RunBlockSyncingAction(connectionsSet => {
						//if we need to get a digest, we do now
						this.SynchronizeDigest(connectionsSet);

						syncDigest = false;
						synched = true;

						return ResultsState.OK;
					}, 2, connections);

					if(result != ResultsState.OK) {
						if(!connections.HasSyncingConnections) {
							Log.Verbose("Failed to sync digest, we had no syncing connections");
						} else {
							Log.Warning("Failed to sync digest");
						}

						return;
					}
					
					syncDigest = false;
				}

				if(!syncGenesis && !syncDigest) {
					// ok, we are good to go with the rest of the syncing
					this.LaunchMainBlockSync(connections);
					synched = true;
				}

				// a good time to also request a wallet sync if none was requested
				if(synched) {
					this.RequestWalletSync();
				}
				
				this.CheckShouldCancel();

				if(this.newPeerTask != null) {
					this.canFetchNewPeers = true;

					if(this.newPeerTask?.Wait(TimeSpan.FromSeconds(10)) ?? false) {
						this.newPeerTask?.Dispose();
					}
				}
				
				this.CheckShouldCancel();
				
				// ok, we just synced so we can update our marker
				this.ChainStateProvider.LastSync = DateTime.Now;

				Log.Information($"Synchronization for '{BlockchainTypes.GetBlockchainTypeName(this.chainType)}' chain is completed.");

			} finally {
				this.IsBusy = false;

				try {
					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.BlockchainSyncEnded);
					this.CentralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;
				} catch(Exception ex) {
					// do nothing, we tried but failed
				}

				try {
					if(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
						this.centralCoordinator.TriggerBlockchainSyncedEvent();
					}
				} catch {

				}

				try {
					var activeConnections = connections.GetAllConnections();

					// we are done or failed, but in any case lets try to tell the peers that we can stop the sync now
					if(activeConnections.Any()) {
						var closeMessage = (BlockchainTargettedMessageSet<FINISH_SYNC>) this.chainSyncMessageFactory.CreateSyncWorkflowFinishSyncSet(this.trigger.BaseHeader);

						closeMessage.Message.Reason = FinishSync.FinishReason.Ok;

						try {
							// lets be nice, lets inform them that we will close the connection for this workflow
							this.SendMessageToPeers(closeMessage, activeConnections, connections);
						} catch(Exception ex) {
							Log.Error(ex, "Failed to close all peer connections but workflow is over.");
						}
					}
				} catch(Exception ex) {
					// do nothing, we tried but failed, we will stop disgracefully
					Log.Error(ex, "Failed to alert our peers and stop the workflow gracefully. we must do a disgraceful stop");
				}
			}
		}

		/// <summary>
		///     Ensure that we dont stop during a sync step if a shutdown has been requested
		/// </summary>
		/// <param name="beacons"></param>
		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {

			// ok, if this happens while we are syncing, we ask for a grace period until we are ready to clean exit
			if(this.IsBusy) {
				beacons.Add(new TaskFactory().StartNew(() => {

					while(true) {
						if(!this.IsBusy) {
							// we are ready to go
							break;
						}

						// we have to wait a little more
						Thread.Sleep(500);
					}
				}));
			}
		}

		/// <summary>
		///     Obtain the results of our fetch peer service and merge them with ours
		/// </summary>
		/// <param name="newPeerTask"></param>
		protected virtual void HandleNewPeerTaskResult(ref Task<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> newPeerTask, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {
			lock(this.locker) {
				if((newPeerTask != null) && newPeerTask.IsCompleted) {

					if(newPeerTask.IsFaulted) {
						Log.Error(newPeerTask.WithAllExceptions().Exception, "Failed to fetch new syncing peers...");
					} else {
						// all good, merge our connections
						connections.Merge(newPeerTask.Result);
					}

					newPeerTask.Dispose();
					newPeerTask = null;
				}
			}
		}

		/// <summary>
		///     A special method, meant to run in it's own thread to fetch new connections in our peers while we query the blocks
		///     in parallel. If we run out, it wlil ask the connection manager to fetch more
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		protected virtual ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> FetchNewPeers(object state) {

			var castedState = ((ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> potentialConnections, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> allConnections)) state;

			var potentialConnections = castedState.potentialConnections;
			var allConnections = castedState.allConnections;

			// lets acquire a new auto event on the workflow
			AutoResetEvent autoEvent = this.RegisterNewAutoEvent();

			this.CheckShouldCancel();

			try {

				int receivedRepliesCount = 0;

				if(potentialConnections.HasSyncingConnections) {
					Log.Verbose("We should not have any syncing peers here. we operate on new connections only. Removing them.");
					potentialConnections.Set(potentialConnections.GetNonSyncingConnections());
				}

				var activeConnections = potentialConnections.GetActiveConnections();

				int validRepliesCount = 0;

				if(activeConnections.Any()) {
					// now we check which ones are willing to sync, are synced and ready to go with valid dates
					this.trigger = (IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER>) this.chainSyncMessageFactory.CreateSyncWorkflowTriggerSet(this.CorrelationId);

					// prepare our trigger, lets tell a bit to the peers about us and our chain state
					this.trigger.Message.ChainInception = this.ChainStateProvider.ChainInception;
					this.trigger.Message.DiskBlockHeight = this.ChainStateProvider.DiskBlockHeight;
					this.trigger.Message.BlockSavingMode = this.blockchainSavingMode;
					this.trigger.Message.DigestHeight = this.ChainStateProvider.DigestHeight;

					Log.Verbose($"We will now attempt to connect and sync with {activeConnections.Count} new peer(s).");

					// invite them to join
					int peerSendCount = this.SendMessageToPeers(this.trigger, activeConnections, potentialConnections);

					Log.Verbose($"Turns out we sent connection requests to {peerSendCount} new peer(s).");

					TimeSpan waitTime = TimeSpan.FromSeconds(PEER_SYNC_ATTEMPT_TIMEOUT);
					DateTime absoluteTimeout = DateTime.Now + waitTime;

					var failedReceivedReplies = new List<(SERVER_TRIGGER_REPLY message, PeerConnection connection)>();
					var succeededReceivedReplies = new List<(SERVER_TRIGGER_REPLY message, PeerConnection connection)>();

					// loop until we get replies from everyone, or timeout
					while((receivedRepliesCount < peerSendCount) && (DateTime.Now < absoluteTimeout)) {
						// and now we send it to each peer, and see their response. we use our own thread autoevent to wait

						var validReplies = new List<(ResponseValidationResults success, SERVER_TRIGGER_REPLY message, PeerConnection connection)>();

						try {
							validReplies = this.WaitForAnyPeerReplies<SERVER_TRIGGER_REPLY>(this.trigger, (peerReply, peerConnection) => {

								if(peerReply.Message.Status != ServerTriggerReply.SyncHandshakeStatuses.Ok) {
									// ok, this peer can't sync with us.
									return ResponseValidationResults.Invalid;
								}

								// keep the trigger messages for later use
								peerConnection.Trigger = this.trigger;
								peerConnection.TriggerResponse = peerReply;
								peerConnection.ReportedDiskBlockHeight = peerReply.Message.DiskBlockHeight;
								peerConnection.ReportedDigestHeight = peerReply.Message.DigestHeight;

								// although a good peer, it might be behind in its chain. We still keep the connection as who knows, it might catch up faster than us and be able to share later.
								if(peerReply.Message.ChainInception < this.ChainStateProvider.ChainInception) {
									return ResponseValidationResults.Invalid;
								}

								long ourDiskBlockHeight = this.ChainStateProvider.DiskBlockHeight;
								int ourDigestHeight = this.ChainStateProvider.DigestHeight;

								// if we and they care bout digests, lets check
								if(this.UseDigest && BlockchainUtilities.UsesDigests(peerReply.Message.BlockSavingMode)) {
									// if their block height is lower than ours, we reject them if they can't also share a digest
									if((peerReply.Message.DiskBlockHeight < ourDiskBlockHeight) && (peerReply.Message.DigestHeight <= ourDigestHeight)) {
										return ResponseValidationResults.NoData;
									}
								} else {
									// ensure their block height is higher than ours
									if(peerReply.Message.DiskBlockHeight < ourDiskBlockHeight) {
										return ResponseValidationResults.NoData;
									}
								}

								// in this case, peer is valid if it has a more advanced blockchain than us and can share it back right away.
								// if we get here, its a valid message and we are syncing with it
								peerConnection.Syncing = true;

								return ResponseValidationResults.Valid;
							}, potentialConnections);
						} catch(Exception ex) {

						}

						failedReceivedReplies.AddRange(validReplies.Where(r => r.success != ResponseValidationResults.Valid).Select(r => (r.message, r.connection)));
						succeededReceivedReplies.AddRange(validReplies.Where(r => r.success == ResponseValidationResults.Valid).Select(r => (r.message, r.connection)));

						receivedRepliesCount += validReplies.Count;

						var succeeded = succeededReceivedReplies.Select(c => c.connection.ClientUuid).ToList();

						// make the connection valid right away so they can continue if required
						allConnections.AddValidConnections(activeConnections.Where(c => succeeded.Contains(c.PeerConnection.ClientUuid)).ToList());
					}

					validRepliesCount = succeededReceivedReplies.Count;

					// build the list of peers that did not reply at all
					var receivedIds = failedReceivedReplies.Select(c => c.connection.ClientUuid).ToList();
					receivedIds.AddRange(succeededReceivedReplies.Select(c => c.connection.ClientUuid));

					var noReplyConnections = activeConnections.Where(c => !receivedIds.Contains(c.PeerConnection.ClientUuid)).ToList();

					if(failedReceivedReplies.Any() || noReplyConnections.Any()) {
						//TODO: log the peers that did not reply
						// some peers did not reply in time. lets log them so we dont contact them too often or ever again...
						//failedReplies
					}

					if(failedReceivedReplies.Any()) {
						// these guys answered, but could not help us. lets ignore them for a while
						foreach((SERVER_TRIGGER_REPLY message, PeerConnection connection) entry in failedReceivedReplies) {
							potentialConnections.AddRejectedConnection(entry.connection, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.CannotHelp);
						}
					}

					if(noReplyConnections.Any()) {
						// these guys never answered. lets ignore them for a while
						foreach(var entry in noReplyConnections) {
							potentialConnections.AddRejectedConnection(entry.PeerConnection, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoAnswer);
						}
					}

					if(!succeededReceivedReplies.Any() && (potentialConnections.SyncingConnectionsCount <= 2)) {
						// ok, we are in a bad position. we have a low syncing count and we got no new ones. lets command the connections manager to add more connections.
						var disposableConnections = noReplyConnections.Select(c => c.PeerConnection).ToList();
						disposableConnections.AddRange(potentialConnections.GetRejectedConnections());
						disposableConnections = disposableConnections.Distinct().ToList();

						ConnectionsManager.RequestMoreConnectionsTask connectionRequest = new ConnectionsManager.RequestMoreConnectionsTask(disposableConnections);

						this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ReceiveConnectionsManagerTask(connectionRequest);
					}

					Log.Verbose($"In the end, we got {receivedRepliesCount} replies and connected to {validRepliesCount} new peer(s) for Synchronization.");
				}

				Log.Information($"Workflow to check for new Synchronization peers is completed. Added {validRepliesCount} new peers.");
			} catch(Exception e) {
				Log.Error(e, "Exception occured");

				throw;
			} finally {
				// we are done, lets be nice and clear it
				this.ClearAutoEvent(autoEvent);
			}

			return potentialConnections;
		}

		/// <summary>
		///     Get the concensus on a group of (hopefully) similar or dissimilar values.
		/// </summary>
		/// <param name="consensusResult"></param>
		/// <param name="variableName"></param>
		/// <exception cref="WorkflowException"></exception>
		protected void TestConsensus(ConsensusUtilities.ConsensusType consensusResult, string variableName) {
			if((consensusResult == ConsensusUtilities.ConsensusType.Split) || (consensusResult == ConsensusUtilities.ConsensusType.Undefined)) {
				throw new WorkflowException($"We received fragmented results from our existing peers for the {variableName}. This is a serious issue and we will stop here");
			}
		}

		protected virtual (Dictionary<Guid, ENTRY_DETAILS> results, ResultsState state) FetchPeerInfo<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, DATA_SLICE>(FetchInfoParameter<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, DATA_SLICE> parameters)
			where INFO_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoRequest<KEY>
			where INFO_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoResponse<CHANNEL_INFO_SET, T, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET : ChannelsInfoSet<SLICE_KEY, T>
			where T : DataSliceSize, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where SINGLE_ENTRY_CONTEXT : SingleEntryContext<SLICE_KEY, SYNC_MANIFEST, ENTRY_DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {

			this.CheckShouldCancel();

			this.EnsureSyncingPeers(parameters.singleEntryContext.Connections);

			var syncingConnections = parameters.selectUsefulConnections(parameters.singleEntryContext.Connections);

			int syncingCount = syncingConnections.Count;

			if(syncingCount == 0) {
				return (new Dictionary<Guid, ENTRY_DETAILS>(), ResultsState.NoSyncingConnections);
			}

			if(syncingCount < this.MinimumSyncPeerCount) {
				// this is WAAY too risky. we can not trust a single peer unless we have configured it to allow it.
				Log.Verbose($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");

				Thread.Sleep(100);

				throw new NoSyncingConnectionsException($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");
			}

			// ok its big, let's determine the slices for each peer and spread it among our peers
			if(syncingCount > 3) {
				// ok, we have a bit too much, we will limit this to 10 peers, its more than enough
				syncingCount = 3;
			}

			var requestInfos = new List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>();

			int index = 1;

			for(int i = 0; i < syncingCount; i++) {
				var requestMessage = parameters.generateInfoRequestMessage();

				requestMessage.Message.Id = parameters.id;

				requestMessage.Message.RequestAttempt = parameters.singleEntryContext.blockFetchAttemptCounter;

				requestInfos.Add(new PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>(index, requestMessage));
				index++;
			}

			// prepare the connection messages

			TimeSpan timeout = TimeSpan.FromSeconds(FETCH_SINGLE_BLOCK_TIMEOUT);
			DateTime timeoutTime = DateTime.Now + timeout;

			int retryAttempt = 1;

			var missingInfos = requestInfos.ToList();

			// we dont want to reuse the same peers used for slices and for block connection. to ensure a certain level of trust. so we track the slice peers and exclude them for connection queries
			var succeededPeers = new List<Guid>();
			var peersWithNoNextEntry = new List<Guid>();

			var peerInfos = new Dictionary<Guid, ENTRY_DETAILS>();

			int totalValidReplies = 0;

			// now attempt to fill the slices a couple times
			while(true) {
				this.CheckShouldCancel();
				this.EnsureSyncingPeers(parameters.singleEntryContext.Connections);

				// refresh connections
				syncingConnections = parameters.selectUsefulConnections(parameters.singleEntryContext.Connections);

				if(DateTime.Now > timeoutTime) {
					throw new WorkflowException("Failed to sync, incapable of getting missing block slices");
				}

				if(retryAttempt > FETCH_SINGLE_BLOCK_RETRY_COUNT) {
					throw new WorkflowException("We attempted 3 times to query data slices and failed. incapable of getting missing block slices.");
				}

				// make sure we still have connections to work with for our next try
				if(!parameters.singleEntryContext.Connections.HasSyncingConnections && syncingConnections.Any()) {
					return (new Dictionary<Guid, ENTRY_DETAILS>(), ResultsState.NoSyncingConnections);
				}

				// loop until we have it all

				index = 0;

				var validPeers = syncingConnections.Where(c => !succeededPeers.Contains(c.PeerConnection.ClientUuid)).ToList();

				validPeers.Shuffle();
				validPeers = validPeers.Take(syncingCount).ToList();

				if(!validPeers.Any()) {
					throw new NoSyncingConnectionsException();
				}

				var dispatchedInfos = new List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>();

				// and now the connection requests. in this case, we never reuse the same peer which has already replied. we absolutely need a new voice.
				foreach(var requestInfo in missingInfos) {

					if(index == validPeers.Count) {
						break;
					}

					// reassign the slices to peers we know work. If we have less peers than slices, then we just go with it, anf loop back again later
					requestInfo.connection = validPeers[index++];

					// tell the server that we are retrying. This will inform them to be nice with us and our issues...
					requestInfo.requestMessage.Message.RequestAttempt += 1;

					dispatchedInfos.Add(requestInfo);
					Log.Verbose($"Peer IP {requestInfo.connection.PeerConnection.ScoppedAdjustedIp} is getting a connection request.");
				}

				// and now we send it to each peer
				int peerSendCount = this.SendMessagesToPeers(dispatchedInfos, parameters.singleEntryContext.Connections);

				// log the peers we sent this to
				foreach(var requestInfo in dispatchedInfos) {
					parameters.singleEntryContext.AddSentPeerAttempt(requestInfo.connection.PeerConnection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
				}

				// and wait for their response
				var validInfoReplies = this.WaitForAllPeerReplies<INFO_RESPONSE>((IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER>) requestInfos.First().connection.Trigger, peerSendCount, (peerReply, peerConnection) => parameters.validNextInfoFunc(peerReply, missingInfos, peerInfos, peersWithNoNextEntry, peerConnection), parameters.singleEntryContext.Connections);

				foreach((ResponseValidationResults success, INFO_RESPONSE message, PeerConnection connection) reply in validInfoReplies) {
					if(reply.success == ResponseValidationResults.Valid) {
						// make sure we store it, because we dont want to reuse it
						succeededPeers.Add(reply.connection.ClientUuid);
					} else {
						if(reply.success == ResponseValidationResults.Invalid) {
							parameters.singleEntryContext.AddFaultyPeerAttempt(reply.connection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
						}
					}
				}

				this.CheckShouldCancel();

				// check again, if we have any missing reponses among the missing lot
				missingInfos = missingInfos.Where(s => s.responseMessage == null).ToList();

				// make sure to remove the connections of peers who once again returned us nothing
				foreach(var missingInfo in missingInfos) {
					parameters.singleEntryContext.AddFaultyPeerAttempt(missingInfo.connection.PeerConnection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
				}

				totalValidReplies += validInfoReplies.Count;

				if((totalValidReplies >= 2) || (syncingCount == 1)) {
					// fiou, we got enough, we can take it
					break;
				}

				// we loop again until we have it all
				retryAttempt++;
			}

			this.CheckShouldCancel();

			foreach(Guid outdated in peersWithNoNextEntry) {
				//TODO: send a close connection message
				parameters.singleEntryContext.Connections.AddRejectedConnection(outdated, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoNextBlock);
			}

			return (peerInfos, ResultsState.OK);
		}

		protected virtual (Dictionary<Guid, ENTRY_DETAILS> results, ResultsState state) FetchPeerSliceHashes<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE, SYNC_MANIFEST, DATA_SLICE>(FetchSliceHashesParameter<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE> parameters)
			where INFO_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesRequest<KEY>
			where INFO_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesResponse<KEY>
			where CHANNEL_INFO_SET : ChannelsInfoSet<SLICE_KEY, T>
			where T : DataSliceSize, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {

			this.CheckShouldCancel();

			this.EnsureSyncingPeers(parameters.Connections);

			var syncingConnections = parameters.selectUsefulConnections(parameters.Connections);

			int syncingCount = syncingConnections.Count;

			if(syncingCount == 0) {
				return (new Dictionary<Guid, ENTRY_DETAILS>(), ResultsState.NoSyncingConnections);
			}

			if(syncingCount < this.MinimumSyncPeerCount) {
				// this is WAAY too risky. we can not trust a single peer unless we have configured it to allow it.
				Log.Verbose($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");

				throw new WorkflowException($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");
			}

			// ok its big, let's determine the slices for each peer and spread it among our peers
			if(syncingCount > 3) {
				// ok, we have a bit too much, we will limit this to 10 peers, its more than enough
				syncingCount = 3;
			}

			var requestInfos = new List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>();

			int index = 1;

			for(int i = 0; i < syncingCount; i++) {
				var requestMessage = parameters.generateInfoRequestMessage();

				requestMessage.Message.Id = parameters.id;

				requestInfos.Add(new PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>(index, requestMessage));
				index++;
			}

			// prepare the connection messages

			TimeSpan timeout = TimeSpan.FromSeconds(FETCH_SINGLE_BLOCK_TIMEOUT);
			DateTime timeoutTime = DateTime.Now + timeout;

			int retryAttempt = 1;

			var missingInfos = requestInfos.ToList();

			// we dont want to reuse the same peers used for slices and for block connection. to ensure a certain level of trust. so we track the slice peers and exclude them for connection queries
			var succeededPeers = new List<Guid>();
			var peersWithNoNextEntry = new List<Guid>();

			var peerInfos = new Dictionary<Guid, ENTRY_DETAILS>();

			int totalValidReplies = 0;

			// now attempt to fill the slices a couple times
			while(true) {
				this.CheckShouldCancel();
				this.EnsureSyncingPeers(parameters.Connections);

				// refresh connections
				syncingConnections = parameters.selectUsefulConnections(parameters.Connections);

				if(DateTime.Now > timeoutTime) {
					throw new WorkflowException("Failed to sync, incapable of getting missing block slices");
				}

				if(retryAttempt > FETCH_SINGLE_BLOCK_RETRY_COUNT) {
					throw new WorkflowException("We attempted 3 times to query data slices and failed. incapable of getting missing block slices.");
				}

				// make sure we still have connections to work with for our next try
				if(!parameters.Connections.HasSyncingConnections && syncingConnections.Any()) {
					return (new Dictionary<Guid, ENTRY_DETAILS>(), ResultsState.NoSyncingConnections);
				}

				// loop until we have it all

				index = 0;

				var validPeers = syncingConnections.Where(c => !succeededPeers.Contains(c.PeerConnection.ClientUuid)).ToList();

				validPeers.Shuffle();
				validPeers = validPeers.Take(syncingCount).ToList();

				if(!validPeers.Any()) {
					throw new NoSyncingConnectionsException();
				}

				var dispatchedInfos = new List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>();

				// and now the connection requests. in this case, we never reuse the same peer which has already replied. we absolutely need a new voice.
				foreach(var requestInfo in missingInfos) {

					if(index == validPeers.Count) {
						break;
					}

					// reassign the slices to peers we know work. If we have less peers than slices, then we just go with it, anf loop back again later
					requestInfo.connection = validPeers[index++];

					// tell the server that we are retrying. This will inform them to be nice with us and our issues...
					requestInfo.requestMessage.Message.RequestAttempt += 1;

					dispatchedInfos.Add(requestInfo);
					Log.Verbose($"Peer IP {requestInfo.connection.PeerConnection.ScoppedAdjustedIp} is getting a connection request.");
				}

				// and now we send it to each peer
				int peerSendCount = this.SendMessagesToPeers(dispatchedInfos, parameters.Connections);

				// log the peers we sent this to
				foreach(var requestInfo in dispatchedInfos) {
					//parameters.AddSentPeerAttempt(requestInfo.connection.PeerConnection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
				}

				// and wait for their response
				var validInfoReplies = this.WaitForAllPeerReplies<INFO_RESPONSE>((IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER>) requestInfos.First().connection.Trigger, peerSendCount, (peerReply, peerConnection) => parameters.validNextInfoFunc(peerReply, missingInfos, peerInfos, peersWithNoNextEntry, peerConnection), parameters.Connections);

				foreach((ResponseValidationResults success, INFO_RESPONSE message, PeerConnection connection) reply in validInfoReplies) {
					if(reply.success == ResponseValidationResults.Valid) {
						// make sure we store it, because we dont want to reuse it
						succeededPeers.Add(reply.connection.ClientUuid);
					} else {
						if(reply.success == ResponseValidationResults.Invalid) {
							//parameters.singleEntryContext.AddFaultyPeerAttempt(reply.connection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
						}
					}
				}

				this.CheckShouldCancel();

				// check again, if we have any missing reponses among the missing lot
				missingInfos = missingInfos.Where(s => s.responseMessage == null).ToList();

				// make sure to remove the connections of peers who once again returned us nothing
				foreach(var missingInfo in missingInfos) {
					//parameters.singleEntryContext.AddFaultyPeerAttempt(missingInfo.connection.PeerConnection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
				}

				totalValidReplies += validInfoReplies.Count;

				if((totalValidReplies >= 2) || (syncingCount == 1)) {
					// fiou, we got enough, we can take it
					break;
				}

				// we loop again until we have it all
				retryAttempt++;
			}

			this.CheckShouldCancel();

			foreach(Guid outdated in peersWithNoNextEntry) {
				//TODO: send a close connection message
				parameters.Connections.AddRejectedConnection(outdated, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoNextBlock);
			}

			return (peerInfos, ResultsState.OK);
		}

		protected virtual (Dictionary<Guid, ENTRY_DETAILS> results, ResultsState state) FetchPeerData<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, ENTRY_DETAILS, DATA_REQUEST, DATA_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, COMPLETED_CONTENTS, DATA_SLICE>(FetchDataParameter<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, ENTRY_DETAILS, DATA_REQUEST, DATA_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, COMPLETED_CONTENTS, DATA_SLICE> parameters)
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<CHANNEL_INFO_SET_REQUEST, T_REQUEST, KEY, SLICE_KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET_REQUEST : ChannelsInfoSet<SLICE_KEY, T_REQUEST>, new()
			where CHANNEL_INFO_SET_RESPONSE : ChannelsInfoSet<SLICE_KEY, T_RESPONSE>, new()
			where T_REQUEST : DataSliceInfo, new()
			where T_RESPONSE : DataSlice, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where SINGLE_ENTRY_CONTEXT : SingleEntryContext<SLICE_KEY, SYNC_MANIFEST, ENTRY_DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where COMPLETED_CONTENTS : class
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {

			this.CheckShouldCancel();

			var sliceInfos = new List<PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE>>();

			this.EnsureSyncingPeers(parameters.singleEntryContext.Connections);

			var syncingConnections = parameters.selectUsefulConnections(parameters.singleEntryContext.Connections);
			int syncingCount = syncingConnections.Count;

			if(syncingCount == 0) {
				return (new Dictionary<Guid, ENTRY_DETAILS>(), ResultsState.NoSyncingConnections);
			}

			if(syncingCount < this.MinimumSyncPeerCount) {
				// this is WAAY too risky. we can not trust a single peer unless we have configured it to allow it.
				Log.Verbose($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");

				throw new WorkflowException($"We do not have enough peers to sync. We have {syncingCount} connections and the minimum required is {this.MinimumSyncPeerCount}");
			}

			// ok its big, let's determine the slices for each peer and spread it among our peers
			if(syncingCount > 10) {
				// ok, we have a bit too much, we will limit this to 10 peers, its more than enough
				syncingCount = 10;
			}

			// slice the entire block size. Here we spread the header and contents over the peers. We try to equalize it and split it evenly between them

			// first we request the header data
			var remainingSlices = parameters.singleEntryContext.syncManifest.RemainingSlices;

			for(int i = 0; i < remainingSlices.Count; i++) {

				var requestMessage = parameters.generateMultiSliceDataRequestMessage();

				foreach(var channel in remainingSlices[i].fileSlices) {

					T_REQUEST request = new T_REQUEST {Offset = channel.Value.Offset, Length = channel.Value.Length};

					if(requestMessage.Message.SlicesInfo.SlicesInfo.ContainsKey(channel.Key)) {
						requestMessage.Message.SlicesInfo.SlicesInfo[channel.Key] = request;
					} else {
						requestMessage.Message.SlicesInfo.SlicesInfo.Add(channel.Key, request);
					}
				}

				requestMessage.Message.SlicesInfo.FileId = remainingSlices[i].sliceId;
				requestMessage.Message.RequestAttempt = parameters.singleEntryContext.blockFetchAttemptCounter;

				sliceInfos.Add(new PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE>(i, requestMessage));
			}

			// we dont want to reuse the same peers used for slices and for block connection. to ensure a certain level of trust. so we track the slice peers and exclude them for connection queries
			var nextBlockPeerSpecs = new Dictionary<Guid, ENTRY_DETAILS>();

			// this is the list of peers that have a block now, but tell us they have no next blocks. we will remove them for the next block
			var peersWithNoNextEntry = new List<Guid>();

			this.PerformSlicesDownload(syncingConnections, parameters, sliceInfos, nextBlockPeerSpecs, peersWithNoNextEntry);

			this.CheckShouldCancel();

			// set the slice details so we know who contributed what.
			foreach(var slice in sliceInfos) {

				parameters.singleEntryContext.syncManifest.Slices[slice.index].ClientGuid = slice.PeerId;
				parameters.singleEntryContext.syncManifest.Slices[slice.index].Hash = slice.Hash;
			}

			parameters.updateSyncManifest();

			SYNC_MANIFEST syncManifest = parameters.singleEntryContext.syncManifest;

			if(syncManifest.IsComplete) {

				COMPLETED_CONTENTS completedData = parameters.prepareCompletedData?.Invoke();

				parameters.clearManifest();

				bool valid = parameters.downloadCompleted?.Invoke(completedData) ?? true;

				if(!valid) {
					throw new ApplicationException("Failed to confirm data, download was invalid");
				}
			}

			// ok, now we remove the connections that wont be available for the next block only if we have not reached the end of the blockchain
			if(this.ChainStateProvider.DiskBlockHeight != this.ChainStateProvider.PublicBlockHeight) {
				foreach(Guid outdated in peersWithNoNextEntry) {
					//TODO: send a close connection message
					parameters.singleEntryContext.Connections.AddRejectedConnection(outdated, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoNextBlock);
				}
			}

			return (nextBlockPeerSpecs, ResultsState.OK);
		}

		protected void PerformSlicesDownload<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, ENTRY_DETAILS, DATA_REQUEST, DATA_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, COMPLETED_CONTENTS, DATA_SLICE>(List<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> syncingConnections, FetchDataParameter<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, ENTRY_DETAILS, DATA_REQUEST, DATA_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, COMPLETED_CONTENTS, DATA_SLICE> parameters, List<PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE>> slices, Dictionary<Guid, ENTRY_DETAILS> nextBlockPeerSpecs, List<Guid> peersWithNoNextEntry)
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<CHANNEL_INFO_SET_REQUEST, T_REQUEST, KEY, SLICE_KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET_REQUEST : ChannelsInfoSet<SLICE_KEY, T_REQUEST>, new()
			where CHANNEL_INFO_SET_RESPONSE : ChannelsInfoSet<SLICE_KEY, T_RESPONSE>, new()
			where T_REQUEST : DataSliceInfo, new()
			where T_RESPONSE : DataSlice, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where SINGLE_ENTRY_CONTEXT : SingleEntryContext<SLICE_KEY, SYNC_MANIFEST, ENTRY_DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where COMPLETED_CONTENTS : class
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {

			SYNC_MANIFEST syncManifest = parameters.singleEntryContext.syncManifest;

			TimeSpan timeoutSlider = TimeSpan.FromSeconds(FETCH_SINGLE_BLOCK_TIMEOUT);

			DateTime UpdateTimeout() {
				return DateTime.Now + timeoutSlider;
			}

			DateTime absoluteTimeoutTime = UpdateTimeout();

			var peerSlicesContexts = new Dictionary<Guid, PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE>>();
			var slicePeersContexts = slices.ToDictionary(s => s.index, s => new SlicePeersContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> {SliceIndex = s.index, SliceInfo = s});

			// get the trigger shared by all
			var trigger = syncingConnections.First().Trigger;

			// the total number of peers we sent the messages too. including the ones that have not replied yet or timedout
			int dispatchedPeerSendCount = 0;

			// now attempt to fill the slices a couple times
			while(true) {

				this.CheckShouldCancel();
				this.EnsureSyncingPeers(parameters.singleEntryContext.Connections);

				if(DateTime.Now > absoluteTimeoutTime) {
					throw new WorkflowException($"Failed to sync block {syncManifest.Id}. Incapable of getting missing block slices. Timed out.");
				}

				// make sure we still have connections to work with for our next try
				if(!parameters.singleEntryContext.Connections.HasSyncingConnections) {
					throw new NoSyncingConnectionsException();
				}

				// loop until we have it all

				// ok, now we assign the slices to peers and send them out

				var validPeers = syncingConnections.ToDictionary(c => c.PeerConnection.ClientUuid, c => c);
				var validPeerUUids = validPeers.Keys.ToList();

				// update the peers
				// new ones
				var newPeers = validPeerUUids.Where(k => !peerSlicesContexts.Keys.Contains(k)).ToList();
				var gonePeers = peerSlicesContexts.Keys.Where(p => !validPeerUUids.Contains(p)).ToList();
				var remainingPeers = peerSlicesContexts.Keys.Where(p => validPeerUUids.Contains(p)).ToList();

				// update those that are gone
				foreach(Guid gonePeer in gonePeers) {
					peerSlicesContexts[gonePeer].Connected = false;
				}

				//add new ones
				foreach(Guid newPeer in newPeers) {
					peerSlicesContexts.Add(newPeer, new PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> {PeerId = newPeer, Connected = true});
				}

				//update the ones that stayed or returned
				foreach(Guid existingPeer in remainingPeers) {
					peerSlicesContexts[existingPeer].Connected = true;
				}

				bool firstRun = true;
				int retryAttempt = 1;

				while(!syncManifest.IsComplete && (DateTime.Now <= absoluteTimeoutTime)) {

					this.CheckShouldCancel();

					// any time out slices needs to be restored
					foreach(var timedoutPeer in peerSlicesContexts.Values.Where(p => p.IsTimeout)) {

						// this is a suspecious peer
						timedoutPeer.SetTimedout();
					}

					List<PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE>> usablePeers = null;

					if(slicePeersContexts.Values.Any(s => s.Ready) && peerSlicesContexts.Values.Any(p => p.CanTakeSliceNoTimeout)) {
						usablePeers = peerSlicesContexts.Values.Where(p => p.CanTakeSliceNoTimeout).Shuffle().ToList();
					} else if(slicePeersContexts.Values.Any(s => s.Ready) && peerSlicesContexts.Values.Any(p => p.CanTakeSlice && (p.Strikes < 3))) {
						// ok, we will take the ones that timed out too, give them another chance
						usablePeers = peerSlicesContexts.Values.Where(p => p.CanTakeSlice && (p.Strikes < 3)).Shuffle().ToList();
					}

					if(slicePeersContexts.Values.Any(s => s.Ready) && (usablePeers?.Any() ?? false)) {

						var availableSlices = slicePeersContexts.Values.Where(p => p.Ready).Take(usablePeers.Count).ToList();

						// now limit to slices too
						usablePeers = usablePeers.Take(availableSlices.Count).Shuffle().ToList();

						// assign a slice to an available peer
						for(int i = 0; i < usablePeers.Count; i++) {
							var validPeer = usablePeers[i];
							var slice = availableSlices[i];

							slice.SliceInfo.connection = validPeers[validPeer.PeerId];
							slice.SetCurrentPeer(validPeer);

							if(firstRun) {
								// lets ask them for the next block specs
								parameters.prepareFirstRunRequestMessage?.Invoke(slice.SliceInfo.requestMessage.Message);
							}
						}

						this.CheckShouldCancel();

						dispatchedPeerSendCount += this.SendMessagesToPeers(availableSlices.Select(s => s.SliceInfo).ToList(), parameters.singleEntryContext.Connections);

						// update the sliding absolute timeout
						absoluteTimeoutTime = UpdateTimeout();
					}

					if(dispatchedPeerSendCount <= 0) {
						// we have nothing going on at all
						Thread.Sleep(100);
						dispatchedPeerSendCount = 0;

						continue;
					}

					var validPeerReplies = new List<(ResponseValidationResults success, DATA_RESPONSE message, PeerConnection connection)>();

					try {
						validPeerReplies = this.WaitForAnyPeerReplies<DATA_RESPONSE>((IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER>) trigger, (peerReply, peerConnection) => parameters.validSlicesFunc(peerReply, nextBlockPeerSpecs, slices, peersWithNoNextEntry, peerConnection), parameters.singleEntryContext.Connections);

					} catch(TimeoutException tex) {
						Log.Error(tex, "Timeout waiting for block replies");

						// keep going, we will truly timeout if we need to
						continue;
					}

					this.CheckShouldCancel();

					if(validPeerReplies.Any()) {

						// ok, we got replies!
						bool anyValidReplies = false;

						foreach((ResponseValidationResults success, DATA_RESPONSE message, PeerConnection connection) response in validPeerReplies) {

							// this is important, if its not our current block, it could be a vestige from the past. we ignore it
							if(response.message.Id.Equals(parameters.id) == false) {
								continue;
							}

							var sliceEntry = slicePeersContexts.Values.SingleOrDefault(s => s.SliceInfo.requestMessage.Message.SlicesInfo.FileId == response.message.Slices.FileId);

							if(sliceEntry == null) {
								//TODO: what to do here? perhaps we should log the peer
								continue;
							}

							anyValidReplies = true;

							// ok, valid message, lets mark the response
							dispatchedPeerSendCount--;

							if(response.success == ResponseValidationResults.Valid) {

								parameters.processReturnMessage?.Invoke(response.message, response.connection.ClientUuid, nextBlockPeerSpecs);

								parameters.writeDataSlice(response.message.Slices, response.message);

								// make this peer available again for another slice and wrap this slice up with it's hash. it is done
								sliceEntry.SetCompleted(HashingUtils.GenerateBlockDataSliceHash(response.message.Slices.SlicesInfo.Select(s => s.Value.Data).ToList()));
							} else {
								if(response.success == ResponseValidationResults.Invalid) {
									parameters.singleEntryContext.AddFaultyPeerAttempt(response.connection, parameters.singleEntryContext.blockFetchAttemptCounter, retryAttempt);
								}

								// lets return it to the "to be downloaded" pool
								sliceEntry.ResetSlice();
							}
						}

						if(anyValidReplies) {

							// update our manifest
							parameters.updateSyncManifest();

							// update the sliding absolute timeout
							absoluteTimeoutTime = UpdateTimeout();
						}
					}

					retryAttempt++;
					firstRun = false;
				}

				if(syncManifest.IsComplete) {
					// we did it, it is complete, lets save the new generated file
					return;
				}
			}
		}

		protected virtual int SendMessagesToPeers<KEY, DATA_REQUEST, DATA_RESPONSE>(IEnumerable<PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE>> peerSlices, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connectionSet)
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncRequest<KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncResponse<KEY> {

			int peerSendCount = 0;

			foreach(var slice in peerSlices.ToArray()) {

				IByteArray triggerData = slice.requestMessage.Dehydrate(); // dehydrate it only once

				try {
					if(!this.SendBytes(slice.connection.PeerConnection, triggerData)) {
						Log.Verbose($"Connection with peer  {slice.connection.PeerConnection.ScoppedAdjustedIp} was terminated");

						throw new SendMessageException();
					}

					peerSendCount += 1;
				} catch(Exception ex) {
					// remove this connection from our active ones
					//TODO: send a close connection message
					connectionSet.AddRejectedConnection(slice.connection.PeerConnection, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoConnection);
				}
			}

			this.EnsureSyncingPeers(connectionSet);

			return peerSendCount;
		}

		/// <summary>
		///     get a single message or more and continue. we do not wait for more than one.
		/// </summary>
		/// <param name="trigger"></param>
		/// <param name="replyValidFunction"></param>
		/// <param name="connections"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		protected virtual List<(ResponseValidationResults success, T message, PeerConnection connection)> WaitForAnyPeerReplies<T>(IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> trigger, Func<ITargettedMessageSet<T, IBlockchainEventsRehydrationFactory>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> replyValidFunction, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections)
			where T : class, INetworkMessage<IBlockchainEventsRehydrationFactory> {
			return this.WaitForAllPeerReplies(null, trigger, -1, replyValidFunction, connections);
		}

		/// <summary>
		///     At this point, we wait for our many peers to reply, and se sum up the replies. We loop until we either
		///     get a reply from each, or time outs from others
		/// </summary>
		protected virtual List<(ResponseValidationResults success, T message, PeerConnection connection)> WaitForAllPeerReplies<T>(IBlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> trigger, int peerSendCount, Func<ITargettedMessageSet<T, IBlockchainEventsRehydrationFactory>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> replyValidFunction, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections)
			where T : class, INetworkMessage<IBlockchainEventsRehydrationFactory> {
			return this.WaitForAllPeerReplies(null, trigger, peerSendCount, replyValidFunction, connections);
		}

		protected virtual List<(ResponseValidationResults success, T message, PeerConnection connection)> WaitForAllPeerReplies<T>(Func<TimeSpan?, int, IEnumerable<ITargettedMessageSet<T, IBlockchainEventsRehydrationFactory>>> waitMessagesFunc, IBlockchainTargettedMessageSet<CHAIN_SYNC_TRIGGER> trigger, int peerSendCount, Func<ITargettedMessageSet<T, IBlockchainEventsRehydrationFactory>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> replyValidFunction, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections)
			where T : class, INetworkMessage<IBlockchainEventsRehydrationFactory> {

			var replies = new List<(ResponseValidationResults success, T reply, PeerConnection connection)>();

			if(waitMessagesFunc == null) {
				waitMessagesFunc = (timeout, peerSendCountWait) => this.WaitNetworkMessages<T, IBlockchainEventsRehydrationFactory>(timeout, peerSendCountWait);
			}

			try {
				// now we wait for peer replies. The complexity here is that we handle multiple peers asyncronously.
				// so we wait for a certain number (or timeouts) and then launch threads with them

				// at this point, we will rebuild our active connection list from the valid replies we get

				// here we will wait for a combined maximum of x seconds
				//TODO: restore this to a proper value. 1 minute should be good? should be a dynmic parameter
				TimeSpan timeout = TimeSpan.FromSeconds(WAIT_MESSAGES_TIME);
				DateTime maxWaitTime = DateTime.Now + timeout;

				//TODO; check all of this
				try {
					var messages = waitMessagesFunc(timeout, peerSendCount);

					// these are some peers that replied to our request
					foreach(var replyMessage in messages) {

						// we got a reply from this guy, so we are now ON!!
						var currentConnection = connections.GetActiveConnections().SingleOrDefault(c => c.PeerConnection.ClientUuid == replyMessage.BaseHeader.ClientId);

						ResponseValidationResults peerValid = ResponseValidationResults.NoData;

						if(currentConnection != null) {
							peerValid = replyValidFunction(replyMessage, currentConnection);

							if(peerValid != ResponseValidationResults.Valid) {

								// too bad, we can't use it, we remove it
								if(replyMessage.Message is SERVER_TRIGGER_REPLY responseTrigger) {
									if(responseTrigger.Status != ServerTriggerReply.SyncHandshakeStatuses.Ok) {
										// if we have a non ok status from the server here
									}
								}

								if(peerValid == ResponseValidationResults.Invalid) {
									connections.AddRejectedConnection(currentConnection.PeerConnection, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.InvalidResponse);
								}
							}

							replies.Add((peerValid, replyMessage.Message, currentConnection.PeerConnection));
						} else {
							throw new WorkflowException("Failed to obtain peer connection");
						}
					}

				} catch(ThreadTimeoutException tte) {
					// first, figure out and remove those who timed out
					var repliedPeerIds = replies.Select(p => p.connection.ClientUuid).ToList();

					// now remove the connections that did not reply
					foreach(Guid peer in repliedPeerIds) {
						connections.AddRejectedConnection(peer, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoAnswer);
					}

					// ok, the wait has timeout, but have we received at least some replies from peers?
					if(!replies.Any()) {
						throw new WorkflowException("Failed to sync, no peer replied");
					}

					// ok, we got some replies, so lets remove the timed out peers and continue

				} catch(Exception ex) {
					// first, figure out and remove those who timed out
					var repliedPeerIds = replies.Select(p => p.connection.ClientUuid).ToList();

					// now remove the connections that did not reply
					foreach(Guid peer in repliedPeerIds) {
						connections.AddRejectedConnection(peer, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoAnswer);
					}
				}
			} catch(Exception e) {
				Log.Error(e, "Error occured while waiting for messages");

				throw;

				//TODO: if error is serious enough to stop the whole process, then we return an exception. it will kill the whole sync though
			}

			// ok, we got our replies 
			return replies;
		}

		/// <summary>
		///     Send a message to some peers
		/// </summary>
		/// <param name="message"></param>
		/// <param name="connections"></param>
		/// <returns>The connections the data was successfully sent to</returns>
		protected virtual int SendMessageToPeers(ITargettedMessageSet message, List<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> connections, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connectionSet) {
			IByteArray triggerData = message.Dehydrate(); // dehydrate it only once

			int peerSendCount = 0;
			ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>[] currentConnections = null;

			lock(this.locker) {
				currentConnections = connections.ToArray();
			}

			// here we truly send it to all active peers in our connection set
			foreach(var connection in currentConnections) {

				try {
					if(!this.SendBytes(connection.PeerConnection, triggerData)) {
						Log.Verbose($"Connection with peer  {connection.PeerConnection.ScoppedAdjustedIp} was terminated");

						throw new WorkflowException();
					}

					peerSendCount += 1;
				} catch(Exception ex) {
					// remove this connection from our active ones
					//TODO: send a close connection message
					connectionSet.AddRejectedConnection(connection.PeerConnection, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.RejectedConnection.RejectionReason.NoConnection);
				}
			}

			// make sure we still have active peers
			if(!connectionSet.HasActiveConnections) {
				// we have no active syncing connections left
				//TODO: what do we do here?
			}

			return peerSendCount;
		}

		protected void EnsureSyncingPeers(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connectionSet) {
			if(!connectionSet.HasSyncingConnections) {
				throw new NoSyncingConnectionsException();
			}
		}

		/// <summary>
		///     lets reset the manifest bac to basic, something went wrong
		/// </summary>
		/// <param name="syncManifest"></param>
		/// <typeparam name="FILE_KEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		protected void ResetSyncManifest<FILE_KEY, T, DATA_SLICE>(T syncManifest, string path)
			where T : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY>, new() {

			foreach(DATA_SLICE slice in syncManifest.Slices) {
				slice.Downloaded = false;
			}

			this.UpdateSyncManifest<FILE_KEY, T, DATA_SLICE>(syncManifest, path);
		}

		protected T LoadSyncManifest<FILE_KEY, T, DATA_SLICE>(string path)
			where T : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY>, new() {

			if(!this.fileSystem.File.Exists(path)) {
				return null;
			}

			string json = this.fileSystem.File.ReadAllText(path);

			if(string.IsNullOrWhiteSpace(json)) {
				return null;
			}

			JsonSerializerSettings settings = JsonUtils.CreateSerializerSettings();
			settings.Formatting = Formatting.Indented;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			return JsonConvert.DeserializeObject<T>(json, settings);
		}

		protected IByteArray LoadSyncManifestFile<FILE_KEY, T, DATA_SLICE>(T filesetSyncManifest, FILE_KEY key, string path)
			where T : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY>, new() {

			string dirName = this.GetDownloadTempDirName(path);

			string filename = Path.Combine(dirName, filesetSyncManifest.FileKeyToString(key));

			if(!this.fileSystem.File.Exists(filename)) {
				throw new ApplicationException($"Sync manifest channel file named {filename} does not exist. it must have been cleared previously.");
			}

			return (ByteArray) this.fileSystem.File.ReadAllBytes(filename);

		}

		protected void CreateSyncManifest<FILE_KEY, T, DATA_SLICE>(T filesetSyncManifest, string path)
			where T : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY> {

			if(filesetSyncManifest == null) {
				return;
			}

			try {

				this.UpdateSyncManifest<FILE_KEY, T, DATA_SLICE>(filesetSyncManifest, path);

				string dirName = this.GetDownloadTempDirName(path);

				FileExtensions.EnsureDirectoryStructure(dirName, this.fileSystem);

				// and now create the download placeholders
				foreach(var file in filesetSyncManifest.Files) {

					string filepath = Path.Combine(dirName, filesetSyncManifest.FileKeyToString(file.Key));

					if(this.fileSystem.File.Exists(filepath) && (this.fileSystem.FileInfo.FromFileName(filepath).Length != file.Value.Length)) {
						this.fileSystem.File.Delete(filepath);
					}

					if(!this.fileSystem.File.Exists(filepath)) {
						using(Stream fs = this.fileSystem.FileStream.Create(filepath, FileMode.Create, FileAccess.Write, FileShare.None)) {
							fs.SetLength(file.Value.Length);
						}
					}
				}
			} catch(Exception ex) {
				Log.Error("error: ", ex);

				throw;
			}
		}

		/// <summary>
		///     This is where actually write the chain of bytes received into the temp files.
		/// </summary>
		/// <param name="filesetSyncManifest"></param>
		/// <param name="sliceData"></param>
		/// <param name="path"></param>
		/// <typeparam name="CHANNEL_INFO_SET"></typeparam>
		/// <typeparam name="FILE_KEY"></typeparam>
		/// <typeparam name="SYNC_MANIFEST"></typeparam>
		/// <typeparam name="SLICE_TYPE"></typeparam>
		/// <exception cref="WorkflowException"></exception>
		protected void WriteSyncSlice<CHANNEL_INFO_SET, FILE_KEY, SYNC_MANIFEST, SLICE_TYPE, DATA_SLICE>(SYNC_MANIFEST filesetSyncManifest, CHANNEL_INFO_SET sliceData, string path)
			where CHANNEL_INFO_SET : ChannelsInfoSet<FILE_KEY, SLICE_TYPE>
			where SLICE_TYPE : DataSlice, new()
			where SYNC_MANIFEST : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY> {

			DATA_SLICE manifestSlice = filesetSyncManifest.Slices.Single(s => s.sliceId == sliceData.FileId);
			manifestSlice.Downloaded = true;

			string dirName = this.GetDownloadTempDirName(path);

			foreach(var fileSlice in sliceData.SlicesInfo) {

				DataSliceInfo sliceInfo = manifestSlice.fileSlices[fileSlice.Key];

				if((fileSlice.Value.Offset != sliceInfo.Offset) || (fileSlice.Value.Length != sliceInfo.Length)) {
					//TODO: what to do?
					throw new WorkflowException("Invalid data returned");
				}

				string filePath = Path.Combine(dirName, filesetSyncManifest.FileKeyToString(fileSlice.Key));

				if(!this.fileSystem.File.Exists(filePath)) {
					this.CreateSyncManifest<FILE_KEY, SYNC_MANIFEST, DATA_SLICE>(filesetSyncManifest, path);
				}

				using(Stream fs = this.fileSystem.FileStream.Create(filePath, FileMode.Open, FileAccess.Write, FileShare.None)) {
					fs.Seek(fileSlice.Value.Offset, SeekOrigin.Begin);
#if (NETSTANDARD2_0)
					fs.Write(fileSlice.Value.Data.ToExactByteArray(), 0, fileSlice.Value.Data.Length);
#elif (NETCOREAPP2_2)
					fs.Write(fileSlice.Value.Data.Span);
#else
	throw new NotImplementedException();
#endif

				}
			}
		}

		protected void UpdateSyncManifest<FILE_KEY, T, DATA_SLICE>(T filesetSyncManifest, string path)
			where T : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY> {

			if(filesetSyncManifest == null) {
				return;
			}

			JsonSerializerSettings settings = JsonUtils.CreateSerializerSettings();
			settings.Formatting = Formatting.Indented;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			string json = JsonConvert.SerializeObject(filesetSyncManifest, settings);

			FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(path), this.fileSystem);

			this.fileSystem.File.WriteAllText(path, json);
		}

		protected void ClearSyncManifest(string path) {

			// clear it
			if(this.fileSystem.File.Exists(path)) {
				this.fileSystem.File.Delete(path);
			}

			string dirName = this.GetDownloadTempDirName(path);

			if(this.fileSystem.Directory.Exists(dirName)) {
				this.fileSystem.Directory.Delete(dirName, true);
			}
		}

		/// <summary>
		///     Generate the slice structure of the files.
		/// </summary>
		/// <param name="singleEntryContext"></param>
		protected void GenerateSyncManifestStructure<SYNC_MANIFEST, FILE_KEY, DATA_SLICE>(SYNC_MANIFEST syncManifest)
			where SYNC_MANIFEST : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY>, new() {

			// and now the slices

			DATA_SLICE slice = new DATA_SLICE();
			syncManifest.Slices.Add(slice);

			long remainingSliceSize = MAXIMUM_UNIFIED_BLOCK_SIZE;
			int fileCount = syncManifest.Files.Count;
			var fileEntries = syncManifest.Files.ToArray();

			//TODO: improve this logic
			for(int i = 0; i < fileCount; i++) {
				var file = fileEntries[i];
				long offset = 0;
				long remainingFileBytes = file.Value.Length;

				// if the remains are too smal and its not the end of the road, lets split
				if(remainingSliceSize < MINIMUM_USEFUL_SLICE_SIZE) {
					// if we get here, we still have some work to do, but the remaining space is too small. make a new slice
					slice = new DATA_SLICE();
					remainingSliceSize = MAXIMUM_UNIFIED_BLOCK_SIZE;
					syncManifest.Slices.Add(slice);
				}

				while(offset < file.Value.Length) {
					if(remainingFileBytes <= remainingSliceSize) {
						// end of this file
						slice.fileSlices.Add(file.Key, new DataSliceInfo {Offset = offset, Length = remainingFileBytes});
						remainingSliceSize -= remainingFileBytes;

						break;
					}

					// cut the file data
					slice.fileSlices.Add(file.Key, new DataSliceInfo {Offset = offset, Length = remainingSliceSize});
					offset += remainingSliceSize;
					remainingFileBytes -= remainingSliceSize;

					slice = new DATA_SLICE();
					remainingSliceSize = MAXIMUM_UNIFIED_BLOCK_SIZE;
					syncManifest.Slices.Add(slice);
				}
			}

			// assign them unique Ids
			ushort index = 1;

			foreach(DATA_SLICE lslice in syncManifest.Slices) {
				lslice.sliceId = index++;
			}
		}

		/// <summary>
		///     Here we take a list of potential public block heights and determine if we will update our own. We dont quite trust
		///     a single actor, so ew check for consensus among all our replies
		/// </summary>
		/// <param name="potentialPublicBlockHeights"></param>
		protected void UpdatePublicBlockHeight(List<long> potentialPublicBlockHeights) {

			potentialPublicBlockHeights = potentialPublicBlockHeights.Where(v => v > 0).ToList();

			(long result, ConsensusUtilities.ConsensusType concensusType) publicBlockHeightConsensus = ConsensusUtilities.GetConsensus(potentialPublicBlockHeights);

			long publicBlockHeight = this.ChainStateProvider.PublicBlockHeight;

			if(publicBlockHeightConsensus.concensusType == ConsensusUtilities.ConsensusType.Single) {
				// we got a simple reply, we can't quite trust it. we only take it if its bigger than current. we may overwrite it when we get a proper consensus
				if(publicBlockHeightConsensus.result > publicBlockHeight) {
					publicBlockHeight = publicBlockHeightConsensus.result;
				}
			} else if(publicBlockHeightConsensus.concensusType == ConsensusUtilities.ConsensusType.Split) {

				// in case of the split, we will be safe and take the highest height that is higher than our chain. we may overwrite it when we get a proper consensus
				try {
					var entries = potentialPublicBlockHeights.Where(e => e >= publicBlockHeight).ToList();

					if(entries.Any()) {
						long reportedHeight = entries.Max();

						if(reportedHeight > publicBlockHeight) {
							publicBlockHeight = reportedHeight;
						}
					}
				} catch {
					// we just let it go
				}
			} else {
				if(publicBlockHeightConsensus.concensusType == ConsensusUtilities.ConsensusType.Absolute) {
					// this is easy, everybody agrees, let's take it
					publicBlockHeight = publicBlockHeightConsensus.result;
				} else {
					// ok, we got a split decision. we dont necessarily take the majority since we could be missing a new update.
					// we will simply take the highest with more than 2 in agreement
					var groupings = ConsensusUtilities.GetConsensusGroupings(potentialPublicBlockHeights);
					var multipleGroupings = groupings.Where(c => c.Count > 1).ToList();

					if(multipleGroupings.Any()) {
						publicBlockHeight = multipleGroupings.Max(c => c.Value);
					} else {
						// finally, everyone disagrees. we will take the highest number for now...  we may overwrite it when we get a proper consensus
						try {
							long reportedHeight = 0;
							var potentials = potentialPublicBlockHeights.Where(e => e >= publicBlockHeight).ToList();

							if(potentials.Any()) {
								reportedHeight = potentials.Max();
							}

							if(reportedHeight > publicBlockHeight) {
								publicBlockHeight = reportedHeight;
							}
						} catch {
							// we just let it go
						}
					}
				}
			}

			// ok, thats our new value
			if(publicBlockHeight != this.ChainStateProvider.PublicBlockHeight) {
				this.ChainStateProvider.PublicBlockHeight = publicBlockHeight;
			}

		}

		protected string GetDownloadTempDirName(string path) {
			return Path.Combine(Path.GetDirectoryName(path), DOWNLOAD_TEMP_DIR_NAME);
		}

		protected enum DownloadResults {
			Valid,
			InvalidBlockData,
			Invalid
		}

		protected class PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE>
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<CHANNEL_INFO_SET_REQUEST, T_REQUEST, KEY, SLICE_KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET_REQUEST : ChannelsInfoSet<SLICE_KEY, T_REQUEST>, new()
			where CHANNEL_INFO_SET_RESPONSE : ChannelsInfoSet<SLICE_KEY, T_RESPONSE>, new()
			where T_REQUEST : DataSliceInfo, new()
			where T_RESPONSE : DataSlice, new() {

			public Guid PeerId { get; set; }
			public DateTime Timeout { get; set; } = DateTime.MaxValue;

			public bool HasTimedOut => this.Strikes > 0;
			public bool Connected { get; set; }
			public bool IsTimeout => DateTime.Now > this.Timeout;
			public SlicePeersContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> Slice { get; set; }
			public bool IsFree => this.Slice == null;

			public bool CanTakeSlice => this.IsFree && this.Connected;
			public bool CanTakeSliceNoTimeout => this.CanTakeSlice && !this.HasTimedOut;

			public int Strikes { get; set; }

			public void ResetSlice() {
				this.Slice.ResetSlice();
				this.Slice = null;
				this.Timeout = DateTime.MaxValue;
			}

			public void SetSlice(SlicePeersContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> slice) {
				this.Slice = slice;
				this.Timeout = DateTime.Now + TimeSpan.FromSeconds(PEER_SLICE_TIMEOUT);
			}

			public void SetTimedout() {
				if(this.IsTimeout) {
					this.Strikes++;
					this.ResetSlice();
				}
			}
		}

		protected class SlicePeersContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE>
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<CHANNEL_INFO_SET_REQUEST, T_REQUEST, KEY, SLICE_KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET_REQUEST : ChannelsInfoSet<SLICE_KEY, T_REQUEST>, new()
			where CHANNEL_INFO_SET_RESPONSE : ChannelsInfoSet<SLICE_KEY, T_RESPONSE>, new()
			where T_REQUEST : DataSliceInfo, new()
			where T_RESPONSE : DataSlice, new() {

			public enum SliceStatuses {
				Ready,
				InProgress,
				Completed
			}

			public readonly List<Guid> FailedPeers = new List<Guid>();

			public int SliceIndex { get; set; }

			public SliceStatuses Status { get; set; } = SliceStatuses.Ready;

			public PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE> SliceInfo { get; set; }
			public PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> CurrentPeer { get; set; }
			public bool Completed => this.Status == SliceStatuses.Completed;
			public bool Ready => this.Status == SliceStatuses.Ready;

			public void SetCurrentPeer(PeerSlicesContext<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, DATA_REQUEST, DATA_RESPONSE> peerContext) {
				this.CurrentPeer = peerContext;
				this.CurrentPeer.SetSlice(this);
				this.Status = SliceStatuses.InProgress;
			}

			public void SetCompleted(int hash) {
				if(this.Status == SliceStatuses.InProgress) {
					this.Status = SliceStatuses.Completed;
					this.SliceInfo.connection = null;
					this.SliceInfo.PeerId = this.CurrentPeer.PeerId;
					this.SliceInfo.Hash = hash;
					this.CurrentPeer.ResetSlice();
					this.CurrentPeer = null;
				}

			}

			public void ResetSlice() {
				if(this.Status != SliceStatuses.Completed) {
					this.FailedPeers.Add(this.CurrentPeer.PeerId);
					this.SliceInfo.connection = null;
					this.CurrentPeer = null;
					this.Status = SliceStatuses.Ready;
				}
			}
		}

		private struct SyncHistory {
			public long blockId;
			public DateTime timestamp;
		}

		protected enum ResponseValidationResults {
			Invalid,
			NoData,
			Valid
		}

		protected class FetchInfoParameter<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, DATA_SLICE>
			where INFO_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoRequest<KEY>
			where INFO_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoResponse<CHANNEL_INFO_SET, T, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET : ChannelsInfoSet<SLICE_KEY, T>
			where T : DataSliceSize, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where SINGLE_ENTRY_CONTEXT : SingleEntryContext<SLICE_KEY, SYNC_MANIFEST, ENTRY_DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {

			public Func<BlockchainTargettedMessageSet<INFO_REQUEST>> generateInfoRequestMessage;

			public KEY id;

			public Func<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, List<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>> selectUsefulConnections;

			public SINGLE_ENTRY_CONTEXT singleEntryContext;
			public Func<ITargettedMessageSet<INFO_RESPONSE, IBlockchainEventsRehydrationFactory>, List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>, Dictionary<Guid, ENTRY_DETAILS>, List<Guid>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> validNextInfoFunc;
		}

		protected class FetchSliceHashesParameter<CHANNEL_INFO_SET, T, KEY, SLICE_KEY, ENTRY_DETAILS, INFO_REQUEST, INFO_RESPONSE>
			where INFO_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesRequest<KEY>
			where INFO_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesResponse<KEY>
			where CHANNEL_INFO_SET : ChannelsInfoSet<SLICE_KEY, T>
			where T : DataSliceSize, new()
			where ENTRY_DETAILS : new() {
			public ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> Connections;

			public Func<BlockchainTargettedMessageSet<INFO_REQUEST>> generateInfoRequestMessage;

			public KEY id;

			public Func<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, List<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>> selectUsefulConnections;

			public Func<ITargettedMessageSet<INFO_RESPONSE, IBlockchainEventsRehydrationFactory>, List<PeerRequestInfo<KEY, INFO_REQUEST, INFO_RESPONSE>>, Dictionary<Guid, ENTRY_DETAILS>, List<Guid>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> validNextInfoFunc;
		}

		protected class FetchDataParameter<CHANNEL_INFO_SET_REQUEST, T_REQUEST, CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY, ENTRY_DETAILS, DATA_REQUEST, DATA_RESPONSE, SYNC_MANIFEST, SINGLE_ENTRY_CONTEXT, COMPLETED_CONTENTS, DATA_SLICE>
			where DATA_REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<CHANNEL_INFO_SET_REQUEST, T_REQUEST, KEY, SLICE_KEY>
			where DATA_RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<CHANNEL_INFO_SET_RESPONSE, T_RESPONSE, KEY, SLICE_KEY>
			where CHANNEL_INFO_SET_REQUEST : ChannelsInfoSet<SLICE_KEY, T_REQUEST>
			where CHANNEL_INFO_SET_RESPONSE : ChannelsInfoSet<SLICE_KEY, T_RESPONSE>
			where T_REQUEST : DataSliceInfo, new()
			where T_RESPONSE : DataSlice, new()
			where SYNC_MANIFEST : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>
			where ENTRY_DETAILS : new()
			where SINGLE_ENTRY_CONTEXT : SingleEntryContext<SLICE_KEY, SYNC_MANIFEST, ENTRY_DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where COMPLETED_CONTENTS : class
			where DATA_SLICE : FilesetSyncManifest<SLICE_KEY, DATA_SLICE>.SyncingDataSlice<SLICE_KEY>, new() {
			public BlockFetchAttemptTypes blockFetchAttempt;

			public Action clearManifest;
			public Func<COMPLETED_CONTENTS, bool> downloadCompleted;

			public ENTRY_DETAILS entryInfo;
			public Func<BlockchainTargettedMessageSet<DATA_REQUEST>> generateMultiSliceDataRequestMessage;

			public KEY id;

			public Func<COMPLETED_CONTENTS> prepareCompletedData;

			public Action<DATA_REQUEST> prepareFirstRunRequestMessage;

			public Action<DATA_RESPONSE, Guid, Dictionary<Guid, ENTRY_DETAILS>> processReturnMessage;

			public Func<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, List<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>> selectUsefulConnections;
			public SINGLE_ENTRY_CONTEXT singleEntryContext;

			public Action updateSyncManifest;

			public Func<ITargettedMessageSet<DATA_RESPONSE, IBlockchainEventsRehydrationFactory>, Dictionary<Guid, ENTRY_DETAILS>, List<PeerRequestInfo<KEY, DATA_REQUEST, DATA_RESPONSE>>, List<Guid>, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResponseValidationResults> validSlicesFunc;

			public Action<CHANNEL_INFO_SET_RESPONSE, DATA_RESPONSE> writeDataSlice;
		}

		protected enum BlockFetchAttemptTypes : byte {
			None = 0,
			Attempt1 = 1,
			Attempt2 = 2,
			Attempt3 = 3,
			Overflow = 4
		}

		/// <summary>
		///     Used to hold context while syncing blocks. Also holds connection about troublemakers which can be used to build
		///     statistics
		/// </summary>
		/// <typeparam name="CHAIN_SYNC_TRIGGER"></typeparam>
		/// <typeparam name="SERVER_TRIGGER_REPLY"></typeparam>
		protected class SingleEntryContext<FILE_KEY, SYNC_MANIFEST, DETAILS, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DATA_SLICE>
			where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
			where SERVER_TRIGGER_REPLY : ServerTriggerReply
			where SYNC_MANIFEST : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
			where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY>, new()
			where DETAILS : new() {

			/// <summary>
			///     Here we keep a log of peers that caused us trouble during syncing for each attempt
			/// </summary>
			public readonly Dictionary<int, Dictionary<int, List<PeerConnection>>> faultyPeers = new Dictionary<int, Dictionary<int, List<PeerConnection>>>();

			/// <summary>
			///     The list of peers we sent the data to for each attempt and sub attempt
			/// </summary>
			public readonly Dictionary<int, Dictionary<int, List<PeerConnection>>> sentPeers = new Dictionary<int, Dictionary<int, List<PeerConnection>>>();

			public BlockFetchAttemptTypes blockFetchAttempt;
			public byte blockFetchAttemptCounter;

			public DETAILS details;

			//public ChannelsInfoSet<BlockChannelsInfoSet<DataSliceSize>, DataSliceSize> infos;

			public SYNC_MANIFEST syncManifest;

			public SingleEntryContext() {
				this.details = new DETAILS();
			}

			public ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> Connections { get; set; }

			public void AddSentPeerAttempt(PeerConnection peer, int attempt, int subattempt) {
				this.AddPeerAttempt(this.sentPeers, peer, attempt, subattempt);
			}

			public void AddFaultyPeerAttempt(PeerConnection peer, int attempt, int subattempt) {
				// make sure the peer is now banned
				this.Connections.AddBannedConnection(peer);

				this.AddPeerAttempt(this.faultyPeers, peer, attempt, subattempt);
			}

			private void AddPeerAttempt(Dictionary<int, Dictionary<int, List<PeerConnection>>> collection, PeerConnection peer, int attempt, int subattempt) {

				if(!collection.ContainsKey(attempt)) {
					collection.Add(attempt, new Dictionary<int, List<PeerConnection>>());
				}

				var attemptEntry = collection[attempt];

				if(!attemptEntry.ContainsKey(subattempt)) {
					attemptEntry.Add(subattempt, new List<PeerConnection>());
				}

				var subAttemptEntry = attemptEntry[subattempt];

				if(!subAttemptEntry.Contains(peer)) {
					subAttemptEntry.Add(peer);
				}
			}
		}

		protected class PeerRequestInfo<KEY, REQUEST, RESPONSE>
			where REQUEST : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncRequest<KEY>
			where RESPONSE : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncResponse<KEY> {

			public readonly int index;
			public ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>.ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connection;
			public int Hash;
			public Guid PeerId;
			public BlockchainTargettedMessageSet<REQUEST> requestMessage;
			public RESPONSE responseMessage;

			public PeerRequestInfo(int index, BlockchainTargettedMessageSet<REQUEST> requestMessage) {
				this.index = index;
				this.requestMessage = requestMessage;
			}
		}
	}
}