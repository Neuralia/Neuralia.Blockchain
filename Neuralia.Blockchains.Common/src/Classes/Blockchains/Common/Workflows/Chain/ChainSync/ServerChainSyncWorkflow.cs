using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Digest;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

// ReSharper disable ReplaceWithSingleAssignment.False

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync {
	public interface IServerChainSyncWorkflow : IServerWorkflow<IBlockchainEventsRehydrationFactory> {
	}

	public abstract class ServerChainSyncWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, CLOSE_CONNECTION, REQUEST_BLOCK, REQUEST_DIGEST, SEND_BLOCK, SEND_DIGEST, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES> : ServerChainWorkflow<CHAIN_SYNC_TRIGGER, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IClientChainSyncWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply
		where CLOSE_CONNECTION : FinishSync
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

		/// <summary>
		///     How many seconds do we wait for a return message. for a server, we may have to wait longer since the peer may be
		///     very busy downloading
		/// </summary>
		protected const int WAIT_NEXT_BLOCK_TIMEOUT = 700;

		public const int MAX_CACHE_ENTRIES = 3 * 10;

		/// <summary>
		///     Here we store the clients with which we have a sync workflow already. It helps ensure a peer will only have one
		///     sync at a time
		/// </summary>
		protected static readonly object ClientIdWorkflowExistsLocker = new object();

		protected static readonly ConcurrentDictionary<Guid, IWorkflow<IBlockchainEventsRehydrationFactory>> ActiveClientIds = new ConcurrentDictionary<Guid, IWorkflow<IBlockchainEventsRehydrationFactory>>();

		protected static readonly TimeSpan Cache_Entry_Lifespan = TimeSpan.FromSeconds(20);
		public static readonly object cacheLocker = new object();

		protected static readonly ConcurrentDictionary<BlockId, BlocksCacheEntry> DeliveryBlocksCache = new ConcurrentDictionary<BlockId, BlocksCacheEntry>();

		protected readonly NodeShareType ShareType;

		private bool shutdownRequest;

		public ServerChainSyncWorkflow(BlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> triggerMessage, PeerConnection peerConnectionn, CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, triggerMessage, peerConnectionn) {

			// this is a special workflow, and we make sure we are generous in waiting times, to accomodate everything that can happen
			//TODO: set this to 3 minute
			this.hibernateTimeoutSpan = TimeSpan.FromMinutes(3);
			this.ShareType = this.ChainConfiguration.NodeShareType();

			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;

			this.PeerUnique = true;
			this.IsLongRunning = true;
		}

		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.LongRunning;
		private bool IsBusy { get; set; }

		protected IChainStateProvider ChainStateProvider => this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;
		protected ChainConfigurations ChainConfiguration => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
		protected bool NetworkPaused => this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused;

		protected override async Task<bool> PerformWork(LockContext lockContext) {
			try {
				this.CheckShouldStopThrow();

				// check if this client already has an active sync workflow in progress
				if(this.ClientIdWorkflowExistsAdd()) {
					// this client already has a workflow, this is bad
					//TODO: log peer for having attempted another workflow, could be a DDOS attempt
					this.CentralCoordinator.Log.Warning($"A synchronization workflow already exists for peer {this.PeerConnection.ScopedAdjustedIp}");

					return false;
				}

				try {
					this.CentralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;

					await this.PerformServerWork().ConfigureAwait(false);
				} finally {
					this.CentralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;
				}
				
				return true;
			} 
			catch(OutOfMemoryException oex) {
				// thats bad, lets clear everything

				DeliveryBlocksCache.Clear();
				
				GC.Collect();
					
				throw;
			}

			finally {
				// clear it up, we are done. they can try again later
				this.RemoveClientIdWorkflow();
			}
		}

		protected virtual void PrepareHandshake(CHAIN_SYNC_TRIGGER trigger, BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverHandshake) {
			if(this.ChainStateProvider.ChainInception == DateTimeEx.MinValue) {
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Synching;
			} else if((trigger.ChainInception != DateTimeEx.MinValue) && (this.ChainStateProvider.ChainInception != trigger.ChainInception)) {
				Console.WriteLine($"{trigger.ChainInception.ToString("o")} and {this.ChainStateProvider.ChainInception.ToString("o")}");
				// this is a pretty serious error actually. it should always be the same for everybody
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Error;
			} else if(this.ChainStateProvider.DiskBlockHeight < trigger.DiskBlockHeight) {
				// well, we are behind in the blocks that we have, we can not help
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Synching;
			}
		}

		protected virtual bool CheckShouldStop() {
			return this.NetworkPaused || this.shutdownRequest || this.CheckCancelRequested();
		}

		protected void CheckShouldStopThrow() {
			if(this.CheckShouldStop()) {
				this.CancelTokenSource.Cancel();
				this.CancelToken.ThrowIfCancellationRequested();
			}
		}

		/// <summary>
		///     Ensure that we dont stop during a sync step if a shutdown has been requested
		/// </summary>
		/// <param name="beacons"></param>
		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {

			try {
				this.shutdownRequest = true;

				// ok, if this happens while we are syncing, we ask for a grace period until we are ready to clean exit
				if(this.IsBusy) {
					beacons.Add(Task.Run(() => {

						DateTime limit = DateTime.Now.AddSeconds(30);
						while(true) {
							if(!this.IsBusy || DateTime.Now > limit) {
								// we are ready to go
								break;
							}

							// we have to wait a little more
							Thread.Sleep(500);
						}
					}));
				}
			} catch {
				
			}
		}

		protected virtual async Task PerformServerWork() {

			if(this.CheckShouldStop()) {
				return;
			}

			try {
				LockContext lockContext = null;
				IChainSyncMessageFactory chainSyncMessageFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.GetChainSyncMessageFactory();

				// ok, we just received a trigger, lets examine it
				BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverHandshake = (BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY>) chainSyncMessageFactory.CreateSyncWorkflowTriggerServerReplySet(this.triggerMessage.Header);

				double graceStrikes = 6;
				double period = 20;
				IPMarshall.Instance.Quarantine(this.PeerConnection.NodeAddressInfo.AdjustedAddress, IPMarshall.QuarantineReason.TooManySyncRequests, DateTimeEx.CurrentTime.AddDays(1), $"More than {graceStrikes} sync requests from {this.PeerConnection.NodeAddressInfo} in less than {period} s", graceStrikes, TimeSpan.FromSeconds(period));

				if (IPMarshall.Instance.IsQuarantined(this.PeerConnection.NodeAddressInfo.AdjustedAddress))
				{
					this.CentralCoordinator.Log.Information($"Syncing request from peer {this.PeerConnection.ScopedAdjustedIp} is refused, the peer is now quarantined, now disconnecting...", this.CorrelationId);
					this.CentralCoordinator.BlockchainServiceSet.NetworkingService.ConnectionStore.RemoveConnection(this.PeerConnection);

					return;
				}
				
				this.CentralCoordinator.Log.Debug($"Received a syncing request from peer {this.PeerConnection.ScopedAdjustedIp}.", this.CorrelationId);

				
				CHAIN_SYNC_TRIGGER trigger = this.triggerMessage.Message;

				// if we are syncing or not synced yet, lets see if we are further ahead. if not, we must decline; we can't help
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Ok;

				this.PrepareHandshake(trigger, serverHandshake);

				//TODO: define here the conditions when the active chain is behind the one of the client

				// yup, we are behind the caller, we can't help them
				if(serverHandshake.Message.Status != ServerTriggerReply.SyncHandshakeStatuses.Ok) {
					// too bad, we are currently syncing too, or some other related error, we can't help
					await Send(serverHandshake).ConfigureAwait(false);

					return;
				}

				long diskBlockHeight1 = this.ChainStateProvider.DiskBlockHeight;

				// ok, getting here, it seems we can help them. we will send them our own information
				serverHandshake.Message.ChainInception = this.ChainStateProvider.ChainInception;
				serverHandshake.Message.DiskBlockHeight = diskBlockHeight1;
				serverHandshake.Message.DigestHeight = this.ChainStateProvider.DigestHeight;
				serverHandshake.Message.ShareType = this.ShareType;

				serverHandshake.Message.EarliestBlockHeight = 0;

				if(this.ShareType.OnlyBlocks || this.ShareType.HasDigestsAndBlocks || (this.ChainStateProvider.DigestHeight == 0)) {

					if(diskBlockHeight1 == 1) {
						serverHandshake.Message.EarliestBlockHeight = 1;
					} else if(diskBlockHeight1 > 1) {
						// in this case, the earliest block we have is the second one, since we skip the genesis which everyone has
						serverHandshake.Message.EarliestBlockHeight = 2;
					}
				} else if(this.ShareType.HasDigestsThenBlocks) {
					// if we have a digest and nothing above, thats what we have
					serverHandshake.Message.EarliestBlockHeight = this.ChainStateProvider.DigestBlockHeight;

					// but if we have more than the digest, then its always digest block +1
					if(diskBlockHeight1 > this.ChainStateProvider.DigestBlockHeight) {
						serverHandshake.Message.EarliestBlockHeight = this.ChainStateProvider.DigestBlockHeight + 1;
					}
				}

				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Ok;

				if(!await Send(serverHandshake).ConfigureAwait(false)) {
					this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

					return;
				}

				// now we start waiting for block requests... one at a time.
				while(true) {

					try {

						this.IsBusy = true;

						List<ITargettedMessageSet<IBlockchainEventsRehydrationFactory>> requestSets = null;

						DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(WAIT_NEXT_BLOCK_TIMEOUT);

						bool hasMessages = false;

						while(DateTime.Now <= timeout) {

							if(await this.CheckSyncShouldStop(chainSyncMessageFactory).ConfigureAwait(false)) {
								return;
							}

							requestSets = await WaitNetworkMessages(new[] {typeof(CLOSE_CONNECTION), typeof(REQUEST_BLOCK_INFO), typeof(REQUEST_BLOCK_SLICE_HASHES), typeof(REQUEST_BLOCK), typeof(REQUEST_DIGEST_INFO), typeof(REQUEST_DIGEST), typeof(REQUEST_DIGEST_FILE)}, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

							hasMessages = requestSets.Any();

							if(hasMessages) {
								break;
							}
						}

						if(hasMessages == false) {
							// we timed out it seems
							this.CentralCoordinator.Log.Verbose($"The sync with peer {this.PeerConnection.AdjustedIp} has timed out.");

							break;
						}

						foreach(ITargettedMessageSet<IBlockchainEventsRehydrationFactory> requestSet in requestSets) {

							if((requestSet == null) || requestSet.BaseMessage is CLOSE_CONNECTION) {
								// this is the end, the other peer wants to stop this or we timed out

								break;
							}

							if(await this.CheckSyncShouldStop(chainSyncMessageFactory).ConfigureAwait(false)) {
								return;
							}

							if(requestSet?.BaseMessage is REQUEST_BLOCK_INFO blockInfoRequestMessage) {
								this.CentralCoordinator.Log.Debug($"Sending block id {blockInfoRequestMessage.Id} info to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								BlockchainTargettedMessageSet<SEND_BLOCK_INFO> sendBlockInfoMessage = (BlockchainTargettedMessageSet<SEND_BLOCK_INFO>) chainSyncMessageFactory.CreateServerSendBlockInfo(this.triggerMessage.Header);

								IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainProvider = this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase;

								// make sure we get these two values atomically, so there is no insert happening at the same time chaning the values
								(long usableDiskBlocHeight, long usablePublicBlockHeight) = await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PerformAtomicChainHeightOperation<(long, long)>(lc => Task.FromResult((this.ChainStateProvider.DiskBlockHeight, this.ChainStateProvider.PublicBlockHeight)), lockContext).ConfigureAwait(false);

								if((blockInfoRequestMessage.Id > 0) && (blockInfoRequestMessage.Id <= usableDiskBlocHeight)) {

									sendBlockInfoMessage.Message.Id = blockInfoRequestMessage.Id;
									sendBlockInfoMessage.Message.ChainBlockHeight = usableDiskBlocHeight;

									// here we ensure that we offer only the blocks that are now unlocked for sync.
									sendBlockInfoMessage.Message.PublicBlockHeight = usablePublicBlockHeight;

									if(blockInfoRequestMessage.IncludeBlockDetails) {
										// set the block data
										(ChannelsEntries<int> sizes, SafeArrayHandle hash)? results = this.FetchBlockSize(blockInfoRequestMessage.Id);

										if(results.HasValue) {
											sendBlockInfoMessage.Message.HasBlockDetails = true;

											sendBlockInfoMessage.Message.BlockHash.Entry = results.Value.hash.Entry;

											sendBlockInfoMessage.Message.SlicesSize.FileId = 0;

											foreach((BlockChannelUtils.BlockChannelTypes key, int value) in results.Value.sizes.Entries) {
												sendBlockInfoMessage.Message.SlicesSize.SlicesInfo.Add(key, new DataSliceSize(value));
											}
										}
									}
								}

								if(!await Send(sendBlockInfoMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								// now we prefetch the next two blocks while we sent the response and we are in downtime
								BlocksCacheEntry entry = this.GetBlockEntry(blockInfoRequestMessage.Id + 1);

								if(entry != null) {
									this.GetBlockEntry(blockInfoRequestMessage.Id + 2);
								}

								//sendBlockInfoMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_BLOCK blockRequestMessage) {
								BlockchainTargettedMessageSet<SEND_BLOCK> sendBlockMessage = (BlockchainTargettedMessageSet<SEND_BLOCK>) chainSyncMessageFactory.CreateServerSendBlock(this.triggerMessage.Header);

								IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainProvider = this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase;

								// when requesting actual blocks, we always offer the real block height. they are too far ahead in the process to be lied to.
								(long usableDiskBlocHeight, long usablePublicBlockHeight) = await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PerformAtomicChainHeightOperation<(long, long)>(lc => Task.FromResult((this.ChainStateProvider.DiskBlockHeight, this.ChainStateProvider.PublicBlockHeight)), lockContext).ConfigureAwait(false);

								if((blockRequestMessage.Id == 0) || (blockRequestMessage.Id > usableDiskBlocHeight)) {

									// send a default empty message
									if(!await Send(sendBlockMessage).ConfigureAwait(false)) {
										this.CentralCoordinator.Log.Verbose($"Syncing with peer  {this.PeerConnection.ScopedAdjustedIp} is over. we dont have enough blocks.");

										return;
									}
								}

								this.CentralCoordinator.Log.Debug($"Sending block id {blockRequestMessage.Id} to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								sendBlockMessage.Message.Id = blockRequestMessage.Id;

								// lets send them our latest chain block, sicne it may have moved since
								sendBlockMessage.Message.ChainBlockHeight = usableDiskBlocHeight;
								sendBlockMessage.Message.PublicBlockHeight = usablePublicBlockHeight;

								ChannelsEntries<(int offset, int length)> offsets = new ChannelsEntries<(int offset, int length)>();

								sendBlockMessage.Message.Slices.FileId = blockRequestMessage.SlicesInfo.FileId;

								foreach((BlockChannelUtils.BlockChannelTypes key, DataSliceInfo value) in blockRequestMessage.SlicesInfo.SlicesInfo) {
									offsets[key] = ((int) value.Offset, (int) value.Length);
								}

								long nextBlockId = 0;
								long potentialNextBlockId = blockRequestMessage.Id + 1;

								if(blockRequestMessage.IncludeNextInfo && (potentialNextBlockId <= usableDiskBlocHeight)) {
									nextBlockId = potentialNextBlockId;
								}

								(ChannelsEntries<SafeArrayHandle> blockSlice, ChannelsEntries<int> nextBlockSize, SafeArrayHandle nextBlockHash) blockSlices = this.FetchBlockSlice(blockRequestMessage.Id, offsets, nextBlockId);

								foreach((BlockChannelUtils.BlockChannelTypes key, SafeArrayHandle value) in blockSlices.blockSlice.Entries) {

									if(blockRequestMessage.SlicesInfo.SlicesInfo.ContainsKey(key)) {
										DataSliceInfo sliceInfo = blockRequestMessage.SlicesInfo.SlicesInfo[key];
										long offset = sliceInfo.Offset;
										long length = sliceInfo.Length;

										sendBlockMessage.Message.Slices.SlicesInfo.Add(key, new DataSlice(length, offset, value));
									}
								}

								if(nextBlockId != 0) {
									// if we do have the block, then we send it's connection, otherwise we ignore it
									sendBlockMessage.Message.HasNextInfo = true;
									sendBlockMessage.Message.NextBlockHeight = nextBlockId;
									sendBlockMessage.Message.NextBlockHash.Entry = blockSlices.nextBlockHash.Entry;

									foreach(KeyValuePair<BlockChannelUtils.BlockChannelTypes, int> size in blockSlices.nextBlockSize.Entries) {
										sendBlockMessage.Message.NextBlockChannelSizes.SlicesInfo.Add(size.Key, new DataSliceSize(size.Value));
									}
								}

								if(!await Send(sendBlockMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								// now we prefetch the next two blocks while we sent the response and we are in downtime
								BlocksCacheEntry entry = this.GetBlockEntry(blockRequestMessage.Id + 1);

								if(entry != null) {
									this.GetBlockEntry(blockRequestMessage.Id + 2);
								}

								// remove our hook, we probably dont need it anymore
								this.UnhookCacheEntries(blockRequestMessage.Id);

								//sendBlockMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_BLOCK_SLICE_HASHES requestBlockSliceHashes) {

								BlockchainTargettedMessageSet<SEND_BLOCK_SLICE_HASHES> sendBlockMessage = (BlockchainTargettedMessageSet<SEND_BLOCK_SLICE_HASHES>) chainSyncMessageFactory.CreateServerSendBlockSliceHashes(this.triggerMessage.Header);

								long diskBlockHeight = this.ChainStateProvider.DiskBlockHeight;

								if((requestBlockSliceHashes.Id == 0) || (requestBlockSliceHashes.Id > diskBlockHeight)) {

									// send a default empty message
									if(!await Send(sendBlockMessage).ConfigureAwait(false)) {
										this.CentralCoordinator.Log.Verbose($"Syncing with peer  {this.PeerConnection.ScopedAdjustedIp} is over. we dont have enough blocks.");

										return;
									}
								}

								this.CentralCoordinator.Log.Verbose($"Sending block id {requestBlockSliceHashes.Id} slice hashes to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								sendBlockMessage.Message.Id = requestBlockSliceHashes.Id;

								// query and hash the slices
								ChannelsEntries<int> startingOffsets = new ChannelsEntries<int>();

								(List<int> sliceHashes, int hash)? blockSlices = this.FetchBlockSlicesHashes(requestBlockSliceHashes.Id, requestBlockSliceHashes.Slices.Select(s => {
									ChannelsEntries<(int offset, int length)> offsets = new ChannelsEntries<(int offset, int length)>();

									foreach((BlockChannelUtils.BlockChannelTypes key, int value) in s) {
										offsets[key] = (startingOffsets[key], value);

										startingOffsets[key] += value;
									}

									return offsets;
								}).ToList());

								if(blockSlices.HasValue) {
									sendBlockMessage.Message.SliceHashes.AddRange(blockSlices.Value.sliceHashes);
									sendBlockMessage.Message.SlicesHash = blockSlices.Value.hash;
								}

								if(!await Send(sendBlockMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								//sendBlockMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST_INFO requestDigestInfo) {

								this.CentralCoordinator.Log.Information($"Sending digest id {requestDigestInfo.Id} connection to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								BlockchainTargettedMessageSet<SEND_DIGEST_INFO> sendDigestInfoMessage = (BlockchainTargettedMessageSet<SEND_DIGEST_INFO>) chainSyncMessageFactory.CreateServerSendDigestInfo(this.triggerMessage.Header);

								int digestId = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;

								if(requestDigestInfo.Id == digestId) {
									int results = this.FetchDigestSize(digestId);

									sendDigestInfoMessage.Message.Id = digestId;
									sendDigestInfoMessage.Message.SlicesSize.FileId = 0;
									sendDigestInfoMessage.Message.SlicesSize.FileInfo.Length = results;
								}

								if(!await Send(sendDigestInfoMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								//sendDigestInfoMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST requestDigest) {

								this.CentralCoordinator.Log.Information($"Sending digest for id {requestDigest.Id} to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								BlockchainTargettedMessageSet<SEND_DIGEST> sendDigestMessage = (BlockchainTargettedMessageSet<SEND_DIGEST>) chainSyncMessageFactory.CreateServerSendDigest(this.triggerMessage.Header);

								int digestId = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;

								if(requestDigest.Id == digestId) {
									sendDigestMessage.Message.Id = digestId;
									sendDigestMessage.Message.Slices.FileId = requestDigest.SlicesInfo.FileId;
									sendDigestMessage.Message.Slices.FileInfo.Data.Entry = this.FetchDigest(requestDigest.Id, (int) requestDigest.SlicesInfo.FileInfo.Offset, (int) requestDigest.SlicesInfo.FileInfo.Length).Entry;

									sendDigestMessage.Message.Slices.FileInfo.Offset = requestDigest.SlicesInfo.FileInfo.Offset;
									sendDigestMessage.Message.Slices.FileInfo.Length = requestDigest.SlicesInfo.FileInfo.Length;
								}

								if(!await Send(sendDigestMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								//sendDigestMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST_FILE requestDigestFile) {

								this.CentralCoordinator.Log.Information($"Sending digest id {requestDigestFile.Id} to peer {this.PeerConnection.ScopedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								BlockchainTargettedMessageSet<SEND_DIGEST_FILE> sendDigestFileMessage = (BlockchainTargettedMessageSet<SEND_DIGEST_FILE>) chainSyncMessageFactory.CreateServerSendDigestFile(this.triggerMessage.Header);

								sendDigestFileMessage.Message.Id = requestDigestFile.Id;

								sendDigestFileMessage.Message.Slices.FileId = requestDigestFile.SlicesInfo.FileId;

								foreach((ChannelFileSetKey key, DataSliceInfo value) in requestDigestFile.SlicesInfo.SlicesInfo) {

									SafeArrayHandle data = this.FetchDigestFile(key.ChannelId, key.IndexId, key.FileId, key.FilePart, (int) value.Offset, (int) value.Length);

									sendDigestFileMessage.Message.Slices.SlicesInfo.Add(key, new DataSlice(value.Length, value.Offset, data));
								}

								if(!await Send(sendDigestFileMessage).ConfigureAwait(false)) {
									this.CentralCoordinator.Log.Verbose($"Connection with peer  {this.PeerConnection.ScopedAdjustedIp} was terminated");

									return;
								}

								//sendDigestFileMessage.BaseMessage.Dispose();
							}

							//requestSet?.BaseMessage.Dispose();

							// ok, this should be the end for this block. we loop and the client will tell us if they want more blocks, or if they are nice, to stop. worst case, we will timeout.
						}
					} finally {
						this.IsBusy = false;
					}
				}
			} finally {
				this.UnhookAllCacheEntries();

				// thats it, we are done :)
				this.CentralCoordinator.Log.Debug($"Finished handling synchronization for peer {this.PeerConnection.ScopedAdjustedIp}.");

			}
		}

		/// <summary>
		///     check if the sync should stop, and send a message that we are stopping
		/// </summary>
		/// <param name="chainSyncMessageFactory"></param>
		/// <returns></returns>
		protected async Task<bool> CheckSyncShouldStop(IChainSyncMessageFactory chainSyncMessageFactory) {
			if(this.CheckShouldStop()) {

				BlockchainTargettedMessageSet<CLOSE_CONNECTION> closeMessage = (BlockchainTargettedMessageSet<CLOSE_CONNECTION>) chainSyncMessageFactory.CreateSyncWorkflowFinishSyncSet(this.triggerMessage.BaseHeader);

				closeMessage.Message.Reason = FinishSync.FinishReason.Busy;

				try {
					// lets be nice, lets inform them that we will close the connection for this workflow
					await Send(closeMessage).ConfigureAwait(false);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Debug(ex, "Failed to close peer connection but workflow is over.");
				}

				return true;
			}

			return false;
		}

		/// <summary>
		///     remove hooks on all blocks
		/// </summary>
		protected virtual void UnhookAllCacheEntries() {
			try {

				KeyValuePair<BlockId, BlocksCacheEntry>[] lowerBlocks = DeliveryBlocksCache.Where(e => e.Value.Hooks.ContainsKey(this.PeerConnection.ClientUuid)).ToArray();

				foreach(KeyValuePair<BlockId, BlocksCacheEntry> entry in lowerBlocks) {
					try {
						entry.Value.Hooks.RemoveSafe(this.PeerConnection.ClientUuid);
					} catch {
						// do nothing
					}
				}
			} catch {
				// do nothing
			}

			this.ClearBlockCache();
		}

		/// <summary>
		///     remove hooks on blocks we dont really need anymore
		/// </summary>
		/// <param name="id"></param>
		protected virtual void UnhookCacheEntries(long id) {

			try {

				KeyValuePair<BlockId, BlocksCacheEntry>[] lowerBlocks = DeliveryBlocksCache.Where(e => (e.Key < id) && e.Value.Hooks.ContainsKey(this.PeerConnection.ClientUuid)).ToArray();

				foreach(KeyValuePair<BlockId, BlocksCacheEntry> entry in lowerBlocks) {
					try {
						entry.Value.Hooks.RemoveSafe(this.PeerConnection.ClientUuid);
					} catch {
						// do nothing
					}
				}

			} catch {
				// do nothing
			}
		}

		protected virtual BlocksCacheEntry GetBlockEntry(long id) {

			if(id > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
				return null;
			}

			BlocksCacheEntry cacheEntry = null;

			if(!DeliveryBlocksCache.ContainsKey(id)) {

				ChannelsEntries<SafeArrayHandle> blockData = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockChannels(id);

				if(blockData == null) {
					return null;
				}

				cacheEntry = new BlocksCacheEntry();
				cacheEntry.BlockData = blockData;
				cacheEntry.Timeout = DateTime.Now + Cache_Entry_Lifespan;
				(ChannelsEntries<int> sizes, SafeArrayHandle hash)? entry = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSizeAndHash(id);

				if(entry.HasValue) {
					cacheEntry.BlockSize = entry.Value.sizes;
					cacheEntry.BlockHash = entry.Value.hash;
				}

				DeliveryBlocksCache.AddSafe(id, cacheEntry);
			}

			cacheEntry = DeliveryBlocksCache[id];
			cacheEntry.Timeout = DateTime.Now + Cache_Entry_Lifespan;

			// add our hook
			if(!cacheEntry.Hooks.ContainsKey(this.PeerConnection.ClientUuid)) {
				cacheEntry.Hooks.AddSafe(this.PeerConnection.ClientUuid, false);
			}

			this.ClearBlockCache();

			return cacheEntry;
		}

		protected void ClearBlockCache() {
			try {
				lock(cacheLocker) {
					if(DeliveryBlocksCache.Count > MAX_CACHE_ENTRIES) {
						//  gotta clean up a bit
						try {

							foreach(KeyValuePair<BlockId, BlocksCacheEntry> entry in DeliveryBlocksCache.Where(e => (e.Value.Timeout < DateTime.Now) || !e.Value.Hooks.Any())) {
								DeliveryBlocksCache.RemoveSafe(entry.Key);
							}

						} catch {
							// do nothing
						}
					}
				}
			} catch {
				// do nothing
			}
		}

		/// <summary>
		///     fetch block data either from the cache, or from disk
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected virtual ChannelsEntries<SafeArrayHandle> FetchBlockData(long id) {

			return this.GetBlockEntry(id).BlockData;
		}

		/// <summary>
		///     Get both a slice of block and the size of the next one
		/// </summary>
		/// <param name="id"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		protected virtual (ChannelsEntries<SafeArrayHandle> blockSlice, ChannelsEntries<int> nextBlockSize, SafeArrayHandle nextBlockHash) FetchBlockSlice(long id, ChannelsEntries<(int offset, int length)> offsets, long nextBlockId) {

			ChannelsEntries<SafeArrayHandle> blockData = this.FetchBlockData(id);
			(ChannelsEntries<int> sizes, SafeArrayHandle hash)? nextBlock = null;

			ChannelsEntries<SafeArrayHandle> slices = new ChannelsEntries<SafeArrayHandle>(offsets.EnabledChannels);

			offsets.RunForAll((flag, offset) => {

				(int i, int length) = offset;
				slices[flag] = (SafeArrayHandle)blockData[flag].Entry.Slice(i, length);
			});

			if(nextBlockId != 0) {
				nextBlock = this.FetchBlockSize(nextBlockId);
			}

			return (slices, nextBlock?.sizes, nextBlock?.hash);
		}

		/// <summary>
		///     Get the binary size of a block on disk
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected virtual (ChannelsEntries<int> sizes, SafeArrayHandle hash)? FetchBlockSize(long id) {

			BlocksCacheEntry blockEntry = this.GetBlockEntry(id);

			return (blockEntry.BlockSize, blockEntry.BlockHash);
		}

		protected virtual (List<int> sliceHashes, int hash)? FetchBlockSlicesHashes(long id, List<ChannelsEntries<(int offset, int length)>> slices) {

			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.BuildBlockSliceHashes(id, slices);

		}

		protected virtual int FetchDigestSize(int id) {
			// lets make sure our hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestHeaderSize(id);
		}

		protected virtual SafeArrayHandle FetchDigest(int id, int offset, int length) {

			// lets make sure hour hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestHeaderArchiveData(id, offset, length);
		}

		protected virtual SafeArrayHandle FetchDigestFile(DigestChannelType channelId, int indexId, int fileId, uint filePart, long offset, int length) {

			// lets make sure hour hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestFile(channelId, indexId, fileId, filePart, offset, length);
		}

		protected virtual void RemoveClientIdWorkflow() {

			lock(ClientIdWorkflowExistsLocker) {
				ActiveClientIds.RemoveSafe(this.PeerConnection.ClientUuid);
			}
		}

		/// <summary>
		///     Check if the client Scope is already using a sync workflow. If now, we register it as such
		/// </summary>
		/// <returns></returns>
		protected virtual bool ClientIdWorkflowExistsAdd() {

			lock(ClientIdWorkflowExistsLocker) {
				if(ActiveClientIds.ContainsKey(this.PeerConnection.ClientUuid) && !this.TestingMode) {

					IWorkflow<IBlockchainEventsRehydrationFactory> workflow = ActiveClientIds[this.PeerConnection.ClientUuid];

					if(!workflow.IsCompleted) {
						return true; // its there, we can't continue
					}

					// its fine, it is done, we can remove it and act like it was never there
					ActiveClientIds.RemoveSafe(this.PeerConnection.ClientUuid);
				}

				// lets add it now
				ActiveClientIds.AddSafe(this.PeerConnection.ClientUuid, this);
			}

			return false;

		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ServerChainSyncWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, CLOSE_CONNECTION, REQUEST_BLOCK, REQUEST_DIGEST, SEND_BLOCK, SEND_DIGEST, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES> otherWorkflow) {
				return this.triggerMessage.Header.OriginatorId == otherWorkflow.triggerMessage.Header.OriginatorId;
			}

			return base.CompareOtherPeerId(other);
		}

		protected class BlocksCacheEntry {
			public ConcurrentDictionary<Guid, bool> Hooks { get; } = new ConcurrentDictionary<Guid, bool>();
			public DateTime Timeout { get; set; }

			public ChannelsEntries<SafeArrayHandle> BlockData { get; set; }
			public ChannelsEntries<int> BlockSize { get; set; }
			public SafeArrayHandle BlockHash { get; set; }
		}
	}
}