using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Digest;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync {
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


		private readonly ConcurrentDictionary<BlockId, bool> downloadQueue = new ConcurrentDictionary<BlockId, bool>();

		/// <summary>
		///     Now we perform the synchronization for the next block
		/// </summary>
		protected virtual ResultsState SynchronizeGenesisBlock(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {
			this.CheckShouldCancel();

			BlockSingleEntryContext singleEntryContext = new BlockSingleEntryContext();

			singleEntryContext.Connections = connections;
			singleEntryContext.details.Id = 1;

			singleEntryContext.syncManifest = this.LoadBlockSyncManifest(singleEntryContext.details.Id);

			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.BlockchainSyncUpdate(1, 1, ""));

			if((singleEntryContext.syncManifest != null) && ((singleEntryContext.syncManifest.Key != 1) || (singleEntryContext.syncManifest.Attempts >= 3))) {
				// we found one, but if we are here, it is stale so we delete it
				this.ClearBlockSyncManifest(singleEntryContext.details.Id);

				singleEntryContext.syncManifest = null;
			}

			if((singleEntryContext.syncManifest != null) && singleEntryContext.syncManifest.IsComplete) {

				// we are done it seems. move on to the next
				this.ClearBlockSyncManifest(singleEntryContext.details.Id);

				return ResultsState.OK;
			}

			if(singleEntryContext.syncManifest == null) {
				// ok, determine if there is a digest to get

				// no choice, we must fetch the connection
				Repeater.Repeat(() => {

					var nextBlockPeerDetails = this.FetchPeerBlockInfo(singleEntryContext, true, true);

					singleEntryContext.details = this.GetBlockInfoConsensus(nextBlockPeerDetails.results);
				});

				// ok, lets start the sync process
				singleEntryContext.syncManifest = new BlockFilesetSyncManifest();

				singleEntryContext.syncManifest.Key = singleEntryContext.details.Id;

				// lets generate the file map
				foreach(var channel in singleEntryContext.details.nextBlockSize.SlicesInfo) {
					singleEntryContext.syncManifest.Files.Add(channel.Key, new DataSlice { Length = channel.Value.Length});
				}

				this.GenerateSyncManifestStructure<BlockFilesetSyncManifest, BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest.BlockSyncingDataSlice>(singleEntryContext.syncManifest);

				// save it to keep our state
				this.CreateBlockSyncManifest(singleEntryContext.syncManifest);
			}

			singleEntryContext.blockFetchAttemptCounter = 1;
			singleEntryContext.blockFetchAttempt = BlockFetchAttemptTypes.Attempt1;

			// keep the statistics on this block
			this.BlockContexts.Enqueue(singleEntryContext);

			while(true) {
				this.CheckShouldStopThrow();

				bool success = false;

				try {
					Log.Verbose($"Fetching genesis block data, attempt {singleEntryContext.blockFetchAttemptCounter}");

					if(singleEntryContext.syncManifest.IsComplete) {
						// if we are here and the manifest is fully complete, something went very wrong. lets clear it and start over.
						this.ResetBlockSyncManifest(singleEntryContext.syncManifest);
					}

					var result = this.FetchPeerBlockData(singleEntryContext, false);

					if(result.state == ResultsState.OK) {
						success = true;
					}
				} catch(NoSyncingConnectionsException e) {
					throw;
				} catch(Exception e) {
					Log.Error(e, "Failed to fetch genesis block data. might try again...");
				}

				if(!success) {
					Log.Fatal("Failed to fetch genesis block data. we tried all the attempts we could and it still failed. this is critical. we may try again.");

					// well, thats not great, we have to try again if we can
					singleEntryContext.blockFetchAttempt += 1;
					singleEntryContext.blockFetchAttemptCounter += 1;

					if(singleEntryContext.blockFetchAttempt == BlockFetchAttemptTypes.Overflow) {

						// thats it, we tried all we could and we are still failing, this is VERY serious and we kill the sync
						throw new AttemptsOverflowException("Failed to sync, maximum amount of block sync reached. this is very critical.");
					}
				} else {
					// we are done
					break;
				}
			}

			if(this.BlockContexts.Count > MAXIMUM_CONTEXT_HISTORY) {
				// dequeue a previous context
				this.BlockContexts.Dequeue();

				//TODO: add some higher level analytics
			}

			return ResultsState.OK;
		}

		protected PeerBlockSpecs GetBlockInfoConsensus(Dictionary<Guid, PeerBlockSpecs> peerBlockSpecs) {
			// ok, we have the previous block details provider, but if new peers were added since, we will take their trigger connection and add it here

			PeerBlockSpecs consensusSpecs = new PeerBlockSpecs();

			// make a consensus of what we already have

			ConsensusUtilities.ConsensusType nextBlockHeightConsensusType = ConsensusUtilities.ConsensusType.Undefined;

			if(peerBlockSpecs.Any()) {
				try {
					(consensusSpecs.Id, nextBlockHeightConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values, a => a.Id);

					if(nextBlockHeightConsensusType == ConsensusUtilities.ConsensusType.Split) {
						throw new SplitDecisionException();
					}
				} catch(SplitDecisionException e) {

					// ok, we have a tie. lets try without the 0 blockheights. if it fires an exception again
					try {
						(consensusSpecs.Id, nextBlockHeightConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values.Where(v => v.Id > 0), a => a.Id);

						if(nextBlockHeightConsensusType == ConsensusUtilities.ConsensusType.Split) {
							throw new SplitDecisionException();
						}
					} catch(SplitDecisionException e2) {
						// lets do nothing, since we will try again concensus below with more data
						consensusSpecs.Id = long.MaxValue;
					}
				}

				// here we dont need to test the consensus. its its the end, we finish, otherwise we will test again below
			}

			if((consensusSpecs.Id == null) || (consensusSpecs.Id <= 0)) {
				consensusSpecs.end = true;

				// well, the consensus tells us that we have reached the end of the chain. we are fully synched and we can now stop
				return consensusSpecs;
			}

			// ok, get the ultimate consensus on the next block from everyone that matters right now

			(SafeArrayHandle nextBlockHash, ConsensusUtilities.ConsensusType nextBlockHashConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values.Where(v => (v.nextBlockHash.HasData) && v.nextBlockHash.HasData), a => (a.nextBlockHash.Entry.GetArrayHash(), a.nextBlockHash));

			this.TestConsensus(nextBlockHeightConsensusType, nameof(consensusSpecs.Id));
			this.TestConsensus(nextBlockHeightConsensusType, nameof(nextBlockHash));

			var consensusSet = ChannelsInfoSet.RestructureConsensusBands<BlockChannelUtils.BlockChannelTypes, BlockChannelsInfoSet<DataSliceSize>, DataSliceSize>(peerBlockSpecs.ToDictionary(c => c.Key, c => c.Value.nextBlockSize));

			// now the various channel sizes
			foreach(BlockChannelUtils.BlockChannelTypes channel in consensusSet.Keys) {

				if(!consensusSpecs.nextBlockSize.SlicesInfo.ContainsKey(channel)) {
					consensusSpecs.nextBlockSize.SlicesInfo.Add(channel, new DataSliceSize());
				}

				ConsensusUtilities.ConsensusType consensusType;
				(consensusSpecs.nextBlockSize.SlicesInfo[channel].Length, consensusType) = ConsensusUtilities.GetConsensus(consensusSet[channel], a => a.entry.Length);

				this.TestConsensus(consensusType, channel + "-connection.Length");
			}

			consensusSpecs.nextBlockHash.Entry = nextBlockHash.Entry;

			return consensusSpecs;
		}

		/// <summary>
		///     here we attempt to load and install a block from a cached gossip block message
		/// </summary>
		/// <param name="currentBlockId"></param>
		/// <returns></returns>
		protected virtual List<(IBlockEnvelope envelope, long xxHash)> AttemptToLoadBlockFromGossipMessageCache(long currentBlockId) {

			// we can query first in our own thread. 
			try {

				if(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetUnvalidatedBlockGossipMessageCached(currentBlockId)) {
					
					return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedUnvalidatedBlockGossipMessage(currentBlockId);
				}

			} catch(Exception ex) {
				Log.Error(ex, $"Failed to query Unvalidated Block Gossip messages for block Id {currentBlockId}");

				// otherwise, just continue...
			}

			return new List<(IBlockEnvelope envelope, long xxHash)>();
		}

		/// <summary>
		///     Run a syncing action inside the confinsed of assurances that syncing peers are available or being queried
		/// </summary>
		/// <param name="action"></param>
		/// <param name="retryAttempt"></param>
		/// <param name="connections"></param>
		/// <returns></returns>
		/// <exception cref="NoSyncingConnectionsException"></exception>
		/// <exception cref="WorkflowException"></exception>
		/// <exception cref="ImpossibleToSyncException"></exception>
		private ResultsState RunBlockSyncingAction(Func<ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, ResultsState> action, int maxRetryAttempts, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {

			// ok, at this point, we are ready to begin our sync, block by block
			if(this.ChainStateProvider.IsChainDesynced && !this.CentralCoordinator.IsShuttingDown) {

				int retryAttempt = 1;
				int connectionRetryAttempt = 1;

				void Sleep(int milliseconds) {
					DateTime timeout = DateTime.Now.AddMilliseconds(milliseconds);
					while(DateTime.Now > timeout) {

						Thread.Sleep(100);
								
						this.CheckShouldStopThrow();
					}
				}
				
				while((retryAttempt <= maxRetryAttempts) && (connectionRetryAttempt <= maxRetryAttempts)) {
					// we run two parallel threads. one is the workflow to get new peers in the sync, the other is the sync itself with the peers we have
					this.CheckShouldStopThrow();

					if(this.NetworkPaused) {
						return ResultsState.NetworkPaused;
					}
					// first lets get new connections
					if(this.newPeerTask == null) {

						// free the rejected connections that are ready to be evaluated again
						connections.FreeLowLevelBans();

						// Get the ultimate list of all connections we currently have in the network manager and extract the ones that are new
						var newConnections = connections.GetNewPotentialConnections(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase, this.chainType);

						
						if(newConnections.Any() && (connectionRetryAttempt <= maxRetryAttempts)) {
							// create a copy of our connections so we can work in parallel
							var newConnectionSet = new ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>();

							newConnectionSet.Set(newConnections);

							// lets launch our parallel task to get new peers while we sync
			
							this.newPeerTask = Task.Run(() => this.FetchNewPeers(newConnectionSet, connections), this.CancelNeuralium);

							this.newPeerTask?.ContinueWith(t => {
								// retrieve the results
								this.HandleNewPeerTaskResult(ref this.newPeerTask, connections);
							}, TaskContinuationOptions.OnlyOnRanToCompletion);


							while(!connections.HasSyncingConnections && (this.newPeerTask != null) && !this.newPeerTask.IsCompleted) {

								Thread.Sleep(50);
								
								this.CheckShouldStopThrow();
							}

							if(!connections.HasSyncingConnections || (connections.SyncingConnectionsCount < this.MinimumSyncPeerCount)) {
								// if we still have none or nont enough, then lets clear the rejected connections. give them a chance
								connections.FreeLowLevelBans();

								if(connectionRetryAttempt > 1) {
									// we can give this a try
									connections.ClearBanned();
								}

								// sleep a bit before we retry
								Thread.Sleep(100);
								connectionRetryAttempt++;

								continue;
							}

							connectionRetryAttempt = 0;
						} else if(!connections.HasSyncingConnections && (connectionRetryAttempt <= maxRetryAttempts)) {
							
							// sleep about 1 second
							Sleep(1000);
							
							connectionRetryAttempt++;

							continue;
						} else if(!connections.HasSyncingConnections) {
							// ok, thats a big deal, we have no new connections and no more syncing connections. we have to stop syncing.
							Log.Debug("No more syncing connections and no new connections. we have nobody to talk to.");

							return ResultsState.NoSyncingConnections;
						}

						connectionRetryAttempt = 0;
					} else if(!connections.HasSyncingConnections) {

						Sleep(1000);

						connectionRetryAttempt++;

						continue;
					}

					if(connectionRetryAttempt > maxRetryAttempts) {

						throw new ImpossibleToSyncException($"Exceptions occured. we tried {connectionRetryAttempt} times and failed every times. failed to sync.");
					}

					// make sure we have some friends to talk to
					if(connections.HasSyncingConnections) {

						// now, Sync the block, if any peers are willing
						this.UpdatePublicBlockHeight(connections.GetAllConnections().Select(c => c.ReportedPublicBlockHeight).ToList());

						this.CheckShouldStopThrow();

						try {
							// the genesis is always the first thing we sync if we have nothing
							ResultsState state = ResultsState.None;

							state = action?.Invoke(connections) ?? ResultsState.Error;

							if(state == ResultsState.OK) {
								return ResultsState.OK;
							}

							if(state == ResultsState.NoSyncingConnections) {
								Log.Verbose("We have no more syncing connections. we will try to get some more.");

								throw new NoSyncingConnectionsException();
							}

							if(state == ResultsState.Error) {
								Log.Verbose("An error occured.");

								throw new WorkflowException();
							}

						} catch(NoDigestInfoException ndex) {
							Log.Verbose("No digest information could be obtained.");

							// thats it, for digest we continue
							return ResultsState.OK;

						} catch(NoSyncingConnectionsException e) {
							Log.Verbose("We have no more syncing connections. we will try to get some more.");

							retryAttempt++;

							Thread.Sleep(TimeSpan.FromSeconds(3));
						} catch(AttemptsOverflowException e) {
							Log.Verbose(e, "We have attempted to correct errors and have reached an overflow limit.");

							throw new ImpossibleToSyncException("We have attempted to correct errors and have reached an overflow limit.", e);
						} catch(Exception e) {
							Log.Verbose(e, "");

							retryAttempt++;
						}

						if(retryAttempt > maxRetryAttempts) {

							throw new ImpossibleToSyncException($"Exceptions occured. we tried {retryAttempt} times and failed every times. failed to sync.");
						}

					} else {
						// well, we have no connections. if we have a fetching task going, then we just wait a bit
						if(this.newPeerTask != null) {
							this.CheckShouldStopThrow();
							this.newPeerTask?.Wait();
						}
					}

					// now check if we got new peers for the next round
					this.HandleNewPeerTaskResult(ref this.newPeerTask, connections);

				}
			}

			return ResultsState.OK;
		}

		/// <summary>
		///     Run the 3 parallel processes that will ensure blocks are being synced.
		///     1. downloading the blocks to disk
		///     2. rehydrate, validate and insert block to disk
		///     3. interpret the blocks
		/// </summary>
		/// <param name="connections"></param>
		private void LaunchMainBlockSync(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {

			this.UpdateSignificantActionTimestamp();
			
			bool running = true;
			this.nextGCCollect = DateTime.UtcNow.AddMinutes(1);

			//this variable ensures that we did at least one network check to udpate our blockchain info. set it to false, to reset the check
			this.updatePublicBlockHeightPerformed = false;

			if(this.CheckShouldStop()) {
				return;
			}
			
			// launch the various tasks
			var downloadTask = Task<bool>.Factory.StartNew(() => {

				Thread.CurrentThread.Name = "Chain Sync Download Thread";
				
				this.CheckShouldStopThrow();

				int blockGossipCacheProximityLevel = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockGossipCacheProximityLevel;

				while(running) {

					int sleepTime = 0;
					if(this.CheckShouldStop()) {
						return false;
					}

					if(this.NetworkPaused) {
						sleepTime = 1000;
					} else {
						if(!this.PrepareNextBlockDownload(connections)) {
							sleepTime = 500;
						} else {
							this.UpdateSignificantActionTimestamp();
						}
					}

					if(sleepTime != 0) {
						Thread.Sleep(1000);
					}
				}

				return true;
			}, this.CancelTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

			var verifyTask = Task<bool>.Factory.StartNew(() => {

				Thread.CurrentThread.Name = "Chain Sync Verification Thread";
				
				this.CheckShouldStopThrow();

				while(running) {

					if(this.CheckShouldStop()) {
						return false;
					}
					
					if(!this.InsertNextBlock(connections)) {
						Thread.Sleep(100);
					} else {
						this.UpdateSignificantActionTimestamp();
					}
					
				}

				return true;
			}, this.CancelTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

			var interpretationTask = Task<bool>.Factory.StartNew(() => {
				
				Thread.CurrentThread.Name = "Chain Sync Interpretation Thread";
				this.CheckShouldStopThrow();

				while(running) {

					if(this.CheckShouldStop()) {
						return false;
					}

					if(!this.InterpretNextBlock()) {
						Thread.Sleep(100);
					} else {
						this.UpdateSignificantActionTimestamp();
					}
				}

				return true;
			}, this.CancelTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			

			var tasksSet = new Task[] {downloadTask, verifyTask, interpretationTask};

			while(true) {

				bool blockHeightCheck = false;

				// if we at least made an update, then we can start to filter on height equality, otherwise we force an update
				if(this.updatePublicBlockHeightPerformed) {

					// ok, the public block height was updated at least once, now we can compare for equality and stop when the heights match.
					long blockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
					long publicBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight;

					blockHeightCheck = (blockHeight == publicBlockHeight);
				}

				bool stop = this.CheckShouldStop() || tasksSet.Any(t => t.IsCompleted) || blockHeightCheck || this.SignificantActionTimeout();

				if(stop) {

					running = false;

					void Wait(int time) {
						try {
							Task.WaitAll(tasksSet.Where(t => !t.IsCompleted).ToArray(), TimeSpan.FromSeconds(time));
						} catch(TaskCanceledException tex) {
							// ignore it						
						} catch(AggregateException agex) {
							agex.Handle(ex => {

								if(ex is TaskCanceledException) {
									// ignore it					
									return true;
								}

								return false;
							});
						}
					}

					Wait(3);

					if(tasksSet.Any(t => !t.IsCompleted)) {
						this.CancelTokenSource.Cancel();

						Wait(10);
					}

					// lets push forward the faults
					if(tasksSet.Any(t => t.IsFaulted)) {
						var faultedTasks = tasksSet.Where(t => t.IsFaulted && (t.Exception != null)).ToList();

						var exceptions = new List<Exception>();

						foreach(Task task in faultedTasks) {

							if(task.Exception is AggregateException agex) {
								exceptions.AddRange(agex.InnerExceptions.Where(e => !(e is TaskCanceledException)));
							} else {
								exceptions.Add(task.Exception);
							}
						}

						if(exceptions.Any()) {
							throw new AggregateException(exceptions);
						}
					}

					return;
				}

				if(this.ShouldAct(ref this.nextGCCollect)) {
					GC.Collect();

					// lets act again in X seconds
					this.nextGCCollect = DateTime.UtcNow.AddMinutes(1);
				}
				Thread.Sleep(1000);
			}
		}

		protected virtual bool CheckShouldStop() {
			return this.NetworkPaused || this.shutdownRequest || this.CheckCancelRequested();
		}

		protected void CheckShouldStopThrow() {
			if(this.CheckShouldStop()) {
				this.CancelTokenSource.Cancel();
				this.CancelNeuralium.ThrowIfCancellationRequested();
			}
		}

		private DateTime? nextGCCollect;
		
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

		private BlockId currentBlockDownloadId = 0;
		
		/// <summary>
		///     Download the next block in line, or rather, the next required block
		/// </summary>
		/// <param name="connections"></param>
		/// <returns></returns>
		/// <exception cref="WorkflowException"></exception>
		protected virtual bool PrepareNextBlockDownload(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {

			try {
				long downloadBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DownloadBlockHeight;

				if(!this.downloadQueue.Any()) {
					// if we have no blocks in the queue, we add the next set, unless we have them all

					long publicBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight;

					if(downloadBlockHeight < publicBlockHeight) {
						int end = (int) (publicBlockHeight - downloadBlockHeight);

						foreach(int entry in Enumerable.Range(1, Math.Min(end, 100))) {
							this.downloadQueue.AddSafe(entry + downloadBlockHeight, false);
						}
					} else if(downloadBlockHeight == publicBlockHeight) {

						if(this.updatePublicBlockHeightPerformed == false){

							ResultsState resultx = this.RunBlockSyncingAction(connectionsSet => {
								//if we need to get a digest, we do now
								if(connections.HasSyncingConnections && connections.SyncingConnectionsCount >= this.MinimumSyncPeerCount) {
									// ok, we need to force an update to get the lastesdt blockchain info
									BlockSingleEntryContext singleEntryContext = new BlockSingleEntryContext();
									singleEntryContext.Connections = connections;
									singleEntryContext.details.Id = publicBlockHeight;
									this.UpdateBlockInfo(singleEntryContext);
								}

								return ResultsState.OK;
							}, 2, connections);
						}

						// we reached the end. lets just wait until we either must stop or have more to fetch
						return false;
					}
				}

				if(!this.downloadQueue.Any()) {
					// nothing to download, let's sleep a while
					return false;
				}

				// get the next lowest entry to download
				this.currentBlockDownloadId = this.downloadQueue.Keys.ToArray().OrderBy(k => k).First();

				if(this.currentBlockDownloadId < downloadBlockHeight) {
					// we are downloading a block that is back in time, lets free the loosely banned peers
					connections.FreeLowLevelBans();
				}

				bool force = this.downloadQueue[this.currentBlockDownloadId];

				bool done = false;

				// first thing, check if we have a cached gossip message
				if(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetUnvalidatedBlockGossipMessageCached(this.currentBlockDownloadId)) {
					done = true;
				} else {
					// now we check if we have a completed syncing manifest for it
					ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(this.currentBlockDownloadId);

					if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.Completed) {
						done = true;
					}
				}

				if(done) {
					if(force) {
						// reset it all
						this.ClearBlockSyncManifest(this.currentBlockDownloadId);
					} else {
						// we already have it, we move on.
						this.UpdateDownloadBlockHeight(this.currentBlockDownloadId);

						this.downloadQueue.RemoveSafe(this.currentBlockDownloadId);

						return true;
					}
				}

				ResultsState result = this.RunBlockSyncingAction(connectionsSet => {
					//if we need to get a digest, we do now
					this.DownloadNextBlock(this.currentBlockDownloadId, null, connectionsSet);

					this.UpdateDownloadBlockHeight(this.currentBlockDownloadId);

					this.downloadQueue.RemoveSafe(this.currentBlockDownloadId);
					
					return ResultsState.OK;
				}, 3, connections);

				if(result != ResultsState.OK) {
					throw new WorkflowException();
				}
			} finally {
				this.currentBlockDownloadId = 0;
			}

			return true;
		}

		/// <summary>
		///     Here we rehydrate a block from disk, validate it and if applcable, we insert it on disk
		/// </summary>
		/// <param name="connections"></param>
		/// <returns></returns>
		/// <exception cref="InvalidBlockDataException"></exception>
		/// <exception cref="WorkflowException"></exception>
		protected virtual bool InsertNextBlock(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {

			long downloadBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DownloadBlockHeight;
			long diskBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

			if(diskBlockHeight < downloadBlockHeight) {

				var dataChannels = new ChannelsEntries<SafeArrayHandle>();

				BlockId nextBlockId = new BlockId(diskBlockHeight + 1);

				LoadSources loadSource = LoadSources.NotLoaded;

				List<(IBlockEnvelope envelope, long xxHash)> gossipEnvelopes = null;
				IDehydratedBlock dehydratedBlock = null;
				BlockFilesetSyncManifest syncingManifest = null;

				bool isGossipCached = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetUnvalidatedBlockGossipMessageCached(nextBlockId);

				ValidationResult results = new ValidationResult(ValidationResult.ValidationResults.Invalid);

				try {
					// first thing, find which source to load
					if(isGossipCached) {
						// here we go, get the cached gossip message
						gossipEnvelopes = this.AttemptToLoadBlockFromGossipMessageCache(nextBlockId);

						if(gossipEnvelopes.Any()) {

							// ok, since we got this block from the local cache, we will need to query the next block info
							loadSource = LoadSources.Gossip;
						}
					}

					if(loadSource == LoadSources.NotLoaded) {
						// now we check if we have a completed syncing manifest for it
						ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(nextBlockId);

						if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.Completed) {

							syncingManifest = this.LoadBlockSyncManifest(nextBlockId);
							dataChannels = this.LoadBlockSyncManifestChannels(syncingManifest);
							loadSource = LoadSources.Sync;

							dehydratedBlock = new DehydratedBlock();

							try {
								dehydratedBlock.Rehydrate(dataChannels);

								dehydratedBlock.RehydrateBlock(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, true);
							}
							catch(UnrecognizedElementException urex) {
								
								throw;
							}
							catch(Exception ex) {
								throw new InvalidBlockDataException($"Failed to rehydrate block {nextBlockId} while syncing.", ex);
							}
						}

						if(loadSource == LoadSources.NotLoaded) {
							// something happened, we dont have this block, lets download it again
							this.AddBlockIdToDownloadQueue(nextBlockId);

							return false;
						}
					}

					this.CheckShouldStopThrow();

					// now perform validation

					if(loadSource == LoadSources.Gossip) {

						foreach((IBlockEnvelope envelope, long xxHash) potentialMessageEntry in gossipEnvelopes) {

							this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(potentialMessageEntry.envelope, false, result => {
								results = result;
							});

							if(results.Valid) {
								// ok, thats it, we found a valid block in our cache!
								dehydratedBlock = potentialMessageEntry.envelope.Contents;

								// now lets update the message, since we know it's validation status
								try {
									IMessageRegistryDal sqliteDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(this.centralCoordinator.BlockchainServiceSet.GlobalsService.GetSystemFilesDirectoryPath(), this.serviceSet);

									// update the validation status, we know its a good message
									sqliteDal.CheckMessageInCache(potentialMessageEntry.xxHash, true);

								} catch(Exception ex) {
									Log.Error(ex, "Failed to update cached message validation status");
								}

								break;
							}
						}
					} else if(loadSource == LoadSources.Sync) {
						this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateBlock(dehydratedBlock, false, result => {
							results = result;
						});
					}


					if(results.Valid) {

						bool valid = this.InsertBlockIntoChain(dehydratedBlock, 1);

						if(valid) {

							return true;
						}
					}

					throw new WorkflowException();
				} catch(UnrecognizedElementException urex) {
					
					throw;
				}
				catch(Exception ex) {
					if(ex is InvalidBlockDataException inex) {
						// pl, the data we received was invalid, we need to perform a check
						this.CompleteBlockSliceVerification(syncingManifest, connections);
					}

					if(loadSource == LoadSources.Gossip) {

						this.AddBlockIdToDownloadQueue(nextBlockId);
					} else if(loadSource == LoadSources.Sync) {

						this.AddBlockIdToDownloadQueue(nextBlockId);
					}
				} finally {

					// lets clean up
					if(loadSource == LoadSources.Gossip) {
		
						this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.ClearCachedUnvalidatedBlockGossipMessage(nextBlockId);

					} else if(loadSource == LoadSources.Sync) {
						this.ClearBlockSyncManifest(nextBlockId);
					}
				}
			}

			return false;
		}

		/// <summary>
		/// add a block id to the download queue, if ti is not already there and not being downloaded
		/// </summary>
		/// <param name="nextBlockId"></param>
		private void AddBlockIdToDownloadQueue(BlockId nextBlockId) {
			if(!this.downloadQueue.ContainsKey(nextBlockId) && this.currentBlockDownloadId != nextBlockId) {
				this.downloadQueue.AddSafe(nextBlockId, true);
			}
		}
		
		/// <summary>
		///     here we run the block interpretations to complete the insertion process
		/// </summary>
		/// <returns></returns>
		protected virtual bool InterpretNextBlock() {

			long blockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
			long diskBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

			if(blockHeight < diskBlockHeight) {

				BlockId nextBlockId = new BlockId(blockHeight + 1);

				(IBlock block, IDehydratedBlock dehydratedBlock) = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(nextBlockId);

				this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.InterpretBlock(block, dehydratedBlock, false, null, false);
				
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();
				
				this.rateCalculator.AddHistoryEntry(dehydratedBlock.BlockId);
				
				return true;
			}

			return false;
		}

		/// <summary>
		///     Ensure we have the latest downloaded block height. Only update if it is +1 from where we are
		/// </summary>
		/// <param name="nextBlockId"></param>
		private void UpdateDownloadBlockHeight(BlockId nextBlockId) {
			if(nextBlockId == (this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DownloadBlockHeight + 1)) {
				this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DownloadBlockHeight = nextBlockId;
			}
		}

		private void UpdateBlockInfo(BlockSingleEntryContext singleEntryContext) {
			Repeater.Repeat(() => {

				var nextBlockPeerDetails = this.FetchPeerBlockInfo(singleEntryContext, true, true);

				if(!nextBlockPeerDetails.results.Any()) {
					// we got no valuable results, we must get more peers.
					throw new NoSyncingConnectionsException();
				}

				singleEntryContext.details = this.GetBlockInfoConsensus(nextBlockPeerDetails.results);

				// while we are here, lets update the chain block height with the news. its always important to do so
				try {

					this.UpdatePublicBlockHeight(nextBlockPeerDetails.results.Values.Select(r => r.publicChainBlockHeight.Value).ToList());
				} catch(Exception ex) {
					Log.Error(ex, "Failed to update public block height");
				}
			}, 2);
		}
		/// <summary>
		///     Here we perform the actual downloading dance for a block
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="currentBlockPeerSpecs"></param>
		/// <param name="connections"></param>
		/// <exception cref="NoSyncingConnectionsException"></exception>
		/// <exception cref="AttemptsOverflowException"></exception>
		protected virtual void DownloadNextBlock(BlockId blockId, PeerBlockSpecs currentBlockPeerSpecs, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {
			this.CheckShouldStopThrow();

			bool fullyLoaded = false;
			BlockSingleEntryContext singleEntryContext = new BlockSingleEntryContext();
			singleEntryContext.details = currentBlockPeerSpecs;

			bool reuseBlockSpecs = false;

			if((singleEntryContext.details == null) || (singleEntryContext.details.Id != blockId)) {
				singleEntryContext.details = new PeerBlockSpecs();
				singleEntryContext.details.Id = blockId;
			} else {
				// we are good, we can reuse this
				reuseBlockSpecs = true;
			}

			PeerBlockSpecs nextBlockPeerSpecs = null;

			singleEntryContext.Connections = connections;

			singleEntryContext.syncManifest = this.LoadBlockSyncManifest(blockId);

			if((singleEntryContext.syncManifest != null) && ((singleEntryContext.syncManifest.Key != 1) || (singleEntryContext.syncManifest.Attempts >= 3))) {
				// we found one, but if we are here, it is stale so we delete it
				this.ClearBlockSyncManifest(blockId);

				singleEntryContext.syncManifest = null;
			}

			if((singleEntryContext.syncManifest != null) && singleEntryContext.syncManifest.IsComplete) {
				
				//TODO: check this
				return; // (null, ResultsState.OK);
			}

			if(singleEntryContext.details.Id == this.ChainStateProvider.DownloadBlockHeight) {
				// ok, the last download must have been lost, so we reset to the previous and do it all again
				this.ChainStateProvider.DownloadBlockHeight -= 1;
			}

			if(singleEntryContext.syncManifest == null) {
				// ok, determine if there is a digest to get

				// we might already have the block connection consensus
				if((reuseBlockSpecs == false) || fullyLoaded) {
					// no choice, we must fetch the connection

					this.UpdateBlockInfo(singleEntryContext);

					if(fullyLoaded || (singleEntryContext.details.Id == this.ChainStateProvider.DownloadBlockHeight) || singleEntryContext.details.end) {
						// seems we are at the end, no need to go any further
						singleEntryContext.details.end = singleEntryContext.details.end;

						return; // (singleEntryContext.details, ResultsState.OK);
					}
				}

				// ok, lets start the sync process
				singleEntryContext.syncManifest = new BlockFilesetSyncManifest();

				singleEntryContext.syncManifest.Key = singleEntryContext.details.Id;

				// lets generate the file map
				foreach(var channel in singleEntryContext.details.nextBlockSize.SlicesInfo) {
					singleEntryContext.syncManifest.Files.Add(channel.Key, new DataSlice {Length = channel.Value.Length});
				}

				this.GenerateSyncManifestStructure<BlockFilesetSyncManifest, BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest.BlockSyncingDataSlice>(singleEntryContext.syncManifest);

				// save it to keep our state
				this.CreateBlockSyncManifest(singleEntryContext.syncManifest);
			}

			string syncRate = this.rateCalculator.CalculateSyncingRate(this.ChainStateProvider.PublicBlockHeight - this.ChainStateProvider.DownloadBlockHeight);
			
			Log.Information($"Estimated time to blockchain sync completion: {syncRate}");
			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.BlockchainSyncUpdate(singleEntryContext.details.Id, this.ChainStateProvider.PublicBlockHeight, syncRate));

			singleEntryContext.blockFetchAttemptCounter = 1;
			singleEntryContext.blockFetchAttempt = BlockFetchAttemptTypes.Attempt1;

			// keep the statistics on this block
			this.BlockContexts.Enqueue(singleEntryContext);

			while(true) {
				this.CheckShouldStopThrow();

				bool success = false;

				try {
					Log.Verbose($"Fetching block data, attempt {singleEntryContext.blockFetchAttemptCounter}");

					if(singleEntryContext.syncManifest.IsComplete) {
						this.ResetBlockSyncManifest(singleEntryContext.syncManifest);
					}

					// lets get the block bytes
					var nextBlockPeerDetails = this.FetchPeerBlockData(singleEntryContext, true);

					if(!nextBlockPeerDetails.results.Any()) {
						// we got no valuable results, we must get more peers.
						throw new NoSyncingConnectionsException();
					}

					// and the consensus on the results
					nextBlockPeerSpecs = this.GetBlockInfoConsensus(nextBlockPeerDetails.results);

					success = true;
				} catch(NoSyncingConnectionsException e) {
					throw;
				} catch(Exception e) {
					Log.Error(e, "Failed to fetch block data. might try again...");
				}

				if(!success) {
					Log.Error("Failed to fetch block data. we tried all the attempts we could and it still failed. this is critical. we may try again.");

					// well, thats not great, we have to try again if we can
					singleEntryContext.blockFetchAttempt += 1;
					singleEntryContext.blockFetchAttemptCounter += 1;

					if(singleEntryContext.blockFetchAttempt == BlockFetchAttemptTypes.Overflow) {

						// thats it, we tried all we could and we are still failing, this is VERY serious and we kill the sync
						throw new AttemptsOverflowException("Failed to sync, maximum amount of block sync reached. this is very critical.");
					}
				} else {
					// we are done
					break;
				}
			}

			if(this.BlockContexts.Count > MAXIMUM_CONTEXT_HISTORY) {
				// dequeue a previous context
				this.BlockContexts.Dequeue();

				//TODO: add some higher level analytics
			}

			Log.Information($"Block {blockId} has been downloaded successfully");

		}

		/// <summary>
		///     Here we verify a failed download and perhaps find the culprit and cull it (them)
		/// </summary>
		/// <param name="nextBlockPeerDetails"></param>
		/// <param name="singleEntryContext"></param>
		/// <exception cref="NoSyncingConnectionsException"></exception>
		/// <exception cref="WorkflowException"></exception>
		protected void CompleteBlockSliceVerification(BlockFilesetSyncManifest syncingManifest, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {
			// ok, we tried twice and had invalid data. lets try to determine who is at fault

			var sliceHahsesSet = this.FetchPeerBlockSliceHashes(syncingManifest.Key, syncingManifest.Slices.Select(s => s.fileSlices.ToDictionary(e => e.Key, e => (int) e.Value.Length)).ToList(), connections);

			if(!sliceHahsesSet.results.Any()) {
				// we got no valuable results, we must get more peers.
				throw new NoSyncingConnectionsException();
			}

			var peersToRemove = new Dictionary<Guid, PeerConnection>();
			var syncingConnections = connections.GetSyncingConnections();

			// ok, anybody that did not answer is auto banned
			var keys = sliceHahsesSet.results.Keys.ToList();

			foreach(var missing in syncingConnections.Where(c => !keys.Contains(c.PeerConnection.ClientUuid))) {
				peersToRemove.Add(missing.PeerConnection.ClientUuid, missing.PeerConnection);
			}

			// ok, time to find out who says the truth

			var peerTopHashes = sliceHahsesSet.results.Select(e => e.Value).ToList();

			(int result, ConsensusUtilities.ConsensusType concensusType) topHashConsensusSet = ConsensusUtilities.GetConsensus(peerTopHashes, a => a.topHash);

			if((topHashConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Single) || (topHashConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Split)) {
				// this is completely unusable, we should remove all connections
				foreach(var connection in syncingConnections) {
					peersToRemove.Add(connection.PeerConnection.ClientUuid, connection.PeerConnection);
				}
			} else {
				// this is the consensus top hash
				int topHashConsensus = topHashConsensusSet.result;

				// let's make our own
				HashNodeList topNodes = new HashNodeList();

				foreach(int hash in syncingManifest.Slices.Select(s => s.Hash)) {
					topNodes.Add(hash);
				}

				int localTopHash = HashingUtils.HashxxTree32(topNodes);

				if(topHashConsensus != localTopHash) {
					// lets see if we figure out who is lieing here
					var faulty = sliceHahsesSet.results.Where(p => p.Value.topHash != topHashConsensus).ToList();

					if(faulty.Any()) {
						// lets remove these peers

						foreach(var faultyPeer in faulty) {

							var peerConnection = syncingConnections.SingleOrDefault(c => c.PeerConnection.ClientUuid == faultyPeer.Key);

							if((peerConnection != null) && !peersToRemove.ContainsKey(faultyPeer.Key)) {
								peersToRemove.Add(faultyPeer.Key, peerConnection.PeerConnection);
							}
						}
					}
				}

				// time to analyze who is lieing
				var slicesSets = sliceHahsesSet.results.ToDictionary(e => e.Key, e => e.Value.sliceHashes);

				(int result, ConsensusUtilities.ConsensusType concensusType) sliceCountConsensusSet = ConsensusUtilities.GetConsensus(slicesSets, a => a.Value.Count);

				if((sliceCountConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Single) || (sliceCountConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Split)) {
					// this is completely unusable, we should remove all connections
					foreach(var connection in syncingConnections) {
						connections.AddConnectionStrike(connection.PeerConnection, ConnectionSet.ConnectionStrikeset.RejectionReason.Banned);
					}

					return;
				}

				{
					var faulty = sliceHahsesSet.results.Where(p => p.Value.sliceHashes.Count != sliceCountConsensusSet.result).ToList();

					// lets remove these peers
					foreach(var faultyPeer in faulty) {

						var peerConnection = syncingConnections.SingleOrDefault(c => c.PeerConnection.ClientUuid == faultyPeer.Key);

						if((peerConnection != null) && !peersToRemove.ContainsKey(faultyPeer.Key)) {
							peersToRemove.Add(faultyPeer.Key, peerConnection.PeerConnection);
						}
					}
				}

				// now we check each entries in the slices to see who is lieing with the hashes
				for(int i = 0; i < sliceCountConsensusSet.result; i++) {

					(int result, ConsensusUtilities.ConsensusType concensusType) sliceEntryConsensusSet = ConsensusUtilities.GetConsensus(slicesSets, a => i < a.Value.Count ? a.Value[i] : 0);

					if((sliceEntryConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Single) || (sliceEntryConsensusSet.concensusType == ConsensusUtilities.ConsensusType.Split)) {
						// this is completely unusable, we should remove all connections
						foreach(var connection in syncingConnections) {
							connections.AddConnectionStrike(connection.PeerConnection, ConnectionSet.ConnectionStrikeset.RejectionReason.Banned);
						}

						return;
					}

					var faulty = sliceHahsesSet.results.Where(p => (p.Value.sliceHashes.Count <= i) || (p.Value.sliceHashes[i] != sliceEntryConsensusSet.result)).ToList();

					// lets remove these peers
					foreach(var faultyPeer in faulty) {

						var peerConnection = syncingConnections.SingleOrDefault(c => c.PeerConnection.ClientUuid == faultyPeer.Key);

						if((peerConnection != null) && !peersToRemove.ContainsKey(faultyPeer.Key)) {
							peersToRemove.Add(faultyPeer.Key, peerConnection.PeerConnection);
						}
					}

					// lets also compare with the slice hash we had received during the last block query request
					BlockFilesetSyncManifest.BlockSyncingDataSlice lastSliceInfoEntry = syncingManifest.Slices[i];

					if(lastSliceInfoEntry.Hash != sliceEntryConsensusSet.result) {
						// ok, they lied! lets add this peer too
						var peerConnection = syncingConnections.SingleOrDefault(c => c.PeerConnection.ClientUuid == lastSliceInfoEntry.ClientGuid);

						if((peerConnection != null) && !peersToRemove.ContainsKey(lastSliceInfoEntry.ClientGuid)) {
							peersToRemove.Add(lastSliceInfoEntry.ClientGuid, peerConnection.PeerConnection);
						}
					}
				}
			}

			// ban these peers!
			//TODO: any other logging we want to do on evil peers?
			foreach(var peer in peersToRemove) {
				connections.AddConnectionStrike(peer.Value, ConnectionSet.ConnectionStrikeset.RejectionReason.Banned);
			}

		}

		/// <summary>
		/// </summary>
		/// <param name="singleEntryContext"></param>
		/// <param name="includeBlockDetails">We dotn always want the actual block details, mostly when we load from local cache</param>
		/// <param name="considerPublicChainHeigthInConnection">
		///     In certain cases, especially when we load blocks from cache for a while, our knowledge of peer's public block
		///     height can get stale.
		///     if this parameter is false, we will pick all connections, regardless of their last know public chain height,
		///     because it is probably stale and may exclude still potentially valid sharing partners
		/// </param>
		/// <returns></returns>
		protected (Dictionary<Guid, PeerBlockSpecs> results, ResultsState state) FetchPeerBlockInfo(BlockSingleEntryContext singleEntryContext, bool includeBlockDetails, bool considerPublicChainHeigthInConnection) {
			var infoParameters = new FetchInfoParameter<BlockChannelsInfoSet<DataSliceSize>, DataSliceSize, BlockId, BlockChannelUtils.BlockChannelTypes, PeerBlockSpecs, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, BlockFilesetSyncManifest, BlockSingleEntryContext, BlockFilesetSyncManifest.BlockSyncingDataSlice>();

			infoParameters.singleEntryContext = singleEntryContext;
			infoParameters.id = singleEntryContext.details.Id;

			infoParameters.generateInfoRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_BLOCK_INFO>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestBlockInfo(this.trigger.BaseHeader);

				requestMessage.Message.Id = infoParameters.singleEntryContext.details.Id;
				requestMessage.Message.IncludeBlockDetails = includeBlockDetails;

				return requestMessage;
			};

			infoParameters.validNextInfoFunc = (peerReply, missingRequestInfos, nextPeerDetails, peersWithNoNextEntry, peerConnection) => {

				if(peerReply.Message.Id <= 0) {
					if(peerReply.Message.HasBlockDetails && (peerReply.Message.BlockHash != null) && peerReply.Message.BlockHash.HasData) {
						return ResponseValidationResults.Invalid; // no block data is a major issue
					}

					if(peerReply.Message.HasBlockDetails && (peerReply.Message.SlicesSize.HighHeaderInfo.Length > 0)) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(peerReply.Message.ChainBlockHeight <= 0) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(peerReply.Message.HasBlockDetails && (peerReply.Message.SlicesSize.LowHeaderInfo.Length > 0)) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					// this guy says there is no more chain... we will consider it in our consensus
					PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

					// update the value with what they reported
					peerConnection.ReportedDiskBlockHeight = peerReply.Message.ChainBlockHeight;
					peerConnection.ReportedPublicBlockHeight = peerReply.Message.PublicBlockHeight;
					
					nextBlockSpecs.Id = 0;
					nextBlockSpecs.publicChainBlockHeight = peerReply.Message.PublicBlockHeight;
					nextBlockSpecs.hasBlockDetails = false;

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextBlockSpecs);

					// there will be no next block with this guy. we will remove him for now, we can try him again later
					peersWithNoNextEntry.Add(peerReply.Header.ClientId);
				}  else if(peerReply.Message.Id != singleEntryContext.details.Id) {

					if(peerReply.Message.Id == singleEntryContext.details.Id - 1) {
						return ResponseValidationResults.LatePreviousBlock; // that's a late message
					}
					
					return ResponseValidationResults.Invalid; // that's an illegal value
				} else if(!nextPeerDetails.ContainsKey(peerConnection.PeerConnection.ClientUuid)) {

					if(peerReply.Message.HasBlockDetails && ((peerReply.Message.BlockHash == null) || peerReply.Message.BlockHash.IsEmpty)) {
						return ResponseValidationResults.Invalid; // no block data is a major issue
					}

					if(peerReply.Message.HasBlockDetails && (peerReply.Message.SlicesSize.HighHeaderInfo.Length <= 0)) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(peerReply.Message.ChainBlockHeight <= 0) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(peerReply.Message.HasBlockDetails && (peerReply.Message.SlicesSize.LowHeaderInfo.Length <= 0)) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					// now we record what the peer says the next block will be like for consensus establishment
					PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

					nextBlockSpecs.Id = peerReply.Message.Id;
					nextBlockSpecs.publicChainBlockHeight = peerReply.Message.PublicBlockHeight;

					// update the value with what they reported
					peerConnection.ReportedDiskBlockHeight = peerReply.Message.ChainBlockHeight;
					peerConnection.ReportedPublicBlockHeight = peerReply.Message.PublicBlockHeight;

					nextBlockSpecs.hasBlockDetails = peerReply.Message.HasBlockDetails;

					if(nextBlockSpecs.hasBlockDetails) {
						nextBlockSpecs.nextBlockHash.Entry = peerReply.Message.BlockHash.Entry;
						nextBlockSpecs.nextBlockSize = peerReply.Message.SlicesSize;
					}

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextBlockSpecs);
				}

				foreach(var blockInfo in missingRequestInfos.Where(s => s.connection.PeerConnection.ClientUuid == peerConnection.PeerConnection.ClientUuid)) {
					// all good, keep the reply for later
					blockInfo.responseMessage = peerReply.Message;
				}

				return ResponseValidationResults.Valid;
			};

			infoParameters.selectUsefulConnections = connections => {

				if(infoParameters.singleEntryContext.details.Id == 1) {
					// genesis can use everyone since everyone has the genesis block
					return connections.GetSyncingConnections();
				}

				if(this.UseAllBlocks) {

					var selectedConnections = connections.GetSyncingConnections().Where(c => {

						// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
						return (!considerPublicChainHeigthInConnection || c.ReportedDiskBlockHeight >= infoParameters.singleEntryContext.details.Id) && (c.TriggerResponse.Message.ShareType.AllBlocks || (c.TriggerResponse.Message.EarliestBlockHeight <= infoParameters.singleEntryContext.details.Id));
					}).ToList();

					return selectedConnections;
				}

				// get the ones ahead of us
				var selectedFinalConnections = connections.GetSyncingConnections().Where(c => {

					// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
					return !considerPublicChainHeigthInConnection || c.ReportedDiskBlockHeight >= infoParameters.singleEntryContext.details.Id;
				}).ToList();

				return selectedFinalConnections;
			};

			return this.FetchPeerInfo(infoParameters);
		}

		/// <summary>
		///     In extreme cases, we need to know who is lieing when getting invalid block data. here, we query the hashes of the
		///     slices from every peer, to see hwo is right and wrong
		/// </summary>
		/// <param name="singleEntryContext"></param>
		/// <returns></returns>
		protected (Dictionary<Guid, (List<int> sliceHashes, int topHash)> results, ResultsState state) FetchPeerBlockSliceHashes(long blockId, List<Dictionary<BlockChannelUtils.BlockChannelTypes, int>> slices, ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connectionsSet) {
			var infoParameters = new FetchSliceHashesParameter<BlockChannelsInfoSet<DataSliceSize>, DataSliceSize, BlockId, BlockChannelUtils.BlockChannelTypes, (List<int> sliceHashes, int topHash), REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES>();

			infoParameters.id = blockId;
			infoParameters.Connections = connectionsSet;

			infoParameters.generateInfoRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_BLOCK_SLICE_HASHES>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestBlockSliceHashes(this.trigger.BaseHeader);

				requestMessage.Message.Id = blockId;

				foreach(var sliceEntry in slices) {
					var channels = new Dictionary<BlockChannelUtils.BlockChannelTypes, int>();

					foreach(var channel in sliceEntry) {

						channels.Add(channel.Key, channel.Value);
					}

					requestMessage.Message.Slices.Add(channels);
				}

				return requestMessage;
			};

			infoParameters.validNextInfoFunc = (peerReply, missingRequestInfos, nextPeerDetails, peersWithNoNextEntry, peerConnection) => {

				if((peerReply.Message.Id <= 0) || !peerReply.Message.SliceHashes.Any() || (peerReply.Message.SlicesHash == 0)) {

					// this guy says there is nothing
					PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

					// update the value with what they reported
					nextBlockSpecs.Id = 0;
					nextBlockSpecs.hasBlockDetails = false;

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, (new List<int>(), 0));

					// there will be no next block with this guy. we will remove him for now, we can try him again later
					peersWithNoNextEntry.Add(peerReply.Header.ClientId);
				}else if(peerReply.Message.Id != blockId) {

					if(peerReply.Message.Id == blockId - 1) {
						return ResponseValidationResults.LatePreviousBlock; // that's a late message
					}
				
					return ResponseValidationResults.Invalid; // that's an illegal value
					
				} else if(!nextPeerDetails.ContainsKey(peerConnection.PeerConnection.ClientUuid)) {

					// now we record what the peer says the next block will be like for consensus establishment
					PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

					nextBlockSpecs.Id = peerReply.Message.Id;

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, (peerReply.Message.SliceHashes, peerReply.Message.SlicesHash));
				}

				var blockInfo = missingRequestInfos.Single(s => s.connection.PeerConnection.ClientUuid == peerConnection.PeerConnection.ClientUuid);

				// all good, keep the reply for later
				blockInfo.responseMessage = peerReply.Message;

				return ResponseValidationResults.Valid;
			};

			infoParameters.selectUsefulConnections = connections => {

				if(blockId == 1) {
					// genesis can use everyone since everyone has the genesis block
					return connections.GetSyncingConnections();
				}

				if(this.UseAllBlocks) {

					var selectedConnections = connections.GetSyncingConnections().Where(c => {

						// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
						return (c.ReportedDiskBlockHeight >= blockId) && (c.TriggerResponse.Message.ShareType.AllBlocks || (c.TriggerResponse.Message.EarliestBlockHeight <= blockId));
					}).ToList();

					return selectedConnections;
				}

				// get the ones ahead of us
				var selectedFinalConnections = connections.GetSyncingConnections().Where(c => {

					// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
					return c.ReportedDiskBlockHeight >= blockId;
				}).ToList();

				return selectedFinalConnections;
			};

			return this.FetchPeerSliceHashes<BlockChannelsInfoSet<DataSliceSize>, DataSliceSize, BlockId, BlockChannelUtils.BlockChannelTypes, (List<int> sliceHashes, int topHash), REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(infoParameters);
		}

		protected (Dictionary<Guid, PeerBlockSpecs> results, ResultsState state) FetchPeerBlockData(BlockSingleEntryContext singleBlockContext, bool queryNextInfo) {

			var parameters = new FetchDataParameter<BlockChannelsInfoSet<DataSliceInfo>, DataSliceInfo, BlockChannelsInfoSet<DataSlice>, DataSlice, BlockId, BlockChannelUtils.BlockChannelTypes, PeerBlockSpecs, REQUEST_BLOCK, SEND_BLOCK, BlockFilesetSyncManifest, BlockSingleEntryContext, ChannelsEntries<SafeArrayHandle>, BlockFilesetSyncManifest.BlockSyncingDataSlice>();
			parameters.id = singleBlockContext.details.Id;

			parameters.generateMultiSliceDataRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_BLOCK>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestBlock(this.trigger.BaseHeader);

				requestMessage.Message.Id = parameters.singleEntryContext.details.Id;

				return requestMessage;
			};

			parameters.selectUsefulConnections = connections => {

				if(parameters.singleEntryContext.details.Id == 1) {
					// genesis can use everyone since everyone has the genesis block
					return connections.GetSyncingConnections();
				}

				//TODO: review all this
				if(this.UseAllBlocks) {

					return connections.GetSyncingConnections().Where(c => {

						// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
						return (c.ReportedDiskBlockHeight >= parameters.singleEntryContext.details.Id) && (c.TriggerResponse.Message.ShareType.AllBlocks || (c.TriggerResponse.Message.EarliestBlockHeight <= parameters.singleEntryContext.details.Id));
					}).ToList();
				}

				// get the ones ahead of us
				return connections.GetSyncingConnections().Where(c => {

					// here we select peers that can help us. They must store all blocks, be ahead of us and if they store partial chains, then the digest must be ahead too.
					return c.ReportedDiskBlockHeight >= parameters.singleEntryContext.details.Id;
				}).ToList();
			};

			parameters.validSlicesFunc = (peerReply, nextPeerDetails, dispatchingSlices, peersWithNoNextEntry, peerConnection) => {

				if(peerReply.Message.Id == 0) {
					return ResponseValidationResults.NoData; // they dont have the block, so we ignore it
				}

				if(peerReply.Message.ChainBlockHeight < peerReply.Message.Id) {
					return ResponseValidationResults.Invalid; // this is impossible, its a major lie
				}

				// in this case, peer is valid if it has a more advanced blockchain than us and can share it back.
				bool highDataEmpty = (peerReply.Message.Slices.HighHeaderInfo?.Data == null) || peerReply.Message.Slices.HighHeaderInfo.Data.IsEmpty;
				bool lowDataEmpty = (peerReply.Message.Slices.LowHeaderInfo?.Data == null) || peerReply.Message.Slices.LowHeaderInfo.Data.IsEmpty;
				bool contentDataEmpty = (peerReply.Message.Slices.ContentsInfo?.Data == null) || peerReply.Message.Slices.ContentsInfo.Data.IsEmpty;

				if(highDataEmpty && lowDataEmpty && contentDataEmpty) {
					return ResponseValidationResults.Invalid; // no block data is a major issue
				}

				var slice = dispatchingSlices.SingleOrDefault(s => s.requestMessage.Message.SlicesInfo.FileId == peerReply.Message.Slices.FileId);

				if(slice == null) {
					// we found no matching slice
					return ResponseValidationResults.Invalid;
				}

				foreach(var sliceInfo in peerReply.Message.Slices.SlicesInfo) {
					if(sliceInfo.Value.Data.Length != sliceInfo.Value.Length) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(sliceInfo.Value.Offset != slice.requestMessage.Message.SlicesInfo.SlicesInfo[sliceInfo.Key].Offset) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(sliceInfo.Value.Length != slice.requestMessage.Message.SlicesInfo.SlicesInfo[sliceInfo.Key].Length) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					if(sliceInfo.Value.Data.Length != slice.requestMessage.Message.SlicesInfo.SlicesInfo[sliceInfo.Key].Length) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}
				}

				if(peerReply.Message.HasNextInfo) {
					if(peerReply.Message.NextBlockHeight <= 0) {

						peerConnection.ReportedDiskBlockHeight = peerReply.Message.ChainBlockHeight;
						
						// this guy says there is no more chain... we will consider it in our consensus
						PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

						nextBlockSpecs.Id = 0;
						nextBlockSpecs.nextBlockHash.Entry = null;

						if(!nextPeerDetails.ContainsKey(peerConnection.PeerConnection.ClientUuid)) {
							nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextBlockSpecs);
						}

						// there will be no next block with this guy. we will remove him for now, we can try him again later
						peersWithNoNextEntry.Add(peerReply.Header.ClientId);
					} else if(peerReply.Message.NextBlockHeight != (parameters.singleEntryContext.details.Id + 1)) {

						if(peerReply.Message.Id == parameters.singleEntryContext.details.Id) {
							return ResponseValidationResults.LatePreviousBlock; // that's a late message
						}
				
						return ResponseValidationResults.Invalid; // that's an illegal value
					
					}else if(!nextPeerDetails.ContainsKey(peerConnection.PeerConnection.ClientUuid)) {

						if((peerReply.Message.NextBlockHash == null) || peerReply.Message.NextBlockHash.IsEmpty) {
							return ResponseValidationResults.Invalid; // no block data is a major issue
						}

						if(peerReply.Message.NextBlockChannelSizes.HighHeaderInfo.Length <= 0) {
							return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
						}
						
						peerConnection.ReportedDiskBlockHeight = peerReply.Message.ChainBlockHeight;

						// now we record what the peer says the next block will be like for consensus establishment
						PeerBlockSpecs nextBlockSpecs = new PeerBlockSpecs();

						nextBlockSpecs.Id = peerReply.Message.NextBlockHeight;
						nextBlockSpecs.nextBlockHash.Entry = peerReply.Message.NextBlockHash.Entry;
						nextBlockSpecs.nextBlockSize = peerReply.Message.NextBlockChannelSizes;

						nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextBlockSpecs);
					}
				}

				// all good, keep the reply for later
				slice.responseMessage = peerReply.Message;

				return ResponseValidationResults.Valid;
			};

			parameters.writeDataSlice = (slice, response) => {
				
				this.WriteBlockSyncSlice(parameters.singleEntryContext.syncManifest, slice);
			};

			parameters.updateSyncManifest = () => {
				this.UpdateBlockSyncManifest(parameters.singleEntryContext.syncManifest);
			};

			parameters.clearManifest = () => {
				this.CompleteBlockSyncManifest(singleBlockContext.details.Id);
			};

			parameters.prepareCompletedData = () => {
				BlockFilesetSyncManifest syncManifest = parameters.singleEntryContext.syncManifest;

				var dataChannels = new ChannelsEntries<SafeArrayHandle>();

				foreach(var file in syncManifest.Files) {
					dataChannels[file.Key] = this.LoadBlockSyncManifestFile(syncManifest, file.Key);
				}

				return dataChannels;
			};

			parameters.prepareFirstRunRequestMessage = message => {
				message.IncludeNextInfo = queryNextInfo;
			};

			parameters.processReturnMessage = (message, clientUuid, nextBlockPeerSpecs) => {

				// add the next block specs if applicable
				if(message.HasNextInfo && !nextBlockPeerSpecs.ContainsKey(clientUuid)) {

					PeerBlockSpecs specs = new PeerBlockSpecs();

					specs.Id = message.NextBlockHeight;
					specs.nextBlockHash.Entry = message.NextBlockHash.Entry;
					specs.nextBlockSize = message.NextBlockChannelSizes;

					nextBlockPeerSpecs.Add(clientUuid, specs);
				}

			};

			parameters.singleEntryContext = singleBlockContext;

			return this.FetchPeerData(parameters);
		}

		/// <summary>
		///     insert a block into the blockchain
		/// </summary>
		protected bool InsertBlockIntoChain(IDehydratedBlock dehydratedBlock, int blockFetchAttemptCounter) {
			bool valid = false;

			
			// ok, we have our block! :D
			
			try {
				// install the block, but ask for a wallet sync only when the block is a multiple of 10.
				bool performWalletSync = ((dehydratedBlock.BlockId.Value % 10) == 0) || (dehydratedBlock.BlockId.Value == this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight);

				valid = this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertBlock(dehydratedBlock.RehydratedBlock, dehydratedBlock, performWalletSync, false);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to insert block into the local blockchain. we may try again...");

				if(blockFetchAttemptCounter == 3) {

					// thats it, we tried enough. we  have to break
					throw new WorkflowException("Failed to insert block into the local blockchain.", ex);
				}
			}
			
			return valid;
		}

		protected void RequestWalletSync(bool async = true) {
			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((walletService, taskRoutingContext) => {

				walletService.SynchronizeWallet(true, false);
			});

			if(async) {
				this.DispatchTaskAsync(blockchainTask);
			} else {
				this.DispatchTaskSync(blockchainTask);
			}
		}

		/// <summary>
		///     determine if a manifest exists and its status by location
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public ChainDataProvider.BlockFilesetSyncManifestStatuses GetBlockSyncManifestStatus(BlockId blockId) {

			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestStatus(blockId);
		}

		public BlockFilesetSyncManifest LoadBlockSyncManifest(BlockId blockId) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(blockId);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return null;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(blockId);

			return this.LoadSyncManifest<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(path);
		}

		public SafeArrayHandle LoadBlockSyncManifestFile(BlockFilesetSyncManifest filesetSyncManifest, BlockChannelUtils.BlockChannelTypes key) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(filesetSyncManifest.Key);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return null;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(filesetSyncManifest.Key);

			return this.LoadSyncManifestFile<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(filesetSyncManifest, key, path);
		}

		public ChannelsEntries<SafeArrayHandle> LoadBlockSyncManifestChannels(BlockFilesetSyncManifest filesetSyncManifest) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(filesetSyncManifest.Key);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return null;
			}

			var channelsEntries = new ChannelsEntries<SafeArrayHandle>(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(filesetSyncManifest.Key);

			return channelsEntries.ConvertAll((band, entry) => this.LoadSyncManifestFile<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(filesetSyncManifest, band, path));
		}

		public void CreateBlockSyncManifest(BlockFilesetSyncManifest filesetSyncManifest) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(filesetSyncManifest.Key);

			this.CreateSyncManifest<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void UpdateBlockSyncManifest(BlockFilesetSyncManifest filesetSyncManifest) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(filesetSyncManifest.Key);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(filesetSyncManifest.Key);

			this.UpdateSyncManifest<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void WriteBlockSyncSlice(BlockFilesetSyncManifest filesetSyncManifest, BlockChannelsInfoSet<DataSlice> sliceData) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(filesetSyncManifest.Key);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				//nothing??
				throw new ApplicationException();
			}

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.Completed) {
				// already done
				throw new ApplicationException();
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(filesetSyncManifest.Key);

			this.WriteSyncSlice<BlockChannelsInfoSet<DataSlice>, BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, DataSlice, BlockFilesetSyncManifest.BlockSyncingDataSlice>(filesetSyncManifest, sliceData, path);
		}

		public void CompleteBlockSyncManifest(BlockId blockId) {
			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(blockId);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestCompletedFileName(blockId);

			if(!this.fileSystem.File.Exists(path)) {
				using(Stream stream = this.fileSystem.File.Create(path)) {
					// do nothing, file is empty
				}
			}
		}

		public void ClearBlockSyncManifest(BlockId blockId) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(blockId);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(blockId);

			this.ClearSyncManifest(path);

			path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestCompletedFileName(blockId);

			if(this.fileSystem.File.Exists(path)) {
				this.fileSystem.File.Delete(path);
			}

			path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestCacheFolder(blockId);

			if(this.fileSystem.Directory.Exists(path)) {
				this.fileSystem.Directory.Delete(path, true);
			}
		}

		protected void ResetBlockSyncManifest(BlockFilesetSyncManifest syncManifest) {

			ChainDataProvider.BlockFilesetSyncManifestStatuses status = this.GetBlockSyncManifestStatus(syncManifest.Key);

			if(status == ChainDataProvider.BlockFilesetSyncManifestStatuses.None) {
				return;
			}

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSyncManifestFileName(syncManifest.Key);

			this.ResetSyncManifest<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, BlockFilesetSyncManifest.BlockSyncingDataSlice>(syncManifest, path);

		}

		private enum LoadSources {
			NotLoaded,
			Gossip,
			Sync
		}

		protected class BlockSingleEntryContext : SingleEntryContext<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, PeerBlockSpecs, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, BlockFilesetSyncManifest.BlockSyncingDataSlice> {
		}

		protected class BlockSliceHashesSingleEntryContext : SingleEntryContext<BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest, (List<int> sliceHashes, int topHash), CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, BlockFilesetSyncManifest.BlockSyncingDataSlice> {
		}

		public class PeerBlockSpecs {
			public bool end;
			public bool hasBlockDetails;

			public BlockId Id = new BlockId();
			public readonly SafeArrayHandle nextBlockHash = SafeArrayHandle.Create();
			public BlockChannelsInfoSet<DataSliceSize> nextBlockSize = new BlockChannelsInfoSet<DataSliceSize>();
			public BlockId publicChainBlockHeight;
		}
	}
}