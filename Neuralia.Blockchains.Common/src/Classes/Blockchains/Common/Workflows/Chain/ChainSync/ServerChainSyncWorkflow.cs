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
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
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

		/// <summary>
		///     Here we store the clients with which we have a sync workflow already. It helps ensure a peer will only have one
		///     sync at a time
		/// </summary>
		protected static readonly ConcurrentDictionary<Guid, IWorkflow<IBlockchainEventsRehydrationFactory>> activeClientIds = new ConcurrentDictionary<Guid, IWorkflow<IBlockchainEventsRehydrationFactory>>();

		protected readonly AppSettingsBase.BlockSavingModes blockchainSavingMode;

		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.LongRunning;
		
		private bool shutdownRequest = false;
		private bool IsBusy { get; set; }

		public ServerChainSyncWorkflow(BlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> triggerMessage, PeerConnection peerConnectionn, CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, triggerMessage, peerConnectionn) {

			// this is a special workflow, and we make sure we are generous in waiting times, to accomodate everything that can happen
			//TODO: set this to 3 minute
			this.hibernateTimeoutSpan = TimeSpan.FromMinutes(3);
			this.blockchainSavingMode = this.ChainConfiguration.BlockSavingMode;

			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;

			this.PeerUnique = true;
			this.IsLongRunning = true;
		}

		protected IChainStateProvider ChainStateProvider => this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;
		protected ChainConfigurations ChainConfiguration => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
		protected bool NetworkPaused => this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused;
		
		protected override void PerformWork() {
			try {
				this.CheckShouldCancel();

				// check if this client already has an active sync workflow in progress
				if(this.ClientIdWorkflowExistsAdd()) {
					// this client already has a workflow, this is bad
					//TODO: log peer for having attempted another workflow, could be a DDOS attempt
					Log.Warning($"A synchronization workflow already exists for peer {this.PeerConnection.ScoppedAdjustedIp}");

					return;
				}

				try {
					this.CentralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;
					
					this.PerformServerWork();
				} finally {
					this.CentralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;
				}
			} finally {
				// clear it up, we are done. they can try again later
				this.RemoveClientIdWorkflow();
			}
		}

		protected virtual void PrepareHandshake(CHAIN_SYNC_TRIGGER trigger, BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverHandshake) {
			if(this.ChainStateProvider.ChainInception == DateTime.MinValue) {
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Synching;
			} else if((trigger.ChainInception != DateTime.MinValue) && (this.ChainStateProvider.ChainInception != trigger.ChainInception)) {
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
		
		/// <summary>
		///     Ensure that we dont stop during a sync step if a shutdown has been requested
		/// </summary>
		/// <param name="beacons"></param>
		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {

			this.shutdownRequest = true;
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
		
		protected virtual void PerformServerWork() {

			if(this.CheckShouldStop()) {
				return;
			}

			try {
				IChainSyncMessageFactory chainSyncMessageFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.GetChainSyncMessageFactory();

				// ok, we just received a trigger, lets examine it
				BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> serverHandshake = (BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY>) chainSyncMessageFactory.CreateSyncWorkflowTriggerServerReplySet(this.triggerMessage.Header);
				Log.Information($"Received a syncing request from peer {this.PeerConnection.ScoppedAdjustedIp}.", this.CorrelationId);

				CHAIN_SYNC_TRIGGER trigger = this.triggerMessage.Message;

				// if we are syncing or not synced yet, lets see if we are further ahead. if not, we must decline; we can't help
				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Ok;

				this.PrepareHandshake(trigger, serverHandshake);

				//TODO: define here the conditions when the active chain is behind the one of the client

				// yup, we are behind the caller, we can't help them
				if(serverHandshake.Message.Status != ServerTriggerReply.SyncHandshakeStatuses.Ok) {
					// too bad, we are currently syncing too, or some other related error, we can't help
					this.Send(serverHandshake);

					return;
				}

				// ok, getting here, it seems we can help them. we will send them our own information
				serverHandshake.Message.ChainInception = this.ChainStateProvider.ChainInception;
				serverHandshake.Message.DiskBlockHeight = this.ChainStateProvider.DiskBlockHeight;
				serverHandshake.Message.DigestHeight = this.ChainStateProvider.DigestHeight;
				serverHandshake.Message.BlockSavingMode = this.blockchainSavingMode;

				serverHandshake.Message.EarliestBlockHeight = 0;

				if((this.blockchainSavingMode == AppSettingsBase.BlockSavingModes.BlockOnly) || (this.blockchainSavingMode == AppSettingsBase.BlockSavingModes.DigestAndBlocks) || (this.ChainStateProvider.DigestHeight == 0)) {
					if(this.ChainStateProvider.DiskBlockHeight == 1) {
						serverHandshake.Message.EarliestBlockHeight = 1;
					} else if(this.ChainStateProvider.DiskBlockHeight > 1) {
						// in this case, the earliest block we have is the second one, since we skip the genesis which everyone has
						serverHandshake.Message.EarliestBlockHeight = 2;
					}
				} else if(this.blockchainSavingMode == AppSettingsBase.BlockSavingModes.DigestsThenBlocks) {
					// if we have a digest and nothing above, thats what we have
					serverHandshake.Message.EarliestBlockHeight = this.ChainStateProvider.DigestBlockHeight;

					// but if we have more than the digest, then its always digest block +1
					if(this.ChainStateProvider.DiskBlockHeight > this.ChainStateProvider.DigestBlockHeight) {
						serverHandshake.Message.EarliestBlockHeight = this.ChainStateProvider.DigestBlockHeight + 1;
					}
				}

				serverHandshake.Message.Status = ServerTriggerReply.SyncHandshakeStatuses.Ok;

				if(!this.Send(serverHandshake)) {
					Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

					return;
				}

				// now we start waiting for block requests... one at a time.
				while(true) {
					
					try {

						this.IsBusy = true;

						List<ITargettedMessageSet<IBlockchainEventsRehydrationFactory>> requestSets = null;

						DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(WAIT_NEXT_BLOCK_TIMEOUT);

						bool hasMessages = false;

						while(DateTime.Now < timeout) {
						
							if(this.CheckShouldStop()) {
								
								var closeMessage = (BlockchainTargettedMessageSet<CLOSE_CONNECTION>) chainSyncMessageFactory.CreateSyncWorkflowFinishSyncSet(this.triggerMessage.BaseHeader);

								closeMessage.Message.Reason = FinishSync.FinishReason.Busy;

								try {
									// lets be nice, lets inform them that we will close the connection for this workflow
									this.Send(closeMessage);
								} catch(Exception ex) {
									Log.Error(ex, "Failed to close peer connection but workflow is over.");
								}
								return;
							}
							
							requestSets = this.WaitNetworkMessages(new[] {typeof(CLOSE_CONNECTION), typeof(REQUEST_BLOCK_INFO), typeof(REQUEST_BLOCK_SLICE_HASHES), typeof(REQUEST_BLOCK), typeof(REQUEST_DIGEST_INFO), typeof(REQUEST_DIGEST), typeof(REQUEST_DIGEST_FILE)}, TimeSpan.FromSeconds(2));

							hasMessages = requestSets.Any();

							if(hasMessages) {
								break;
							}
						}

						if(hasMessages == false) {
							// we timed out it seems
							Log.Verbose($"The sync with peer {this.PeerConnection.AdjustedIp} has timed out.");

							break;
						}

						foreach(var requestSet in requestSets) {

							if(requestSet == null || requestSet.BaseMessage is CLOSE_CONNECTION) {
								// this is the end, the other peer wants to stop this or we timed out

								break;
							}

							this.CheckShouldCancel();

							if(requestSet?.BaseMessage is REQUEST_BLOCK_INFO blockInfoRequestMessage) {
								Log.Verbose($"Sending block id {blockInfoRequestMessage.Id} info to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								var sendBlockInfoMessage = (BlockchainTargettedMessageSet<SEND_BLOCK_INFO>) chainSyncMessageFactory.CreateServerSendBlockInfo(this.triggerMessage.Header);

								if((blockInfoRequestMessage.Id > 0) && (blockInfoRequestMessage.Id <= this.ChainStateProvider.DiskBlockHeight)) {

									sendBlockInfoMessage.Message.Id = blockInfoRequestMessage.Id;
									sendBlockInfoMessage.Message.ChainBlockHeight = this.ChainStateProvider.DiskBlockHeight;
									sendBlockInfoMessage.Message.PublicBlockHeight = this.ChainStateProvider.PublicBlockHeight;

									if(blockInfoRequestMessage.IncludeBlockDetails) {
										// set the block data
										var results = this.FetchBlockSize(blockInfoRequestMessage.Id);

										sendBlockInfoMessage.Message.HasBlockDetails = true;

										sendBlockInfoMessage.Message.BlockHash.Entry = results.hash.Entry;

										sendBlockInfoMessage.Message.SlicesSize.FileId = 0;

										foreach(var channel in results.sizes.Entries) {
											sendBlockInfoMessage.Message.SlicesSize.SlicesInfo.Add(channel.Key, new DataSliceSize(channel.Value));
										}
									}
								}

								if(!this.Send(sendBlockInfoMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

									return;
								}

								//sendBlockInfoMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_BLOCK blockRequestMessage) {
								var sendBlockMessage = (BlockchainTargettedMessageSet<SEND_BLOCK>) chainSyncMessageFactory.CreateServerSendBlock(this.triggerMessage.Header);

								long diskBlockHeight = this.ChainStateProvider.DiskBlockHeight;

								if((blockRequestMessage.Id == 0) || (blockRequestMessage.Id > diskBlockHeight)) {

									// send a default empty message
									if(!this.Send(sendBlockMessage)) {
										Log.Verbose($"Syncing with peer  {this.PeerConnection.ScoppedAdjustedIp} is over. we dont have enough blocks.");

										return;
									}
								}

								Log.Verbose($"Sending block id {blockRequestMessage.Id} to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								sendBlockMessage.Message.Id = blockRequestMessage.Id;

								// lets send them our latest chain block, sicne it may have moved since
								sendBlockMessage.Message.ChainBlockHeight = diskBlockHeight;

								var offsets = new ChannelsEntries<(int offset, int length)>();

								sendBlockMessage.Message.Slices.FileId = blockRequestMessage.SlicesInfo.FileId;

								foreach(var channel in blockRequestMessage.SlicesInfo.SlicesInfo) {
									offsets[channel.Key] = ((int) channel.Value.Offset, (int) channel.Value.Length);
								}

								long nextBlockId = 0;
								long potentialNextBlockId = blockRequestMessage.Id + 1;

								if(blockRequestMessage.IncludeNextInfo && (potentialNextBlockId <= diskBlockHeight)) {
									nextBlockId = potentialNextBlockId;
								}

								var blockSlices = this.FetchBlockSlice(blockRequestMessage.Id, offsets, nextBlockId);

								foreach(var slice in blockSlices.blockSlice.Entries) {

									sendBlockMessage.Message.Slices.SlicesInfo.Add(slice.Key, new DataSlice(blockRequestMessage.SlicesInfo.SlicesInfo[slice.Key].Length, blockRequestMessage.SlicesInfo.SlicesInfo[slice.Key].Offset, slice.Value));
								}

								sendBlockMessage.Message.HasNextInfo = blockRequestMessage.IncludeNextInfo;

								if(nextBlockId != 0) {

									// if we do have the block, then we send it's connection, otherwise we ignore it
									sendBlockMessage.Message.NextBlockHeight = nextBlockId;
									sendBlockMessage.Message.NextBlockHash.Entry = blockSlices.nextBlockHash.Entry;

									foreach(var size in blockSlices.nextBlockSize.Entries) {
										sendBlockMessage.Message.NextBlockChannelSizes.SlicesInfo.Add(size.Key, new DataSliceSize(size.Value));
									}
								}

								if(!this.Send(sendBlockMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

									return;
								}

								//sendBlockMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_BLOCK_SLICE_HASHES requestBlockSliceHashes) {

								var sendBlockMessage = (BlockchainTargettedMessageSet<SEND_BLOCK_SLICE_HASHES>) chainSyncMessageFactory.CreateServerSendBlockSliceHashes(this.triggerMessage.Header);

								long diskBlockHeight = this.ChainStateProvider.DiskBlockHeight;

								if((requestBlockSliceHashes.Id == 0) || (requestBlockSliceHashes.Id > diskBlockHeight)) {

									// send a default empty message
									if(!this.Send(sendBlockMessage)) {
										Log.Verbose($"Syncing with peer  {this.PeerConnection.ScoppedAdjustedIp} is over. we dont have enough blocks.");

										return;
									}
								}

								Log.Verbose($"Sending block id {requestBlockSliceHashes.Id} slice hashes to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								sendBlockMessage.Message.Id = requestBlockSliceHashes.Id;

								// query and hash the slices
								var startingOffsets = new ChannelsEntries<int>();

								var blockSlices = this.FetchBlockSlicesHashes(requestBlockSliceHashes.Id, requestBlockSliceHashes.Slices.Select(s => {
									var offsets = new ChannelsEntries<(int offset, int length)>();

									foreach(var channel in s) {
										offsets[channel.Key] = (startingOffsets[channel.Key], channel.Value);

										startingOffsets[channel.Key] += channel.Value;
									}

									return offsets;
								}).ToList());

								if(blockSlices.HasValue) {
									sendBlockMessage.Message.SliceHashes.AddRange(blockSlices.Value.sliceHashes);
									sendBlockMessage.Message.SlicesHash = blockSlices.Value.hash;
								}

								if(!this.Send(sendBlockMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

									return;
								}

								//sendBlockMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST_INFO requestDigestInfo) {

								Log.Information($"Sending digest id {requestDigestInfo.Id} connection to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								var sendDigestInfoMessage = (BlockchainTargettedMessageSet<SEND_DIGEST_INFO>) chainSyncMessageFactory.CreateServerSendDigestInfo(this.triggerMessage.Header);

								int digestId = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;

								if(requestDigestInfo.Id == digestId) {
									int results = this.FetchDigestSize(digestId);

									sendDigestInfoMessage.Message.Id = digestId;
									sendDigestInfoMessage.Message.SlicesSize.FileId = 0;
									sendDigestInfoMessage.Message.SlicesSize.FileInfo.Length = results;
								}

								if(!this.Send(sendDigestInfoMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

									return;
								}

								//sendDigestInfoMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST requestDigest) {

								Log.Information($"Sending digest for id {requestDigest.Id} to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								var sendDigestMessage = (BlockchainTargettedMessageSet<SEND_DIGEST>) chainSyncMessageFactory.CreateServerSendDigest(this.triggerMessage.Header);

								int digestId = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;

								if(requestDigest.Id == digestId) {
									sendDigestMessage.Message.Id = digestId;
									sendDigestMessage.Message.Slices.FileId = requestDigest.SlicesInfo.FileId;
									sendDigestMessage.Message.Slices.FileInfo.Data.Entry = this.FetchDigest(requestDigest.Id, (int) requestDigest.SlicesInfo.FileInfo.Offset, (int) requestDigest.SlicesInfo.FileInfo.Length).Entry;

									sendDigestMessage.Message.Slices.FileInfo.Offset = requestDigest.SlicesInfo.FileInfo.Offset;
									sendDigestMessage.Message.Slices.FileInfo.Length = requestDigest.SlicesInfo.FileInfo.Length;
								}

								if(!this.Send(sendDigestMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

									return;
								}

								//sendDigestMessage.BaseMessage.Dispose();
							}

							if(requestSet?.BaseMessage is REQUEST_DIGEST_FILE requestDigestFile) {

								Log.Information($"Sending digest id {requestDigestFile.Id} to peer {this.PeerConnection.ScoppedAdjustedIp}.");

								// ok, now lets compare with ours, and find the ones that are different	

								var sendDigestFileMessage = (BlockchainTargettedMessageSet<SEND_DIGEST_FILE>) chainSyncMessageFactory.CreateServerSendDigestFile(this.triggerMessage.Header);

								sendDigestFileMessage.Message.Id = requestDigestFile.Id;

								sendDigestFileMessage.Message.Slices.FileId = requestDigestFile.SlicesInfo.FileId;

								foreach(var slice in requestDigestFile.SlicesInfo.SlicesInfo) {

									SafeArrayHandle data = this.FetchDigestFile(slice.Key.ChannelId, slice.Key.IndexId, slice.Key.FileId, slice.Key.FilePart, (int) slice.Value.Offset, (int) slice.Value.Length);

									sendDigestFileMessage.Message.Slices.SlicesInfo.Add(slice.Key, new DataSlice(slice.Value.Length, slice.Value.Offset, data));
								}

								if(!this.Send(sendDigestFileMessage)) {
									Log.Verbose($"Connection with peer  {this.PeerConnection.ScoppedAdjustedIp} was terminated");

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
				// thats it, we are done :)
				Log.Information($"Finished handling synchronization for peer {this.PeerConnection.ScoppedAdjustedIp}.");

			}
		}

		/// <summary>
		///     Get both a slice of block and the size of the next one
		/// </summary>
		/// <param name="Id"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		protected virtual (ChannelsEntries<SafeArrayHandle> blockSlice, ChannelsEntries<int> nextBlockSize, SafeArrayHandle nextBlockHash) FetchBlockSlice(long Id, ChannelsEntries<(int offset, int length)> offsets, long nextBlockId) {

			var slices = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockSlice(Id, offsets);

			(ChannelsEntries<int> sizes, SafeArrayHandle hash)? nextBlock = null;

			if(nextBlockId != 0) {
				nextBlock = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSizeAndHash(nextBlockId);

				if(!nextBlock.HasValue) {
					throw new WorkflowException("Next block was not found");
				}
			}

			return (slices, nextBlock?.sizes, nextBlock?.hash);

		}

		/// <summary>
		///     Get the binary size of a block on disk
		/// </summary>
		/// <param name="Id"></param>
		/// <returns></returns>
		protected virtual (ChannelsEntries<int> sizes, SafeArrayHandle hash) FetchBlockSize(long Id) {

			// lets make sure hour hashes are properly computed
			var result = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockSizeAndHash(Id);

			if(result == null) {
				throw new WorkflowException("Block was not found");
			}

			return (result.Value.sizes, result.Value.hash);
		}

		protected virtual (List<int> sliceHashes, int hash)? FetchBlockSlicesHashes(long Id, List<ChannelsEntries<(int offset, int length)>> slices) {

			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.BuildBlockSliceHashes(Id, slices);
				
		}

		protected virtual int FetchDigestSize(int Id) {
			// lets make sure hour hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestHeaderSize(Id);
		}

		protected virtual SafeArrayHandle FetchDigest(int Id, int offset, int length) {

			// lets make sure hour hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestHeaderArchiveData(Id, offset, length);
		}

		protected virtual SafeArrayHandle FetchDigestFile(DigestChannelType channelId, int indexId, int fileId, uint filePart, long offset, int length) {

			// lets make sure hour hashes are properly computed
			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestFile(channelId, indexId, fileId, filePart, offset, length);
		}

		protected virtual void RemoveClientIdWorkflow() {

			activeClientIds.RemoveSafe(this.PeerConnection.ClientUuid);
		}

		/// <summary>
		///     Check if the client Scope is already using a sync workflow. If now, we register it as such
		/// </summary>
		/// <returns></returns>
		protected virtual bool ClientIdWorkflowExistsAdd() {

			if(activeClientIds.ContainsKey(this.PeerConnection.ClientUuid) && !this.TestingMode) {

				var workflow = activeClientIds[this.PeerConnection.ClientUuid];

				if(!workflow.IsCompleted) {
					return true; // its there, we can't continue
				}

				// its fine, it is done, we can remove it and act like it was never there
				activeClientIds.RemoveSafe(this.PeerConnection.ClientUuid);
			}

			// lets add it now
			activeClientIds.AddSafe(this.PeerConnection.ClientUuid, this);

			return false;

		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ServerChainSyncWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, CLOSE_CONNECTION, REQUEST_BLOCK, REQUEST_DIGEST, SEND_BLOCK, SEND_DIGEST, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES> otherWorkflow) {
				return this.triggerMessage.Header.OriginatorId == otherWorkflow.triggerMessage.Header.OriginatorId;

			}

			return base.CompareOtherPeerId(other);
		}
	}
}