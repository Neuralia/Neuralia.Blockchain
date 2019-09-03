using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Serialization;
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
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
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

		protected PeerDigestSpecs GetDigestInfoConsensus(Dictionary<Guid, PeerDigestSpecs> peerBlockSpecs) {
			// ok, we have the previous block details provider, but if new peers were added since, we will take their trigger connection and add it here
			var existingIds = peerBlockSpecs.Keys.ToList();

			PeerDigestSpecs consensusSpecs = new PeerDigestSpecs();

			// make a consensus of what we already have

			ConsensusUtilities.ConsensusType nextBlockHeightConsensusType = ConsensusUtilities.ConsensusType.Undefined;

			if(peerBlockSpecs.Any()) {
				try {
					(consensusSpecs.digestId, nextBlockHeightConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values, a => a.digestId);
				} catch(SplitDecisionException e) {

					// ok, we have a tie. lets try without the 0 blockheights. if it fires an exception again
					try {
						(consensusSpecs.digestId, nextBlockHeightConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values.Where(v => v.digestId > 0), a => a.digestId);
					} catch(SplitDecisionException e2) {
						// lets do nothing, since we will try again concensus below with more data
						consensusSpecs.digestId = int.MaxValue;
					}
				}

				// here we dont need to test the consensus. its its the end, we finish, otherwise we will test again below
			}

			if(consensusSpecs.digestId <= 0) {
				// well, the consensus tells us that we have reached the end of the chain. we are fully synched and we can now stop
				return consensusSpecs;
			}

			// ok, get the ultimate consensus on the next block from everyone that matters right now

			(consensusSpecs.digestId, nextBlockHeightConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values.Where(v => v.digestId > 0), a => a.digestId);

			//(IByteArray nextBlockHash, ConsensusUtilities.ConsensusType nextBlockHashConsensusType) = ConsensusUtilities.GetConsensus(peerBlockSpecs.Values.Where(v => (v.nextBlockHash != null) && v.nextBlockHash.HasData), a => (a.nextBlockHash.GetArrayHash(), a.nextBlockHash));

			this.TestConsensus(nextBlockHeightConsensusType, nameof(consensusSpecs.digestId));

			//this.TestConsensus(nextBlockHeightConsensusType, nameof(nextBlockHash));

			var consensusSet = ChannelsInfoSet.RestructureConsensusBands<int, DigestChannelsInfoSet<DataSliceSize>, DataSliceSize>(peerBlockSpecs.ToDictionary(c => c.Key, c => c.Value.DigestSize));
			ConsensusUtilities.ConsensusType consensusType;

			// now the various channel sizes
			foreach(int channel in consensusSet.Keys) {

				if(!consensusSpecs.DigestSize.SlicesInfo.ContainsKey(channel)) {
					consensusSpecs.DigestSize.SlicesInfo.Add(channel, new DataSliceSize());
				}

				(consensusSpecs.DigestSize.SlicesInfo[channel].Length, consensusType) = ConsensusUtilities.GetConsensus(consensusSet[channel].Where(v => v.entry.Length > 0), a => a.entry.Length);

				this.TestConsensus(consensusType, channel + "-connection.Length");
			}

			return consensusSpecs;
		}

		/// <summary>
		///     Now we perform the synchronization for the next block
		/// </summary>
		protected virtual ResultsState SynchronizeDigest(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connections) {

			this.CheckShouldCancel();

			DigestSingleEntryContext singleEntryContext = new DigestSingleEntryContext();
			singleEntryContext.details = new PeerDigestSpecs();

			singleEntryContext.Connections = connections;

			// first thing, check if we have a digest sync manifest

			singleEntryContext.syncManifest = this.LoadDigestSyncManifest();
			singleEntryContext.syncManifestFiles = this.LoadDigestFileSyncManifest();

			if((singleEntryContext.syncManifest != null) && singleEntryContext.syncManifest.IsComplete && (singleEntryContext.syncManifestFiles != null) && singleEntryContext.syncManifestFiles.IsComplete) {

				// we are done it seems. move on to the next
				this.ClearDigestSyncManifest();
				this.ClearDigestFileSyncManifest();

				//TODO: check this
				return ResultsState.Error;
			}

			if(singleEntryContext.syncManifest == null) {
				// ok, determine if there is a digest to get

				ConsensusUtilities.ConsensusType nextDigestIdConsensusType;
				(singleEntryContext.details.digestId, nextDigestIdConsensusType) = ConsensusUtilities.GetConsensus(singleEntryContext.Connections.GetSyncingConnections().Where(v => BlockchainUtilities.UsesDigests(v.TriggerResponse.Message.BlockSavingMode)), a => a.ReportedDigestHeight);
				this.TestConsensus(nextDigestIdConsensusType, nameof(singleEntryContext.details.digestId));

				ResultsState resultState = ResultsState.None;

				try {
					Repeater.Repeat(() => {

						// no choice, we must fetch the connection
						var peerBlockSpecs = this.FetchPeerDigestInfo(singleEntryContext);

						if(peerBlockSpecs.state != ResultsState.OK) {
							resultState = peerBlockSpecs.state;

							throw new NoDigestInfoException();
						}

						singleEntryContext.details = this.GetDigestInfoConsensus(peerBlockSpecs.results);
					});
				} catch(WorkflowException) when(resultState != ResultsState.OK) {
					return resultState;
				}

				// ok, lets start the sync process
				singleEntryContext.syncManifest = new DigestFilesetSyncManifest();

				singleEntryContext.syncManifest.Id = singleEntryContext.details.digestId.ToString();

				singleEntryContext.syncManifest.Files.Add(1, new DataSlice {Length = singleEntryContext.details.DigestSize.FileInfo.Length});

				this.GenerateSyncManifestStructure<DigestFilesetSyncManifest, int, DigestFilesetSyncManifest.DigestSyncingDataSlice>(singleEntryContext.syncManifest);

				// save it to keep our state
				this.CreateDigestSyncManifest(singleEntryContext.syncManifest);
			} else {
				singleEntryContext.details.digestId = int.Parse(singleEntryContext.syncManifest.Id);
			}

			// now we will build the list of connections that will be used during this turn

			if(singleEntryContext.syncManifest.IsDigestFileCompleted() == false) {
				while(true) {
					this.CheckShouldCancel();

					bool success = false;

					try {

						if(singleEntryContext.syncManifest.IsComplete) {
							this.ResetDigestSyncManifest(singleEntryContext.syncManifest);
						}

						Repeater.Repeat(() => {

							// we its the first thing to do, lets get the digest core
							this.FetchPeerDigestData(singleEntryContext);

							if(singleEntryContext.syncManifest.IsComplete) {
								success = true;
							}
						});

					} catch(NoSyncingConnectionsException e) {
						throw;
					} catch(Exception e) {
						Log.Error(e, "Failed to fetch digest files data. might try again...");
					}

					if(!success) {
						Log.Fatal("Failed to fetch block digest files. we tried all the attempts we could and it still failed. this is critical. we may try again.");

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
			}

			// now build the files structure
			if(singleEntryContext.syncManifest.IsDigestFileCompleted() && ((singleEntryContext.syncManifestFiles == null) || (singleEntryContext.syncManifestFiles.Files.Count == 0))) {

				// ok, we are ready to move on
				singleEntryContext.syncManifestFiles = new ChannelsFilesetSyncManifest();

				// generate the file map
				IBlockchainDigest digest = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestHeader(int.Parse(singleEntryContext.syncManifest.Id));

				foreach(var channel in digest.DigestDescriptor.Channels) {
					foreach(var index in channel.Value.DigestChannelIndexDescriptors) {
						foreach(var file in index.Value.Files) {
							foreach(var part in file.Value.DigestChannelIndexFilePartDescriptors) {
								singleEntryContext.syncManifestFiles.Files.Add((channel.Key, index.Key, file.Key, part.Key), new DataSlice {Length = part.Value.FileSize});
							}
						}
					}
				}

				this.GenerateSyncManifestStructure<ChannelsFilesetSyncManifest, ChannelFileSetKey, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(singleEntryContext.syncManifestFiles);

				this.CreateDigestFileSyncManifest(singleEntryContext.syncManifestFiles);
			}

			DigestFilesSingleEntryContext singleFileEntryContext = new DigestFilesSingleEntryContext();
			singleFileEntryContext.syncManifest = singleEntryContext.syncManifestFiles;
			singleFileEntryContext.Connections = connections;
			singleFileEntryContext.details = new PeerDigestFileSpecs();
			singleFileEntryContext.details.digestId = singleEntryContext.details.digestId;

			// and then the files
			while(true) {
				this.CheckShouldCancel();

				bool success = false;

				try {

					if(singleFileEntryContext.syncManifest.IsComplete) {
						this.ResetDigestFileSyncManifest(singleFileEntryContext.syncManifest);
					}

					Log.Verbose($"Fetching digest files data, attempt {singleEntryContext.blockFetchAttemptCounter}");

					this.FetchPeerDigestFileData(singleFileEntryContext);

					if(singleFileEntryContext.syncManifest.IsComplete) {
						success = true;
						this.ClearDigestSyncManifest();
						this.ClearDigestFileSyncManifest();
					}

				} catch(NoSyncingConnectionsException e) {
					throw;
				} catch(Exception e) {
					Log.Error(e, "Failed to fetch digest files data. might try again...");
				}

				if(!success) {
					Log.Fatal("Failed to fetch block digest files. we tried all the attempts we could and it still failed. this is critical. we may try again.");

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

			//			Log.Information($"Block {digestId} has been synced successfully");
			return ResultsState.OK;
		}

		protected (Dictionary<Guid, PeerDigestSpecs> results, ResultsState state) FetchPeerDigestInfo(DigestSingleEntryContext singleEntryContext) {
			var infoParameters = new FetchInfoParameter<DigestChannelsInfoSet<DataSliceSize>, DataSliceSize, int, int, PeerDigestSpecs, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, DigestFilesetSyncManifest, DigestSingleEntryContext, DigestFilesetSyncManifest.DigestSyncingDataSlice>();

			infoParameters.id = singleEntryContext.details.digestId;
			infoParameters.singleEntryContext = singleEntryContext;

			infoParameters.generateInfoRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_DIGEST_INFO>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestDigestInfo(this.trigger.BaseHeader);

				return requestMessage;
			};

			infoParameters.validNextInfoFunc = (peerReply, missingRequestInfos, nextPeerDetails, peersWithNoNextEntry, peerConnection) => {

				if(peerReply.Message.Id <= 0) {

					if(peerReply.Message.SlicesSize.FileInfo.Length > 0) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					// this guy says there is no digest... we will consider it in our consensus
					PeerDigestSpecs nextDigestSpecs = new PeerDigestSpecs();

					nextDigestSpecs.digestId = 0;

					// update the value with what they reported
					peerConnection.ReportedDigestHeight = peerReply.Message.Id;

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextDigestSpecs);

					// there will be no next block with this guy. we will remove him for now, we can try him again later
					peersWithNoNextEntry.Add(peerReply.Header.ClientId);
				} else if(peerReply.Message.Id != singleEntryContext.details.digestId) {
					return ResponseValidationResults.Invalid; // that's an illegal value
				} else if(!nextPeerDetails.ContainsKey(peerConnection.PeerConnection.ClientUuid)) {

					if(peerReply.Message.SlicesSize.FileInfo.Length <= 0) {
						return ResponseValidationResults.Invalid; // bad block data is a major issue, they lie to us
					}

					// now we record what the peer says the next block will be like for consensus establishment
					PeerDigestSpecs nextBlockSpecs = new PeerDigestSpecs();

					nextBlockSpecs.digestId = peerReply.Message.Id;

					// update the value with what they reported
					peerConnection.ReportedDigestHeight = peerReply.Message.Id;

					nextBlockSpecs.DigestSize = peerReply.Message.SlicesSize;

					nextPeerDetails.Add(peerConnection.PeerConnection.ClientUuid, nextBlockSpecs);
				}

				var blockInfo = missingRequestInfos.Single(s => s.connection.PeerConnection.ClientUuid == peerConnection.PeerConnection.ClientUuid);

				// all good, keep the reply for later
				blockInfo.responseMessage = peerReply.Message;

				return ResponseValidationResults.Valid;
			};

			infoParameters.selectUsefulConnections = connections => {

				return connections.GetSyncingConnections().Where(c => BlockchainUtilities.UsesDigests(c.TriggerResponse.Message.BlockSavingMode) && (c.ReportedDigestHeight != 0)).ToList();

			};

			return this.FetchPeerInfo(infoParameters);
		}

		protected void FetchPeerDigestData(DigestSingleEntryContext singleBlockContext) {

			var parameters = new FetchDataParameter<DigestChannelsInfoSet<DataSliceInfo>, DataSliceInfo, DigestChannelsInfoSet<DataSlice>, DataSlice, int, int, PeerDigestSpecs, REQUEST_DIGEST, SEND_DIGEST, DigestFilesetSyncManifest, DigestSingleEntryContext, object, DigestFilesetSyncManifest.DigestSyncingDataSlice>();

			parameters.generateMultiSliceDataRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_DIGEST>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestDigest(this.trigger.BaseHeader);

				requestMessage.Message.Id = parameters.singleEntryContext.details.digestId;

				return requestMessage;
			};

			parameters.selectUsefulConnections = connections => {
				return connections.GetSyncingConnections().Where(c => c.ReportedDigestHeight >= parameters.singleEntryContext.details.digestId).ToList();
			};

			parameters.validSlicesFunc = (peerReply, nextPeerDetails, dispatchingSlices, peersWithNoNextEntry, peerConnection) => {

				// in this case, peer is valid if it has a more advanced blockchain than us and can share it back.
				if((peerReply.Message.Slices.FileInfo.Data == null) || peerReply.Message.Slices.FileInfo.Data.IsEmpty) {
					return ResponseValidationResults.Invalid; // no block data is a major issue
				}

				// ReSharper disable once AccessToModifiedClosure
				var slice = dispatchingSlices.Single(s => s.connection.PeerConnection.ClientUuid == peerConnection.PeerConnection.ClientUuid);

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

				// all good, keep the reply for later
				slice.responseMessage = peerReply.Message;

				return ResponseValidationResults.Valid;
			};

			parameters.writeDataSlice = (slice, response) => {

				this.WriteDigestSyncSlice(parameters.singleEntryContext.syncManifest, slice);
			};

			parameters.updateSyncManifest = () => {
				this.UpdateDigestSyncManifest(parameters.singleEntryContext.syncManifest);
			};

			parameters.clearManifest = () => {

				if(parameters.singleEntryContext.syncManifest.IsComplete && (parameters.singleEntryContext.syncManifestFiles != null) && parameters.singleEntryContext.syncManifestFiles.IsComplete) {
					this.ClearDigestSyncManifest();
					this.ClearDigestFileSyncManifest();
				}
			};

			parameters.downloadCompleted = data => {

				DigestFilesetSyncManifest syncManifest = parameters.singleEntryContext.syncManifest;

				IByteArray digestHeaderFile = this.LoadDigestSyncManifestFile(syncManifest, syncManifest.Key);

				IDehydratedBlockchainDigest dehydratedDigest = new DehydratedBlockchainDigest();
				dehydratedDigest.Rehydrate(Compressors.DigestCompressor.Decompress(digestHeaderFile));

				bool valid = true;

				if(valid) {
					try {
						dehydratedDigest.RehydrateDigest(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
					} catch(Exception ex) {
						Log.Error(ex, $"Failed to rehydrate digest {parameters.singleEntryContext.details.digestId} while syncing.");

						valid = false;
					}
				}

				this.CheckShouldCancel();

				if(valid) {

					var validationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateValidationTask<ValidationResult>();

					validationTask.SetAction((validationService, taskRoutingContext) => {
						valid = validationService.ValidateDigest(dehydratedDigest.RehydratedDigest, false).Valid;
					});

					this.DispatchTaskSync(validationTask);

					this.CheckShouldCancel();
				}

				if(valid) {
					// ok, we have our digest header! :D
					var serializationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

					serializationTask.SetAction((serializationService, taskRoutingContext) => {

						serializationService.SaveDigestHeader(parameters.singleEntryContext.details.digestId, digestHeaderFile);
					}, (results, taskRoutingContext) => {
						if(results.Error) {
							Log.Error(results.Exception, "Failed to insert digest into the local blockchain. we may try again...");
							valid = false;

							results.Handled();

							// thats bad, we failed to add our transaction
							if(parameters.singleEntryContext.blockFetchAttemptCounter == 3) {
								results.Wrap<WorkflowException>("Failed to insert block into the local blockchain.");

								// thats it, we tried enough. we  have to break
								results.Rethrow();
							}
						}

					});

					this.DispatchTaskSync(serializationTask);

				}

				return valid;
			};

			parameters.prepareFirstRunRequestMessage = null;
			parameters.processReturnMessage = null;

			parameters.singleEntryContext = singleBlockContext;

			this.FetchPeerData(parameters);
		}

		protected void FetchPeerDigestFileData(DigestFilesSingleEntryContext singleBlockContext) {

			var parameters = new FetchDataParameter<DigestFilesInfoSet<DataSliceInfo>, DataSliceInfo, DigestFilesInfoSet<DataSlice>, DataSlice, int, ChannelFileSetKey, PeerDigestFileSpecs, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, ChannelsFilesetSyncManifest, DigestFilesSingleEntryContext, object, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>();

			parameters.generateMultiSliceDataRequestMessage = () => {
				// its small enough, we will ask a single peer
				var requestMessage = (BlockchainTargettedMessageSet<REQUEST_DIGEST_FILE>) this.chainSyncMessageFactory.CreateSyncWorkflowRequestDigestFile(this.trigger.BaseHeader);

				requestMessage.Message.Id = 1;

				return requestMessage;
			};

			parameters.selectUsefulConnections = connections => {

				return connections.GetSyncingConnections().Where(c => c.ReportedDigestHeight >= parameters.singleEntryContext.details.digestId).ToList();

			};

			parameters.validSlicesFunc = (peerReply, nextPeerDetails, dispatchingSlices, peersWithNoNextEntry, peerConnection) => {

				// ReSharper disable once AccessToModifiedClosure
				var slice = dispatchingSlices.Single(s => s.connection.PeerConnection.ClientUuid == peerConnection.PeerConnection.ClientUuid);

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

				// all good, keep the reply for later
				slice.responseMessage = peerReply.Message;

				return ResponseValidationResults.Valid;
			};

			parameters.writeDataSlice = (slice, response) => {

				this.WriteDigestFileSyncSlice(parameters.singleEntryContext.syncManifest, slice);
			};

			parameters.updateSyncManifest = () => {
				this.UpdateDigestFileSyncManifest(parameters.singleEntryContext.syncManifest);
			};

			parameters.clearManifest = this.ClearDigestFileSyncManifest;

			parameters.downloadCompleted = data => {
				// thats it, we have it all, now we fully validate the digest and install it!

				var transactionchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				transactionchainTask.SetAction((blockchainService, taskRoutingContext) => {

					blockchainService.InstallDigest(parameters.singleEntryContext.details.digestId);
				}, (results, taskRoutingContext) => {
					if(results.Error) {
						Log.Error(results.Exception, "Failed to insert digest into the local blockchain. we may try again...");

						results.Handled();

						// thats bad, we failed to add our transaction
						if(parameters.singleEntryContext.blockFetchAttemptCounter == 3) {
							results.Wrap<WorkflowException>("Failed to insert block into the local blockchain.");

							// thats it, we tried enough. we  have to break
							results.Rethrow();
						}
					}

				});

				this.DispatchTaskSync(transactionchainTask);

				return true;
			};

			parameters.prepareFirstRunRequestMessage = null;
			parameters.processReturnMessage = null;

			parameters.singleEntryContext = singleBlockContext;

			this.FetchPeerData(parameters);
		}

		public DigestFilesetSyncManifest LoadDigestSyncManifest() {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			return this.LoadSyncManifest<int, DigestFilesetSyncManifest, DigestFilesetSyncManifest.DigestSyncingDataSlice>(path);
		}

		public IByteArray LoadDigestSyncManifestFile(DigestFilesetSyncManifest filesetSyncManifest, int key) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			return this.LoadSyncManifestFile<int, DigestFilesetSyncManifest, DigestFilesetSyncManifest.DigestSyncingDataSlice>(filesetSyncManifest, key, path);
		}

		public void CreateDigestSyncManifest(DigestFilesetSyncManifest filesetSyncManifest) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			this.CreateSyncManifest<int, DigestFilesetSyncManifest, DigestFilesetSyncManifest.DigestSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void UpdateDigestSyncManifest(DigestFilesetSyncManifest filesetSyncManifest) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			this.UpdateSyncManifest<int, DigestFilesetSyncManifest, DigestFilesetSyncManifest.DigestSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void WriteDigestSyncSlice(DigestFilesetSyncManifest filesetSyncManifest, DigestChannelsInfoSet<DataSlice> sliceData) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			this.WriteSyncSlice<DigestChannelsInfoSet<DataSlice>, int, DigestFilesetSyncManifest, DataSlice, DigestFilesetSyncManifest.DigestSyncingDataSlice>(filesetSyncManifest, sliceData, path);
		}

		public void ClearDigestSyncManifest() {
			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();
			this.ClearSyncManifest(path);
		}

		protected void ResetDigestSyncManifest(DigestFilesetSyncManifest syncManifest) {
			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestSyncManifestFileName();

			this.ResetSyncManifest<int, DigestFilesetSyncManifest, DigestFilesetSyncManifest.DigestSyncingDataSlice>(syncManifest, path);

		}

		//---------------------------------------

		public ChannelsFilesetSyncManifest LoadDigestFileSyncManifest() {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			return this.LoadSyncManifest<ChannelFileSetKey, ChannelsFilesetSyncManifest, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(path);
		}

		public IByteArray LoadDigestFileSyncManifestFile(ChannelsFilesetSyncManifest filesetSyncManifest, ChannelFileSetKey key) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			return this.LoadSyncManifestFile<ChannelFileSetKey, ChannelsFilesetSyncManifest, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(filesetSyncManifest, key, path);
		}

		public void CreateDigestFileSyncManifest(ChannelsFilesetSyncManifest filesetSyncManifest) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			this.CreateSyncManifest<ChannelFileSetKey, ChannelsFilesetSyncManifest, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void UpdateDigestFileSyncManifest(ChannelsFilesetSyncManifest filesetSyncManifest) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			this.UpdateSyncManifest<ChannelFileSetKey, ChannelsFilesetSyncManifest, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(filesetSyncManifest, path);
		}

		public void WriteDigestFileSyncSlice(ChannelsFilesetSyncManifest filesetSyncManifest, DigestFilesInfoSet<DataSlice> sliceData) {

			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			this.WriteSyncSlice<DigestFilesInfoSet<DataSlice>, ChannelFileSetKey, ChannelsFilesetSyncManifest, DataSlice, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(filesetSyncManifest, sliceData, path);
		}

		public void ClearDigestFileSyncManifest() {
			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();
			this.ClearSyncManifest(path);
		}

		protected void ResetDigestFileSyncManifest(ChannelsFilesetSyncManifest syncManifest) {
			string path = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetDigestFileSyncManifestFileName();

			this.ResetSyncManifest<ChannelFileSetKey, ChannelsFilesetSyncManifest, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice>(syncManifest, path);

		}

		protected class DigestSingleEntryContext : SingleEntryContext<int, DigestFilesetSyncManifest, PeerDigestSpecs, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, DigestFilesetSyncManifest.DigestSyncingDataSlice> {
			public ChannelsFilesetSyncManifest syncManifestFiles;
		}

		protected class DigestFilesSingleEntryContext : SingleEntryContext<ChannelFileSetKey, ChannelsFilesetSyncManifest, PeerDigestFileSpecs, CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice> {
		}

		public class PeerDigestSpecs {

			public int digestId;
			public DigestChannelsInfoSet<DataSliceSize> DigestSize = new DigestChannelsInfoSet<DataSliceSize>();
		}

		public class PeerDigestFileSpecs {
			public DigestFilesInfoSet<DataSliceSize> DigestFileSize = new DigestFilesInfoSet<DataSliceSize>();

			public int digestId;
			public ChannelFileSetKey fileId;
		}
	}
}