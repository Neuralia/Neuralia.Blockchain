using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IGossipMessageDispatcher {
		void DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext);
	}

	public interface IBlockchainManager : IManagerBase, IGossipMessageDispatcher {

		IEventPoolProvider ChainEventPoolProvider { get; }

		bool BlockchainSyncing { get; }
		bool BlockchainSynced { get; }

		bool? WalletSyncedNoWait { get; }
		bool WalletSynced { get; }
		bool WalletSyncing { get; }

		void InsertLocalTransaction(ITransactionEnvelope transactionEnvelope, string note, CorrelationContext correlationContext);

		void InsertGossipTransaction(ITransactionEnvelope transactionEnvelope);

		void InstallGenesisBlock(IGenesisBlock genesisBlock, IDehydratedBlock dehydratedBlock);

		bool InsertInterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool allowWalletSyncGrowth = true);
		bool InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, bool allowWalletSyncGrowth = true);
		bool InsertBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, bool useTransaction = true);
		void SynchronizeBlockchain(bool force);

		void InstallDigest(int digestId);

		void HandleBlockchainMessage(IBlockchainMessage message, IDehydratedBlockchainMessage dehydratedMessage);

		long GetBlockHeight();
		BlockchainInfo GetBlockchainInfo();

		void DispatchLocalTransactionAsync(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext);
		void DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext);

		bool PerformElection(IBlock block);
		List<ElectedCandidateResultDistillate> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate);
		bool PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults);

		void SynchronizeWallet(bool force, bool? allowGrowth = null);
		void SynchronizeWallet(IBlock block, bool force, bool? allowGrowth = null);
		void SynchronizeWallet(List<IBlock> blocks, bool force, bool? allowGrowth = null);
		void SynchronizeWallet(List<IBlock> blocks, bool force, bool mobileForce, bool? allowGrowth = null);

		void SynchronizeBlockchainExternal(string synthesizedBlock);
	}

	public interface IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IManagerBase<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IBlockchainManager
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This is the blockchain maintenance thread. There to take care of our chain and handle it's state.
	/// </summary>
	public abstract class BlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ManagerBase<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly IBlockchainGuidService guidService;
		protected readonly IBlockchainTimeService timeService;

		private IEventPoolProvider chainEventPoolProvider;

		// the sync workflow we keep as a reference.
		private IClientChainSyncWorkflow chainSynchWorkflow;
		private DateTime? nextBlockchainSynchCheck;
		private DateTime? nextExpiredTransactionCheck;
		private DateTime? nextWalletSynchCheck;
		private ISyncWalletWorkflow synchWalletWorkflow;

		public BlockchainManager(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, 1) {
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;

		}

		protected new CENTRAL_COORDINATOR CentralCoordinator => base.CentralCoordinator;

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

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertTransactionHistoryEntry(transaction, transaction.TransactionId.Account, note);
				});
			});

			try {
				this.AddTransactionToEventPool(transactionEnvelope);
			} catch(Exception ex) {
				Log.Error(ex, "failed to add transaction to local event pool");
			}

			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionCreated(transactionEnvelope.Contents.Uuid.SimpleTransactionId));

			// ok, we are ready. lets send it out to the world!!  :)
			this.DispatchLocalTransactionAsync(transactionEnvelope, correlationContext);
		}

		/// <summary>
		///     Publish an unpublished transaction on the network
		/// </summary>
		public void DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext) {

			IWalletTransactionCache results = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetLocalTransactionCacheEntry(transactionId);

			if(results == null) {
				throw new ApplicationException("Impossible to dispatch a transaction, failed to find cached entry.");
			}

			if(results.Status != (byte) WalletTransactionCache.TransactionStatuses.New) {
				throw new ApplicationException("Impossible to dispatch a transaction that has already been sent");
			}

			ITransactionEnvelope transactionEnvelope = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ITransactionEnvelope>(results.Transaction);

			this.DispatchLocalTransactionAsync(transactionEnvelope, correlationContext);
		}

		/// <summary>
		///     Publish an unpublished transaction on the network
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		public void DispatchLocalTransactionAsync(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext) {

			IWalletTransactionCache entry = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetLocalTransactionCacheEntry(transactionEnvelope.Contents.Uuid.SimpleTransactionId);

			if(entry.Status != (byte) WalletTransactionCache.TransactionStatuses.New) {
				throw new ApplicationException("Impossible to dispatch a transaction that has already been sent");
			}

			BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool sent = false;

			if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections && (this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount >= chainConfiguration.MinimumDispatchPeerCount)) {

				IBlockchainGossipMessageSet gossipMessageSet = this.PrepareGossipTransactionMessageSet(transactionEnvelope);

				Repeater.Repeat(() => {
					this.SendGossipTransaction(gossipMessageSet);
					sent = true;
				});

				if(!sent) {
					throw new ApplicationException("Failed to send transaction");
				}

				this.ConfirmTransactionSent(transactionEnvelope, correlationContext, gossipMessageSet.BaseHeader.Hash);

			} else {
				throw new ApplicationException("Failed to send transaction. Not enough peers available to send a gossip transactions.");
			}

			if(!sent) {
				throw new ApplicationException("Failed to send transaction");
			}
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

			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {

				// first and most important, add it to our archive
				serializationService.SerializeBlock(dehydratedBlock);
			}, (results, taskRoutingContext) => {

				if(results.Success) {
					// thats it really. now we have our block, lets update our chain stats.

					// ready to move to the next step
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus = ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted;

					// we got our first block!
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight = 1;

					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockTimestamp = genesisBlock.FullTimestamp;

					// infinite
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockLifespan = 0;

					// lets set the timestamp, thats our inception. its very important to remove milliseconds, keep it very simple.
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception = genesisBlock.Inception.TrimMilliseconds();

					// lets store the block hash
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockHash = genesisBlock.Hash.ToExactByteArrayCopy();

					// keep it too, its too nice :)
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GenesisBlockHash = genesisBlock.Hash.ToExactByteArrayCopy();

					// store the promised next signature if it is secret
					if(genesisBlock.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
						this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.InsertModeratorKey(new TransactionId(), genesisBlock.SignatureSet.NextModeratorKey, genesisBlock.SignatureSet.ConvertToDehydratedKey());
					}

				} else {
					Log.Fatal(results.Exception, "Failed to insert genesis blocks into our model.");

					// this is very critical
					results.Rethrow();
				}

			});

			this.DispatchTaskSync(serializationTask);

			// now inform the wallet manager that a new block has been received
			this.SynchronizeWallet(genesisBlock, true, false);
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

			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {

				// first and most important, add it to our archive
				if(!isBlockAlreadyInserted) {
					// lets transaction this operatoin

					void InsertBlockData() {
						serializationService.SerializeBlock(dehydratedBlock);

						// lets update our chain
						Repeater.Repeat(() => {
							//TODO: group all this inside a single database call for efficiency
							chainStateProvider.DiskBlockHeight = block.BlockId.Value;

							chainStateProvider.LastBlockTimestamp = block.FullTimestamp;

							// a hint as to when we should expect the next one
							chainStateProvider.LastBlockLifespan = block.Lifespan;

							// lets store the block hash
							chainStateProvider.LastBlockHash = block.Hash.ToExactByteArrayCopy();

							// store the promised next signature if it is secret
							if(block.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
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

			}, (results, taskRoutingContext) => {

				if(results.Success) {
					// thats it really. now we have our block, lets update our chain stats.
					if(!isBlockAlreadyInserted) {

						// now, alert the world of this new block!
						this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInserted(block.BlockId.Value, block.FullTimestamp, block.Hash.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

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
							this.SynchronizeWallet(block, true, false);

						} catch(Exception ex) {
							Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
						}
					}

				} else {
					Log.Fatal(results.Exception, "Failed to insert block into our model.");

					// this is very critical
					results.Rethrow();
				}

			});

			this.DispatchTaskSync(serializationTask);

			return true;
		}

		public virtual bool InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, bool allowWalletSyncGrowth = true) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			//lets see if we are ready. a genesis block is alwasy ready
			if(block.BlockId.Value > 1) {

				long previousBlockId = block.BlockId.Value - 1;

				if((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
					Log.Warning("We are attempting to interpret a block but the previous installations are not complete. Syncing...");

					while((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {

						long originalBlockId = chainStateProvider.BlockHeight;
						(IBlock block, IDehydratedBlock dehydratedBlock) previousBlock = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(originalBlockId);

						if(!this.InterpretBlock(previousBlock.block, previousBlock.dehydratedBlock, syncWallet, serializationTransactionProcessor, allowWalletSyncGrowth) || (originalBlockId == chainStateProvider.BlockHeight)) {
							Log.Warning("We failed to catch up the missing blocks in interpretation.");
						}
					}
				}

				if((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {
					Log.Warning("We are attempting to interpret a block but the previous installation is not complete");

					this.SynchronizeBlockchain(true);

					return false;
				}
			}

			// launch the interpretation of this block
			void InterpretBlock(SerializationTransactionProcessor transactionalProcessor) {

				// thats it, lets begin interpretation
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight = block.BlockId.Value;
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus = ChainStateEntryFields.BlockInterpretationStatuses.Blank;

				this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(block, this, transactionalProcessor);

				this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.InterpretNewBlockSnapshots(block, this, transactionalProcessor);

			}

			// if we are not already in a transaction, we make one
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

			// now, alert the world of this new block newly interpreted!
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInterpreted(block.BlockId.Value, block.FullTimestamp, block.Hash.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

			if(syncWallet) {
				try {

					this.SynchronizeWallet(block, true, allowWalletSyncGrowth);

				} catch(Exception ex) {
					Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
				}
			}

			// meanwhile, see if we need to mine
			this.PerformElection(block);

			return true;
		}

		/// <summary>
		///     Here we perform th first part of an election and return the election results
		/// </summary>
		/// <param name="blockElectionDistillate"></param>
		/// <param name="chainEventPoolProvider"></param>
		/// <returns></returns>
		public virtual List<ElectedCandidateResultDistillate> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate) {

			return this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElectionComputations(blockElectionDistillate, this.ChainEventPoolProvider);
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

			var messages = this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults, this.ChainEventPoolProvider);

			return this.DispatchElectionMessages(messages);
		}

		/// <summary>
		///     HEre we perform an entire election with the block we have locally
		/// </summary>
		/// <param name="block"></param>
		/// <param name="async"></param>
		/// <returns></returns>
		public virtual bool PerformElection(IBlock block) {

			bool elected = false;

			try {
				if(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled) {
					// ok, we are mining, lets check this block

					if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount != 0) {
						var messages = this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElection(block, this.ChainEventPoolProvider);

						if((messages != null) && messages.Any()) {
							elected = true;
							this.DispatchElectionMessages(messages);
						}
					} else {
						Log.Error("Mining is enabled but we are not connected to any peers. Elections cancelled.");
					}
				}
			} catch(Exception ex) {
				Log.Fatal(ex, "Failed to perform mining election.");
			}

			return elected;
		}

		public virtual void InstallDigest(int digestId) {

			Log.Information($"Installing new digest Id {digestId}.");

			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<IBlockchainDigest>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {

				// first and most important, add it to our archive
				serializationTask.Results = serializationService.LoadDigestHeader(digestId);
			}, (results, taskRoutingContext) => {

				if(results.Success || (serializationTask.Results == null)) {

					IBlockchainDigest digest = serializationTask.Results;

					Log.Information($"Loaded digest Id {digest.DigestId} and Value {digest.Timestamp}.");

					// now validate
					var validationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateValidationTask<ValidationResult>();

					validationTask.SetAction((validationService, taskRoutingContext2) => {

						validationTask.Results = validationService.ValidateDigest(digest, true);

					}, (results2, taskRoutingContext2) => {

						if(results2.Success) {

							if(validationTask.Results.Invalid) {
								// failed to validate digest
								//TODO: what to do??
								throw new ApplicationException("failed to validate digest");
							}

							// ok, its good, lets install the digest
							var serializationTask2 = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<IBlockchainDigest>();

							serializationTask2.SetAction((serializationService, taskRoutingContext3) => {

								// ok, lets update the actual files and all

								// first, we generate the digest channel descriptor, for quick use
								serializationService.SaveDigestChannelDescription(digestId, digest.DigestDescriptor);

								// now the big moment, we update the entire filesystem
								serializationService.UpdateCurrentDigest(digest);

							}, (results3, taskRoutingContext3) => {

								if(results3.Success) {

									Repeater.Repeat(() => {
										// ok, now we update our states
										this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight = digestId;
										this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestBlockHeight = digest.BlockId.Value;
										this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastDigestHash = digest.Hash.ToExactByteArrayCopy();
										this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastDigestTimestamp = this.timeService.GetTimestampDateTime(digest.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);

										if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestBlockHeight > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
											// ok, this digest is ahead for us, we must now update the snapshot state
											this.UpdateAccountSnapshotFromDigest(digest);
										}
									});

									// now, alert the world of this new digest!
									this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.DigestInserted(digestId));

									// and thats it, the digest is fully installed :)
								}

							});

							this.DispatchTaskAsync(serializationTask2);

						} else {
							Log.Fatal(results2.Exception, $"Failed to validate digest id {digestId}.");

							// this is very critical
							results.Rethrow();
						}

					});

					this.DispatchTaskAsync(validationTask);

				} else {
					Log.Fatal(results.Exception, $"Failed to load digest id {digestId}.");

					// this is very critical
					results.Rethrow();
				}

			});

			this.DispatchTaskAsync(serializationTask);

		}

		public virtual void HandleBlockchainMessage(IBlockchainMessage message, IDehydratedBlockchainMessage dehydratedMessage) {

			//save the message if we need so
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MessageSavingMode == AppSettingsBase.MessageSavingModes.Enabled) {
				var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

				serializationTask.SetAction((serializationService, taskRoutingContext) => {

					// first and most important, add it to our archive
					serializationService.SerializeBlockchainMessage(dehydratedMessage);
				}, (results, taskRoutingContext) => {

					if(results.Error) {
						Log.Fatal(results.Exception, "Failed to serialize message!.");

						// this is very critical
						results.Rethrow();
					}
				});

				this.DispatchTaskAsync(serializationTask);
			}
		}

		public void DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext) {

			BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool sent = false;

			if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections && (this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount >= chainConfiguration.MinimumDispatchPeerCount)) {

				IBlockchainGossipMessageSet gossipMessageSet = this.PrepareGossipBlockchainMessageMessageSet(messageEnvelope);

				Repeater.Repeat(() => {
					// ok, we are ready. lets send it out to the world!!  :)
					this.SendGossipTransaction(gossipMessageSet);
					sent = true;
				});

				if(!sent) {
					throw new ApplicationException("Failed to send message");
				}

			} else {
				throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
			}

			if(!sent) {
				throw new ApplicationException("Failed to send message");
			}
		}

		protected void ConfirmTransactionSent(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext, long messageHash) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionSent(transactionEnvelope.Contents.Uuid.SimpleTransactionId), correlationContext);

			IndependentActionRunner.Run(() => {
				Repeater.Repeat(() => {
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionCacheEntry(transactionEnvelope.Contents.Uuid.SimpleTransactionId, WalletTransactionCache.TransactionStatuses.Dispatched, messageHash);
				});

			}, () => {

				ITransaction transaction = transactionEnvelope.Contents.RehydratedTransaction;

				Repeater.Repeat(() => {

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transactionEnvelope.Contents.Uuid.SimpleTransactionId, WalletTransactionHistory.TransactionStatuses.Dispatched);
				});
			});
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

		protected bool DispatchElectionMessages(List<IElectionCandidacyMessage> messages) {
			if((messages != null) && messages.Any()) {

				// well, seems we have messages!  lets send them out. first, prepare the envelopes

				var results = this.CentralCoordinator.ChainComponentProvider.AssemblyProviderBase.PrepareElectionMessageEnvelopes(messages);

				var sentMessages = new HashSet<Guid>();

				Repeater.Repeat(() => {
					foreach(IMessageEnvelope envelope in results) {
						// if we repeat, lets not send the successful ones more than once.
						if(!sentMessages.Contains(envelope.ID)) {
							this.DispatchNewMessage(envelope, new CorrelationContext());
							sentMessages.Add(envelope.ID);
						}
					}
				});
			}

			return true;
		}

		/// <summary>
		///     called when a new block has been successfully installed
		/// </summary>
		/// <param name="block"></param>
		protected virtual void BlockInstalled(IBlock block, IDehydratedBlock dehydratedBlock) {

		}

		/// <summary>
		///     Update the local snapshots with the connection provided by the digest
		/// </summary>
		/// <param name="digest"></param>
		protected virtual void UpdateAccountSnapshotFromDigest(IBlockchainDigest digest) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestBlockHeight > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
				// ok, this digest is ahead for us, we must now update the snapshot state

				// this is an important moment, and very rare. we will run it in the serialization thread
				var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

				serializationTask.SetAction((serializationService, taskRoutingContext) => {

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(token => {

						token.ThrowIfCancellationRequested();

						this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.ClearSnapshots();

						var walletAccounts = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts();

						token.ThrowIfCancellationRequested();

						//var walletActions = new List<Action<WALLET_MANAGER, TaskRoutingContext>>();

						// loop all accounts :)
						for(long accountSequenceId = 1; accountSequenceId < digest.LastStandardAccountId; accountSequenceId++) {

							token.ThrowIfCancellationRequested();

							AccountId accountId = new AccountId(accountSequenceId, Enums.AccountTypes.Standard);

							IAccountSnapshotDigestChannelCard accountCard = null;

							// determine if its one of ours
							IWalletAccount localAccount = walletAccounts.SingleOrDefault(a => a.PublicAccountId == accountId);

							if(localAccount != null) {

								accountCard = serializationService.LoadDigestStandardAccount(accountSequenceId);

								// ok, this is one of ours, we will need to update the wallet snapshot
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshotFromDigest(accountCard);
							}

							if(this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = serializationService.LoadDigestStandardAccount(accountSequenceId);
								}

								this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard);

								var accountKeyCards = serializationService.LoadDigestAccountKeyCards(accountSequenceId);

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

								accountCard = serializationService.LoadDigestJointAccount(accountSequenceId);

								// ok, this is one of ours, we will need to update the wallet snapshot
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshotFromDigest(accountCard);
							}

							if(this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = serializationService.LoadDigestJointAccount(accountSequenceId);
								}

								this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard);
							}
						}

						// now the accreditation certificates
						var certificates = serializationService.LoadDigestAccreditationCertificateCards();

						foreach(IAccreditationCertificateDigestChannelCard certificate in certificates) {

							token.ThrowIfCancellationRequested();

							this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateAccreditationCertificateFromDigest(certificate);
						}

					});

				}, (results, taskRoutingContext) => {

					if(results.Success) {

					} else {
						Log.Fatal(results.Exception, "Failed to update the blockchain snapshots from the digest.");

						// this is very critical
						results.Rethrow();
					}

				});

				this.DispatchTaskAsync(serializationTask);
			}
		}

		protected IBlockchainGossipMessageSet PrepareGossipTransactionMessageSet(ITransactionEnvelope transactionEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateTransactionCreatedGossipMessageSet(transactionEnvelope);

			this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected IBlockchainGossipMessageSet PrepareGossipBlockchainMessageMessageSet(IMessageEnvelope messageEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateBlockchainMessageCreatedGossipMessageSet(messageEnvelope);

			this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected void SendGossipTransaction(IBlockchainGossipMessageSet gossipMessageSet) {
			if(GlobalSettings.ApplicationSettings.P2pEnabled) {

				if(this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					Log.Warning("No peers available. Gossip message announcing new transaction is not sent");
				} else {
					// ok, we are ready. lets send it out to the world!!  :)
					this.CentralCoordinator.PostNewGossipMessage(gossipMessageSet);
				}

			} else {
				Log.Warning("p2p is not enabled. Gossip message announcing new transaction is not sent");
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

		/// <summary>
		///     every once in a while, we check for the sync status
		/// </summary>
		protected override void ProcessLoop(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext) {
			base.ProcessLoop(workflow, taskRoutingContext);

			if(this.ShouldAct(ref this.nextBlockchainSynchCheck)) {

				this.CheckBlockchainSynchronizationStatus();
			}

			if(this.ShouldAct(ref this.nextWalletSynchCheck)) {

				this.CheckWalletSynchronizationStatus();
			}

			if(this.ShouldAct(ref this.nextExpiredTransactionCheck)) {

				this.ChainEventPoolProvider.DeleteExpiredTransactions();

				this.nextExpiredTransactionCheck = DateTime.Now.AddMinutes(30);
			}
		}

		protected override void Initialize(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext) {
			base.Initialize(workflow, taskRoutingContext);

			// make sure we check our status when starting
			this.CheckBlockchainSynchronizationStatus();

			this.CheckWalletSynchronizationStatus();

			// connect to the wallet events
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().PassphraseCaptureMethod == AppSettingsBase.PassphraseQueryMethod.Event) {
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetExternalPassphraseHandlers(this.WalletProviderOnWalletPassphraseRequest, this.WalletProviderOnWalletKeyPassphraseRequest, this.WalletProviderOnWalletCopyKeyFileRequest, this.CopyWalletRequest);
			}

			bool LoadWallet() {
				try {
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletLoaded();

					return true;
				} catch(WalletNotLoadedException ex) {
					Log.Warning("Failed to load wallet. Not loaded.");
				} catch(Exception ex) {
					Log.Warning("Failed to load wallet. Not loaded.", ex);
				}

				return false;
			}

			// if we must, we load the wallet at the begining
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().LoadWalletOnStart) {
				bool isLoaded = false;
				isLoaded = LoadWallet();

				if(!isLoaded) {
					ChainConfigurations chainConfig = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

					if(chainConfig.CreateMissingWallet) {

						//TODO: passphrases? this here is mostly for debug
						// if we must, we will create a new wallet
						if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(default, chainConfig.EncryptWallet, chainConfig.EncryptWalletKeys, false, null)) {
							throw new ApplicationException("Failed to create a new wallet");
						}

						LoadWallet();
					}
				}
			}

			this.RoutedTaskRoutingReceiver.CheckTasks();
		}

	#region rpc methods

		public long GetBlockHeight() {
			return this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
		}

		public BlockchainInfo GetBlockchainInfo() {

			IChainStateProvider chainState = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			return new BlockchainInfo {
				BlockId = chainState.BlockHeight, BlockHash = ((ByteArray) chainState.LastBlockHash)?.ToBase58(), BlockTimestamp = chainState.LastBlockTimestamp, PublicBlockId = chainState.PublicBlockHeight,
				DigestId = chainState.DigestHeight, DigestHash = ((ByteArray) chainState.LastDigestHash)?.ToBase58(), DigestBlockId = chainState.DigestBlockHeight, DigestTimestamp = chainState.LastDigestTimestamp,
				PublicDigestId = chainState.PublicDigestHeight
			};
		}

	#endregion

	#region blockchain sync

		/// <summary>
		///     store if we have synced at least once since we launched the server.
		/// </summary>
		private bool hasBlockchainSyncedOnce;

		/// <summary>
		///     this method determine's if it is time to run a synchronization on our blockchain
		/// </summary>
		protected virtual void CheckBlockchainSynchronizationStatus() {

			if(this.CentralCoordinator.IsShuttingDown) {
				return;
			}

			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2pEnabled) {

				this.SynchronizeBlockchain(false);

				// lets check again in X seconds
				if(this.hasBlockchainSyncedOnce) {
					// ok, now we can wait the regular intervals
					this.nextBlockchainSynchCheck = DateTime.Now.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextBlockchainSynchCheck = DateTime.Now.AddSeconds(2);
				}
			} else {
				this.nextBlockchainSynchCheck = DateTime.MaxValue;
			}
		}

		/// <summary>
		///     Perform a blockchain sync
		/// </summary>
		public void SynchronizeBlockchain(bool force) {
			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2pEnabled) {

				if(force) {
					// let's for ce a sync by setting the chain as desynced
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTime.MinValue;
				}

				if(!this.BlockchainSyncing && !this.BlockchainSynced) {

					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// if we are not synchronized, we go ahead and do it.
					if(!this.hasBlockchainSyncedOnce || chainStateProvider.IsChainDesynced) {
						// that's it, we launch a chain sync
						lock(this.locker) {
							if(this.chainSynchWorkflow == null) {

								// ok, we did at least once
								this.hasBlockchainSyncedOnce = true;

								this.chainSynchWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateChainSynchWorkflow(this.CentralCoordinator.FileSystem);

								// when its done, we can clear it here. not necessary, but keeps things cleaner.
								this.chainSynchWorkflow.Completed += (success, workflow) => {
									lock(this.locker) {

										if(success && this.BlockchainSynced) {
											this.nextBlockchainSynchCheck = DateTime.Now.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
										} else {
											// we never synced, we need to check more often to be ready to do so
											this.nextBlockchainSynchCheck = DateTime.Now.AddSeconds(5);
										}

										this.chainSynchWorkflow = null;
									}
								};

								this.CentralCoordinator.PostWorkflow(this.chainSynchWorkflow);
							}
						}
					}
				}
			}
		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public bool BlockchainSyncing {
			get {
				lock(this.locker) {
					return (this.chainSynchWorkflow != null) && !this.chainSynchWorkflow.IsCompleted;
				}
			}
		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?
		/// </summary>
		public bool BlockchainSynced {
			get {
				lock(this.locker) {
					return (!this.BlockchainSyncing || (this.chainSynchWorkflow?.IsCompleted ?? true)) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced;
				}
			}
		}

	#endregion

	#region wallet sync

		/// <summary>
		///     store if we have synced the wallet at least once since we launched the server.
		/// </summary>
		private bool hasWalletSyncedOnce;

		/// <summary>
		///     this method determine's if it is time to run a synchronization on our blockchain
		/// </summary>
		protected virtual void CheckWalletSynchronizationStatus() {

			if(this.CentralCoordinator.IsShuttingDown) {
				return;
			}

			if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {

				this.SynchronizeWallet(false, true);

				// lets check again in X seconds
				if(this.hasWalletSyncedOnce) {
					// ok, now we can wait the regular intervals
					this.nextWalletSynchCheck = DateTime.Now.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextWalletSynchCheck = DateTime.Now.AddSeconds(2);
				}
			} else {
				this.nextWalletSynchCheck = DateTime.Now.AddSeconds(5);
			}
		}

		public virtual void SynchronizeWallet(bool force, bool? allowGrowth = null) {
			this.SynchronizeWallet((List<IBlock>) null, force, allowGrowth);
		}

		/// <summary>
		///     a new block has been received, lets sync our wallet
		/// </summary>
		/// <param name="block"></param>
		public virtual void SynchronizeWallet(IBlock block, bool force, bool? allowGrowth = null) {

			this.SynchronizeWallet(new[] {block}.ToList(), force, allowGrowth);
		}

		public virtual void SynchronizeWallet(List<IBlock> blocks, bool force, bool? allowGrowth = null) {
			this.SynchronizeWallet(blocks, force, false, allowGrowth);
		}

		public virtual void SynchronizeWallet(List<IBlock> blocks, bool force, bool mobileForce, bool? allowGrowth = null) {

			var walletSynced = this.WalletSyncedNoWait;

			if(!walletSynced.HasValue) {
				// we could not verify, try again later
				this.nextWalletSynchCheck = DateTime.Now.AddSeconds(1);

				return;
			}

			if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded && ((mobileForce && GlobalSettings.ApplicationSettings.MobileMode) || (!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableWalletSync && force) || !walletSynced.Value)) {
				lock(this.locker) {
					if(this.synchWalletWorkflow == null) {

						this.hasWalletSyncedOnce = true;
						this.synchWalletWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateSyncWalletWorkflow();

						this.synchWalletWorkflow.AllowGrowth = allowGrowth;

						if(blocks?.Any() ?? false) {
							foreach(IBlock block in blocks.Where(b => b != null).OrderBy(b => b.BlockId.Value)) {
								this.synchWalletWorkflow.LoadedBlocks.Add(block.BlockId, block);
							}
						}

						// when its done, we can clear it here. not necessary, but keeps things cleaner.
						this.synchWalletWorkflow.Completed += (success, workflow) => {
							lock(this.locker) {

								// ok, now we can wait the regular intervals
								if((success && this.WalletSynced) || this.BlockchainSyncing) {
									this.nextWalletSynchCheck = this.nextWalletSynchCheck = DateTime.Now.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
								} else {
									this.nextWalletSynchCheck = DateTime.Now.AddSeconds(5);
								}

								this.synchWalletWorkflow = null;
							}
						};

						this.CentralCoordinator.PostWorkflow(this.synchWalletWorkflow);
					}
				}
			}

		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public bool WalletSyncing {
			get {
				lock(this.locker) {
					return (this.synchWalletWorkflow != null) && !this.synchWalletWorkflow.IsCompleted;
				}
			}
		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public bool? WalletSyncedNoWait {
			get {
				lock(this.locker) {
					var walletSynced = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

					if(!walletSynced.HasValue) {
						return null;
					}

					return (!this.WalletSyncing || (this.synchWalletWorkflow?.IsCompleted ?? true)) && walletSynced.Value;
				}
			}
		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public bool WalletSynced {
			get {
				lock(this.locker) {
					return (!this.WalletSyncing || (this.synchWalletWorkflow?.IsCompleted ?? true)) && this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced;
				}
			}
		}

		/// <summary>
		///     a special method to sync the wallet and chain from an external source.
		/// </summary>
		/// <param name="synthesizedBlock"></param>
		public void SynchronizeBlockchainExternal(string synthesizedBlock) {
			if(GlobalSettings.ApplicationSettings.MobileMode) {
				SynthesizedBlockAPI synthesizedBlockApi = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.DeserializeSynthesizedBlockAPI(synthesizedBlock);
				SynthesizedBlock synthesizedBlockInstance = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ConvertApiSynthesizedBlock(synthesizedBlockApi);

				// lets cache the results
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CacheSynthesizedBlock(synthesizedBlockInstance);

				if(synthesizedBlockApi.BlockId == 1) {

					// that's pretty important
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception = synthesizedBlockApi.SynthesizedGenesisBlockBase.Inception;
				}

				// we set the chain height to this block id
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight = synthesizedBlockInstance.BlockId;
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight = synthesizedBlockInstance.BlockId;

				// ensure that we run the general transactions
				string cachePath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetGeneralCachePath();

				using(SerializationTransactionProcessor serializationTransactionProcessor = new SerializationTransactionProcessor(cachePath, this.CentralCoordinator.FileSystem)) {
					this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(synthesizedBlockInstance, this, serializationTransactionProcessor);

					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight = synthesizedBlockInstance.BlockId;
					serializationTransactionProcessor.Commit();
				}

				// do a super force for mobile
				this.SynchronizeWallet(null, true, true, true);
			} else {
				throw new ApplicationException("This can only be invoked in mobile mode.");
			}
		}

	#endregion

	#region wallet manager

		private void CopyWalletRequest(CorrelationContext correlationContext, int attempt) {
			Log.Information("Requesting loading wallet.");

			AutoResetEvent resetEvent = new AutoResetEvent(false);

			LoadWalletSystemMessageTask loadWalletTask = new LoadWalletSystemMessageTask(() => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletTask);

			// wait up to 5 minutes for the wallet to be ready to load
			resetEvent.WaitOne(TimeSpan.FromMinutes(5));

			int g = 0;
		}

		private SecureString WalletProviderOnWalletPassphraseRequest(CorrelationContext correlationContext, int attempt) {
			Log.Information("Requesting wallet passphrase.");
			AutoResetEvent resetEvent = new AutoResetEvent(false);

			if(correlationContext.IsNew) {
				correlationContext.InitializeNew();
			}

			RequestWalletPassphraseSystemMessageTask loadWalletPassphraseTask = new RequestWalletPassphraseSystemMessageTask(attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletPassphraseTask, correlationContext);

			// wait until we get the passphrase back
			resetEvent.WaitOne();

			return loadWalletPassphraseTask.Passphrase;
		}

		private SecureString WalletProviderOnWalletKeyPassphraseRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");
			AutoResetEvent resetEvent = new AutoResetEvent(false);

			RequestWalletKeyPassphraseSystemMessageTask loadWalletKeyPasshraseTask = new RequestWalletKeyPassphraseSystemMessageTask(accountuuid, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletKeyPasshraseTask);

			// wait up to 5 hours for the wallet to be ready to load
			resetEvent.WaitOne(TimeSpan.FromHours(5));

			return loadWalletKeyPasshraseTask.Passphrase;
		}

		private void WalletProviderOnWalletCopyKeyFileRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");
			AutoResetEvent resetEvent = new AutoResetEvent(false);

			RequestWalletKeyCopyFileSystemMessageTask loadWalletKeyCopyFileTask = new RequestWalletKeyCopyFileSystemMessageTask(accountuuid, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletKeyCopyFileTask);

			// wait up to 5 hours for the wallet to be ready to load
			resetEvent.WaitOne(TimeSpan.FromHours(5));
		}

		public void ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases) {

			this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(token => {
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases);
			});

		}

	#endregion

	}
}