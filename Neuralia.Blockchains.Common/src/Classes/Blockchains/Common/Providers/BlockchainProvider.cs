using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	
	
	public interface IBlockchainProvider: IChainProvider{
		
		IEventPoolProvider ChainEventPoolProvider { get; }

		void InsertLocalTransaction(ITransactionEnvelope transactionEnvelope, string note, CorrelationContext correlationContext);
		void InsertGossipTransaction(ITransactionEnvelope transactionEnvelope);

		void InstallGenesisBlock(IGenesisBlock genesisBlock, IDehydratedBlock dehydratedBlock);

		bool InsertInterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool allowWalletSyncGrowth = true);
		bool InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, bool allowWalletSyncGrowth = true);
		bool InsertBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool useTransaction = true);

		void InstallDigest(int digestId);

		void HandleBlockchainMessage(IBlockchainMessage message, IDehydratedBlockchainMessage dehydratedMessage);

		long GetBlockHeight();
		BlockchainInfo GetBlockchainInfo();

		

		void PerformElection(IBlock block);
		List<ElectedCandidateResultDistillate> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate);
		bool PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults);
	}

	public interface IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockchainProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class BlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly IBlockchainGuidService guidService;
		protected readonly IBlockchainTimeService timeService;

		protected CENTRAL_COORDINATOR CentralCoordinator { get; }
		
		public BlockchainProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;
			
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;
		}

		private IEventPoolProvider chainEventPoolProvider;

		public IEventPoolProvider ChainEventPoolProvider {
			get {
				// here we wanr to be the only one holding an insteand due to thread safety. thats why we dont hold it in the general component provider.
				if(this.chainEventPoolProvider == null) {
					this.chainEventPoolProvider = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateBlockchainEventPoolProvider(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase);
				}

				return this.chainEventPoolProvider;
			}
		}
		
		
		/// <summary>
		///     Here is where we insert our own transactions into the cache, and the network
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual void InsertLocalTransaction(ITransactionEnvelope transactionEnvelope, string note, CorrelationContext correlationContext) {
			//TODO: getting here would be a hack by an ill intended peer, should we log the peer's bad behavior?
			if(transactionEnvelope.Contents.RehydratedTransaction is IGenesisAccountPresentationTransaction) {
				throw new ApplicationException("Genesis transactions can not be added this way");
			}

			Log.Verbose($"Inserting new local transaction with Id {transactionEnvelope.Contents.Uuid}");

			// ok, now we will want to send out a gossip message to inform others that we have a new transaction.

			// first step, lets add this new transaction to our own wallet pool
			IndependentActionRunner.Run(() => {
				Repeater.Repeat(() => {
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertLocalTransactionCacheEntry(transactionEnvelope);
				});
			}, () => {
				ITransaction transaction = transactionEnvelope.Contents.RehydratedTransaction;

				Repeater.Repeat(() => {

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertTransactionHistoryEntry(transaction, note);
				});
			});

			try {
				this.AddTransactionToEventPool(transactionEnvelope);
			} catch(Exception ex) {
				Log.Error(ex, "failed to add transaction to local event pool");
			}

			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionCreated(transactionEnvelope.Contents.Uuid));

			// ok, we are ready. lets send it out to the world!!  :)
			this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchLocalTransactionAsync(transactionEnvelope, correlationContext);
		}

		

		/// <summary>
		///     We received a transaction as a gossip message. lets add it to our
		///     transaction cache if required
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		public virtual void InsertGossipTransaction(ITransactionEnvelope transactionEnvelope) {

			Log.Verbose($"Received new gossip transaction with Id {transactionEnvelope.Contents.Uuid} from peers.");

			this.AddTransactionToEventPool(transactionEnvelope);
		}

		public virtual void InstallGenesisBlock(IGenesisBlock genesisBlock, IDehydratedBlock dehydratedBlock) {

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight != 0) {
				throw new ApplicationException("the genesis block must absolutely be the first block in the chain");
			}

			Log.Information($"Installing genesis block with Id {genesisBlock.BlockId} and Timestamp {genesisBlock.FullTimestamp}.");

			try {
				// first and most important, add it to our archive
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlock(dehydratedBlock);

				// if fast keys are enabled, then we create the base directory and first file
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.EnsureFastKeysIndex();

				// thats it really. now we have our block, lets update our chain stats.

				// ready to move to the next step
				var chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;
				
				var actions = new List<Func<IChainStateProvider, string>>();
						
				actions.Add(prov => prov.SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted));
				// we got our first block!
				actions.Add(prov => prov.SetDiskBlockHeightField(1));
				
				actions.Add(prov => prov.SetLastBlockTimestampField(genesisBlock.FullTimestamp));
				// infinite
				actions.Add(prov => prov.SetLastBlockLifespanField(0));


				// lets set the timestamp, thats our inception. its very important to remove milliseconds, keep it very simple.
				actions.Add(prov => prov.SetChainInceptionField(genesisBlock.Inception.TrimMilliseconds()));
				
				// lets store the block hash
				actions.Add(prov => prov.SetLastBlockHashField(genesisBlock.Hash.ToExactByteArrayCopy()));
				
				// keep it too, its too nice :)
				actions.Add(prov => prov.SetGenesisBlockHashField(genesisBlock.Hash.ToExactByteArrayCopy()));
				
				chainStateProvider.UpdateFields(actions);

				// store the promised next signature if it is secret
				if(genesisBlock.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
					chainStateProvider.InsertModeratorKey(new TransactionId(), genesisBlock.SignatureSet.NextModeratorKey, genesisBlock.SignatureSet.ConvertToDehydratedKey());
				}

			} catch(Exception ex) {
				Log.Fatal(ex, "Failed to insert genesis blocks into our model.");

				// this is very critical
				throw;
			}

			// now inform the wallet manager that a new block has been received
			this.CentralCoordinator.RequestWalletSync(genesisBlock, true, false);
		}

		public virtual bool InsertInterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool allowWalletSyncGrowth = true) {
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			if(chainStateProvider.DiskBlockHeight == (block.BlockId.Value - 1)) {
				// good to go!
				if(!this.InsertBlock(block, dehydratedBlock, syncWallet)) {
					return false;
				}
			}

			bool currentincomplete = (chainStateProvider.BlockHeight == block.BlockId.Value) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted);
			bool previousCompleted = (chainStateProvider.BlockHeight == (block.BlockId.Value - 1)) && (chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted);

			if(currentincomplete || previousCompleted) {
				// good to go
				return this.InterpretBlock(block, dehydratedBlock, syncWallet, null, allowWalletSyncGrowth);
			}

			return false;
		}

		public virtual bool InsertBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool useTransaction = true) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			if((chainStateProvider.DiskBlockHeight == 0) || block is IGenesisBlock || (block.BlockId.Value == 1)) {
				if((block.BlockId.Value == 1) && (chainStateProvider.DiskBlockHeight == 0) && block is IGenesisBlock genesisBlock) {

					// ok, this is a genesisModeratorAccountPresentation block, we install its
					this.InstallGenesisBlock(genesisBlock, dehydratedBlock);

					return true;
				}

				throw new ApplicationException("A genesis block must first exist before a standard block can be added");
			}

			if(chainStateProvider.DiskBlockHeight < (block.BlockId.Value - 1)) {
				Log.Warning($"The block '{block.BlockId.Value}' received is further ahead than where we are. The chain must be synced");

				return false;
			}

			if(chainStateProvider.DiskBlockHeight >= block.BlockId.Value) {
				Log.Warning("We are attempting to install a block that we already have");

				return false;
			}

			Log.Information($"Installing block Id {block.BlockId} and Timestamp {block.FullTimestamp}.");

			// if we are already at the height intended, then the block is inserted
			bool isBlockAlreadyInserted = chainStateProvider.DiskBlockHeight >= block.BlockId.Value;

			// first and most important, add it to our archive
			if(!isBlockAlreadyInserted) {
				// lets transaction this operatoin

				void InsertBlockData() {
					this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlock(dehydratedBlock);

					// lets update our chain
					Repeater.Repeat(() => {

						var actions = new List<Func<IChainStateProvider, string>>();
						
						actions.Add(prov => prov.SetDiskBlockHeightField(block.BlockId.Value));
						actions.Add(prov => prov.SetLastBlockTimestampField(block.FullTimestamp));
						
						// a hint as to when we should expect the next one
						actions.Add(prov => prov.SetLastBlockLifespanField(block.Lifespan));
						// lets store the block hash
						actions.Add(prov => prov.SetLastBlockHashField(block.Hash.ToExactByteArrayCopy()));
						
						chainStateProvider.UpdateFields(actions);
						
						// store the promised next signature if it is secret
						if(block.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
							chainStateProvider.UpdateModeratorKey(new TransactionId(), block.SignatureSet.NextModeratorKey, block.SignatureSet.ConvertToDehydratedKey());
						}
						else if(block.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID && ((XmssBlockNextAccountSignature)block.SignatureSet.NextBlockAccountSignature).KeyChange) {
							chainStateProvider.UpdateModeratorKey(new TransactionId(), block.SignatureSet.NextModeratorKey, block.SignatureSet.ConvertToDehydratedKey());
						}
					});
				}

				if(useTransaction) {
					using(IBlockInsertionTransactionProcessor blockStateSnapshotProcessor = this.CreateBlockInsertionTransactionProcessor(block.SignatureSet.NextModeratorKey)) {
						InsertBlockData();

						blockStateSnapshotProcessor.Commit();
					}
				} else {
					InsertBlockData();
				}
			}

			// thats it really. now we have our block, lets update our chain stats.
			if(!isBlockAlreadyInserted) {

				// now, alert the world of this new block!
				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInserted(block.BlockId.Value, block.FullTimestamp, block.Hash.Entry.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

				try {
					this.BlockInstalled(block, dehydratedBlock);
				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to invoke block installed callback.");
				}

				try {
					Repeater.Repeat(() => {
						// Now we clear the transaction pool of any transactions contained in this block
						this.ClearTransactionPoolBlockTransactions(block);
					});
				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to clear the transaction pool of block transactions.");
				}
			}

			if(syncWallet) {
				try {
					this.CentralCoordinator.RequestWalletSync(block, true, false);

				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
				}
			}

			return true;
		}

		public virtual bool InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, bool allowWalletSyncGrowth = true) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			//lets see if we are ready. a genesis block is alwasy ready
			if(block.BlockId.Value > 1) {

				long previousBlockId = block.BlockId.Value - 1;

				if((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
					Log.Warning($"We are attempting to interpret block {block.BlockId.Value} but the previous installations are not complete. Current chain height is at {chainStateProvider.BlockHeight}. Syncing...");

					while((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {

						long originalBlockId = chainStateProvider.BlockHeight;
						(IBlock block1, IDehydratedBlock dehydratedBlock1) = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(originalBlockId);

						if(!this.InterpretBlock(block1, dehydratedBlock1, syncWallet, serializationTransactionProcessor, allowWalletSyncGrowth) || ((originalBlockId == chainStateProvider.BlockHeight) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
							Log.Warning("We failed to catch up the missing blocks in interpretation.");
						}
					}
				}

				if((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {
					Log.Warning("We are attempting to interpret a block but the previous installation is not complete");

					this.CentralCoordinator.RequestBlockchainSync(true);

					return false;
				}
			}

			// launch the interpretation of this block
			void InterpretBlock(SerializationTransactionProcessor transactionalProcessor) {

				// thats it, lets begin interpretation only if ti was not already performed or completed
				if(chainStateProvider.BlockHeight == block.BlockId.Value - 1) {
					
					var actions = new List<Func<IChainStateProvider, string>>();
						
					actions.Add(prov => prov.SetBlockHeightField(block.BlockId.Value));
					actions.Add(prov => prov.SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses.Blank));

					chainStateProvider.UpdateFields(actions);
					
				} else if(chainStateProvider.BlockHeight < block.BlockId.Value - 1) {
					throw new ArgumentException("Block Id is too low for interpretation");
				}

				if(chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted) {
					return;
				}

				this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(block, transactionalProcessor);

				this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.InterpretNewBlockSnapshots(block, transactionalProcessor);
			}

			// if we are not already in a transaction, we make one
			if(chainStateProvider.BlockHeight == block.BlockId.Value && chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted) {
				// block has already been interpreted. go no further
				return true;
			}

			if(serializationTransactionProcessor == null) {
				string cachePath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetGeneralCachePath();

				using(serializationTransactionProcessor = new SerializationTransactionProcessor(cachePath, this.CentralCoordinator.FileSystem)) {

					InterpretBlock(serializationTransactionProcessor);
					serializationTransactionProcessor.Commit();
				}
			} else {
				InterpretBlock(serializationTransactionProcessor);
			}

			if(chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted) {
				throw new ApplicationException($"Failed to interpret block id {block.BlockId}.");
			}

			// thats it, we are at this block level now
			chainStateProvider.BlockHeight = block.BlockId.Value;

			try {
				this.BlockInterpreted(block, dehydratedBlock);
			} catch(Exception ex) {
				Log.Fatal(ex, "Failed to invoke block installed callback.");
			}


			Log.Verbose($"block {block.BlockId.Value} has been successfully interpreted.");

			// now, alert the world of this new block newly interpreted!
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInterpreted(block.BlockId.Value, block.FullTimestamp, block.Hash.Entry.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

			if(syncWallet) {
				try {

					void Catcher() {

						// meanwhile, see if we need to mine
						try {
							this.PerformElection(block);
						} finally {
							this.CentralCoordinator.WalletSynced -= Catcher;
						}
					}

					this.CentralCoordinator.WalletSynced += Catcher;

					this.CentralCoordinator.RequestWalletSync(block, true, allowWalletSyncGrowth);

				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
				}
			} else {
				// we can give it a try
				this.PerformElection(block);
			}

			return true;
		}

		/// <summary>
		///     Here we perform th first part of an election and return the election results
		/// </summary>
		/// <param name="blockElectionDistillate"></param>
		/// <param name="chainEventPoolProvider"></param>
		/// <returns></returns>
		public virtual List<ElectedCandidateResultDistillate> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate) {

			return this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElectionComputations(blockElectionDistillate);
		}

		/// <summary>
		///     Here, we can generate the election messages for any election we are in
		/// </summary>
		/// <param name="blockElectionDistillate"></param>
		/// <param name="electionResults">
		///     The election results where we are elected. MAKE SURE THE TRANSACTION SELECTIONS HAVE BEEN
		///     SET!!
		/// </param>
		/// <param name="async"></param>
		/// <returns></returns>
		public virtual bool PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults) {

			var messages = this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults);

			return this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchElectionMessages(messages);
		}

		/// <summary>
		///     HEre we perform an entire election with the block we have locally
		/// </summary>
		/// <param name="block"></param>
		/// <param name="async"></param>
		/// <returns></returns>
		public virtual void PerformElection(IBlock block) {

			bool elected = false;

			try {
				if(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled) {
					// ok, we are mining, lets check this block

					if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount != 0) {
						this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElection(block, (messages) => {
							if((messages != null) && messages.Any()) {
								elected = true;
								this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchElectionMessages(messages);
							}
						});
					} else {
						Log.Error("Mining is enabled but we are not connected to any peers. Elections cancelled.");
					}
				}
			} catch(Exception ex) {
				Log.Fatal(ex, "Failed to perform mining election.");
			}
		}

		public virtual void InstallDigest(int digestId) {

			Log.Information($"Installing new digest Id {digestId}.");

			// first and most important, add it to our archive
			IBlockchainDigest digest = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestHeader(digestId);

			Log.Information($"Loaded digest Id {digest.DigestId} and Timestamp {digest.FullTimestamp}.");

			// now validate

			try {

				var result = this.CentralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateDigest(digest, true);
				
				if(result.Invalid) {
					// failed to validate digest
					//TODO: what to do??
					throw new ApplicationException("failed to validate digest");
				}

				// ok, its good, lets install the digest

				// ok, lets update the actual files and all

				// first, we generate the digest channel descriptor, for quick use
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SaveDigestChannelDescription(digestId, digest.DigestDescriptor);

				// now the big moment, we update the entire filesystem
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.UpdateCurrentDigest(digest);

				Repeater.Repeat(() => {

					var chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;
					// ok, now we update our states
					chainStateProvider.DigestHeight = digestId;
					chainStateProvider.DigestBlockHeight = digest.BlockId.Value;
					chainStateProvider.LastDigestHash = digest.Hash.ToExactByteArrayCopy();
					chainStateProvider.LastDigestTimestamp = this.timeService.GetTimestampDateTime(digest.Timestamp.Value, chainStateProvider.ChainInception);
					chainStateProvider.LastBlockHash = digest.BlockHash.Entry.ToExactByteArrayCopy();

					chainStateProvider.UpdateModeratorKey(new TransactionId(), digest.BlockSignatureSet.NextModeratorKey, digest.BlockSignatureSet.ConvertToDehydratedKey());

					if(chainStateProvider.DigestBlockHeight > chainStateProvider.BlockHeight) {
						// ok, this digest is ahead for us, we must now update the snapshot state
						this.UpdateAccountSnapshotFromDigest(digest);
					}

					if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.NodeShareType().PartialBlocks) {
						if(chainStateProvider.DownloadBlockHeight < digest.BlockId.Value) {
							chainStateProvider.DownloadBlockHeight = digest.BlockId.Value;
						}
						
						if(chainStateProvider.DiskBlockHeight < digest.BlockId.Value) {
							chainStateProvider.DiskBlockHeight = digest.BlockId.Value;
						}
						
						if(chainStateProvider.BlockHeight < digest.BlockId.Value) {
							chainStateProvider.BlockHeight = digest.BlockId.Value;
						}
					}
				});

				// now, alert the world of this new digest!
				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.DigestInserted(digestId, digest.FullTimestamp, digest.Hash.Entry.ToBase58()));

				// and thats it, the digest is fully installed :)

			}catch(Exception ex) {

				Log.Fatal(ex, $"Failed to validate digest id {digestId}.");

				// this is very critical
				throw new ApplicationException($"Failed to validate digest id {digestId}.");
					

			}

		}

		public virtual void HandleBlockchainMessage(IBlockchainMessage message, IDehydratedBlockchainMessage dehydratedMessage) {

			//save the message if we need so
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MessageSavingMode == AppSettingsBase.MessageSavingModes.Enabled) {

				try {
					// first and most important, add it to our archive
					this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlockchainMessage(dehydratedMessage);
				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to serialize message!.");

					// this is very critical
					throw;
				}
			}
		}

		

		public virtual bool EnsureBlockInstallInterpreted(IBlock block, bool syncWallet) {
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// ok, the block was inserted, but its not complete?
			if((chainStateProvider.BlockHeight == block.BlockId.Value) && !chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {

				// seems we need to interpret this block
				(IBlock block, IDehydratedBlock dehydratedBlock) blockData = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(chainStateProvider.BlockHeight);

				if(blockData == default) {
					throw new ApplicationException($"Failed to load block Id {chainStateProvider.BlockHeight}. could not interpet block, nor install block ID {block.BlockId.Value}");
				}

				this.InterpretBlock(blockData.block, blockData.dehydratedBlock, syncWallet, null);
			}

			return true;
		}

		protected abstract IBlockInsertionTransactionProcessor CreateBlockInsertionTransactionProcessor(byte moderatorKeyOrdinal);

		

		/// <summary>
		///     called when a new block has been successfully installed
		/// </summary>
		/// <param name="block"></param>
		protected virtual void BlockInstalled(IBlock block, IDehydratedBlock dehydratedBlock) {

		}

		/// <summary>
		///     called when a new block has been successfully interpreted
		/// </summary>
		/// <param name="block"></param>
		protected virtual void BlockInterpreted(IBlock block, IDehydratedBlock dehydratedBlock) {

		}
		
		/// <summary>
		///     Update the local snapshots with the connection provided by the digest
		/// </summary>
		/// <param name="digest"></param>
		protected virtual void UpdateAccountSnapshotFromDigest(IBlockchainDigest digest) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestBlockHeight > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
				// ok, this digest is ahead for us, we must now update the snapshot state

				// this is an important moment, and very rare. we will run it in the serialization thread

				try {
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {

						token.ThrowIfCancellationRequested();

						this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.ClearSnapshots();

						var walletAccounts = provider.GetAccounts();

						token.ThrowIfCancellationRequested();
						
						// loop all accounts :)
						for(long accountSequenceId = 1; accountSequenceId <= digest.LastStandardAccountId; accountSequenceId++) {

							token.ThrowIfCancellationRequested();

							AccountId accountId = new AccountId(accountSequenceId, Enums.AccountTypes.Standard);

							IAccountSnapshotDigestChannelCard accountCard = null;

							// determine if its one of ours
							IWalletAccount localAccount = walletAccounts.SingleOrDefault(a => a.PublicAccountId == accountId);

							if(localAccount != null) {

								accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccount(accountSequenceId);

								// ok, this is one of ours, we will need to update the wallet snapshot
								provider.UpdateWalletSnapshotFromDigest(accountCard);
							}

							if(this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccount(accountSequenceId);
								}

								this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard);

								var accountKeyCards = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccountKeyCards(accountSequenceId);

								foreach(IStandardAccountKeysDigestChannelCard digestKey in accountKeyCards) {
									this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateAccountKeysFromDigest(digestKey);
								}
							}
						}

						// loop all accounts :)
						for(long accountSequenceId = 1; accountSequenceId < digest.LastJointAccountId; accountSequenceId++) {

							token.ThrowIfCancellationRequested();

							AccountId accountId = new AccountId(accountSequenceId, Enums.AccountTypes.Joint);

							IAccountSnapshotDigestChannelCard accountCard = null;

							// determine if its one of ours
							IWalletAccount localAccount = walletAccounts.SingleOrDefault(a => a.PublicAccountId == accountId);

							if(localAccount != null) {

								accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestJointAccount(accountSequenceId);

								// ok, this is one of ours, we will need to update the wallet snapshot
								provider.UpdateWalletSnapshotFromDigest(accountCard);
							}

							if(this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestJointAccount(accountSequenceId);
								}

								this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard);
							}
						}

						// now the accreditation certificates
						var certificates = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestAccreditationCertificateCards();

						foreach(IAccreditationCertificateDigestChannelCard certificate in certificates) {

							token.ThrowIfCancellationRequested();

							this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateAccreditationCertificateFromDigest(certificate);
						}

					});

				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to update the blockchain snapshots from the digest.");

					throw;
				}

			}
		}

	

		/// <summary>
		///     Ensure a transaction is added to our chain event pool
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		protected void AddTransactionToEventPool(ITransactionEnvelope transactionEnvelope) {

			if(this.ChainEventPoolProvider.EventPoolEnabled) {

				Log.Verbose($"inserting transaction {transactionEnvelope.Contents.Uuid} into the chain pool. " + (this.ChainEventPoolProvider.SaveTransactionEnvelopes ? "The whole body will be saved" : "Only metadata will be saved"));

				// ok, we are saving the transactions to the transaction pool. first lets save the metadata to the pool
				this.ChainEventPoolProvider.InsertTransaction(transactionEnvelope);
			}
		}

		protected virtual void ClearTransactionPoolBlockTransactions(IBlock block) {
			// get all transactions
			var transactions = block.GetAllTransactions();

			this.ChainEventPoolProvider.DeleteTransactions(transactions);
		}


		



	#region rpc methods

		public long GetBlockHeight() {
			return this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
		}

		public BlockchainInfo GetBlockchainInfo() {

			IChainStateProvider chainState = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			return new BlockchainInfo {
				DownloadBlockId = chainState.DownloadBlockHeight, DiskBlockId = chainState.DiskBlockHeight, BlockId = chainState.BlockHeight, BlockHash = ByteArray.Wrap(chainState.LastBlockHash)?.ToBase58(),
				BlockTimestamp = TimeService.FormatDateTimeStandardUtc(chainState.LastBlockTimestamp), PublicBlockId = chainState.PublicBlockHeight, DigestId = chainState.DigestHeight, DigestHash = ByteArray.Wrap(chainState.LastDigestHash)?.ToBase58(),
				DigestBlockId = chainState.DigestBlockHeight, DigestTimestamp = TimeService.FormatDateTimeStandardUtc(chainState.LastDigestTimestamp), PublicDigestId = chainState.PublicDigestHeight
			};
		}
		
	#endregion
		
		public void PostChainEvent(SystemMessageTask messageTask, CorrelationContext correlationContext = default) {
        			this.CentralCoordinator.PostSystemEvent(messageTask, correlationContext);
        		}
        
        		public void PostChainEvent(BlockchainSystemEventType message, CorrelationContext correlationContext = default) {
        			this.CentralCoordinator.PostSystemEvent(message, correlationContext);
        		}
	}
}