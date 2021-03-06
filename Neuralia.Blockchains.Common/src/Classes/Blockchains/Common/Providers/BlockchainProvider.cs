using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IBlockchainProvider : IChainProvider {
		IEventPoolProvider ChainEventPoolProvider { get; }

		Task InsertLocalTransaction(ITransactionEnvelope signedTransactionEnvelope, string note,  WalletTransactionHistory.TransactionStatuses status, CorrelationContext correlationContext, LockContext lockContext);
		Task InsertGossipTransaction(ITransactionEnvelope signedTransactionEnvelope, LockContext lockContext);

		Task<bool> InstallGenesisBlock(IGenesisBlock genesisBlock, IDehydratedBlock dehydratedBlock, LockContext lockContext);

		Task<bool> InsertInterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, LockContext lockContext, bool allowWalletSyncGrowth = true);
		Task<bool> InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext, bool allowWalletSyncGrowth = true);
		Task<bool> InsertBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, LockContext context, bool useTransaction = true);

		Task<bool> InstallDigest(int digestId, LockContext lockContext);
		
		Task HandleBlockchainMessage(IBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext);
		long GetBlockHeight();
		int GetDigestHeight();
		BlockchainInfo GetBlockchainInfo();

		bool AttemptLockBlockDownload(BlockId blockId);
		void FreeLockedBlock(BlockId blockId);
		Task<T> PerformAtomicChainHeightOperation<T>(Func<LockContext, Task<T>> action, LockContext lockContext);

		Task PerformElection(IBlock block, LockContext lockContext);
		Task<List<ElectedCandidateResultDistillate>> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate, LockContext lockContext);
		Task<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, LockContext lockContext);

		Task SynchronizeBlockchainExternal(string synthesizedBlock, LockContext lockContext);
		Task SynchronizeBlockchainExternalBatch(IEnumerable<string> synthesizedBlocks, LockContext lockContext);
	}

	public interface IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockchainProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class BlockchainProvider {

	}

	public abstract class BlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IBlockchainProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		private readonly Dictionary<BlockId, DateTime> blockDownloadLockCache = new Dictionary<BlockId, DateTime>();

		private readonly object blockDownloadLockCacheLocker = new object();
		protected readonly IBlockchainGuidService guidService;

		private readonly RecursiveAsyncLock insertBlockLocker = new RecursiveAsyncLock();
		private readonly RecursiveAsyncLock interpretBlockLocker = new RecursiveAsyncLock();
		protected readonly IBlockchainTimeService timeService;

		private IEventPoolProvider chainEventPoolProvider;
		
		private long lastIncompleteBlock;
		

		public BlockchainProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;

			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;
		}

		protected CENTRAL_COORDINATOR CentralCoordinator { get; }

		private readonly WrapperConcurrentQueue<IBlock> queuedElectionBlocks = new WrapperConcurrentQueue<IBlock>();
		
		public override async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);

			// make sure to process any queued blocks to process in election
			this.CentralCoordinator.WalletSynced += async lc => {
				
				try {
					while(this.queuedElectionBlocks.TryDequeue(out var block)){
						await this.PerformElection(block, lc).ConfigureAwait(false);
					}
				} catch(Exception ex){
					this.CentralCoordinator.Log.Error(ex, "Failed to process elections");
				}
			};
		}
		
		/// <summary>
		///     allows to lock a block for a download operations.
		/// </summary>
		/// <returns>true if lock was acquired, false if not.</returns>
		public bool AttemptLockBlockDownload(BlockId blockId) {
			lock(this.blockDownloadLockCacheLocker) {
				foreach(KeyValuePair<BlockId, DateTime> timedOut in this.blockDownloadLockCache.Where(e => e.Value < (DateTimeEx.CurrentTime - TimeSpan.FromSeconds(30)))) {
					this.blockDownloadLockCache.Remove(timedOut.Key);
				}

				if(!this.blockDownloadLockCache.ContainsKey(blockId)) {
					this.blockDownloadLockCache.Add(blockId, DateTimeEx.CurrentTime);

					return true;
				}

				return false;
			}
		}

		public void FreeLockedBlock(BlockId blockId) {
			lock(this.blockDownloadLockCacheLocker) {
				foreach(BlockId entry in this.blockDownloadLockCache.Keys.Where(k => k <= blockId)) {
					this.blockDownloadLockCache.Remove(entry);
				}
			}
		}

		/// <summary>
		///     run a method while ensuring that no insert or interpret is in process.
		/// </summary>
		/// <param name="action"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public async Task<T> PerformAtomicChainHeightOperation<T>(Func<LockContext, Task<T>> action, LockContext lockContext) {
			if(action != null) {
				using(LockHandle handle = await this.insertBlockLocker.LockAsync(lockContext).ConfigureAwait(false)) {
					using(LockHandle handle2 = await this.interpretBlockLocker.LockAsync(handle).ConfigureAwait(false)) {
						return await action(handle2).ConfigureAwait(false);
					}
				}
			}

			return default;
		}

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
		/// <param name="signedTransactionEnvelope"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task InsertLocalTransaction(ITransactionEnvelope signedTransactionEnvelope, string note,  WalletTransactionHistory.TransactionStatuses status , CorrelationContext correlationContext, LockContext lockContext) {
			//TODO: getting here would be a hack by an ill intended peer, should we log the peer's bad behavior?
			if(signedTransactionEnvelope.Contents.RehydratedEvent is IGenesisAccountPresentationTransaction) {
				throw new ApplicationException("Genesis transactions can not be added this way");
			}

			this.CentralCoordinator.Log.Verbose($"Inserting new local transaction with Id {signedTransactionEnvelope.Contents.Uuid}");

			// ok, now we will want to send out a gossip message to inform others that we have a new transaction.

			// first step, lets add this new transaction to our own wallet pool
			ITransaction transaction = signedTransactionEnvelope.Contents.RehydratedEvent;

			try {
				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertTransactionHistoryEntry(transaction, true, note, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight, status, lockContext).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "failed to add transaction to local history");
			}

			try {
				await Repeater.RepeatAsync(() => this.AddTransactionToEventPool(signedTransactionEnvelope, lockContext)).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "failed to add transaction to local event pool");
			}

			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionCreated(signedTransactionEnvelope.Contents.Uuid));
		}

		/// <summary>
		///     We received a transaction as a gossip message. lets add it to our
		///     transaction cache if required
		/// </summary>
		/// <param name="signedTransactionEnvelope"></param>
		/// <param name="lockContext"></param>
		public virtual Task InsertGossipTransaction(ITransactionEnvelope signedTransactionEnvelope, LockContext lockContext) {
			this.CentralCoordinator.Log.Verbose($"Received new gossip transaction with Id {signedTransactionEnvelope.Contents.Uuid} from peers.");

			return this.AddTransactionToEventPool(signedTransactionEnvelope, lockContext);
		}

		public virtual async Task<bool> InstallGenesisBlock(IGenesisBlock genesisBlock, IDehydratedBlock dehydratedBlock, LockContext context) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight != 0) {
				throw new ApplicationException("the genesis block must absolutely be the first block in the chain");
			}

			this.CentralCoordinator.Log.Information($"Installing genesis block with Id {genesisBlock.BlockId} and Timestamp {genesisBlock.FullTimestamp}.");

			try {
				// first and most important, add it to our archive
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlock(dehydratedBlock);

				// if Key dictionary are enabled, then we create the base directory and first file
				this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.EnsureKeyDictionaryIndex();

				// thats it really. now we have our block, lets update our chain stats.

				// ready to move to the next step
				IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

				await Repeater.RepeatAsync(async () => {
					List<Func<IChainStateProvider, string[]>> actions = new List<Func<IChainStateProvider, string[]>>();

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

					await chainStateProvider.UpdateFields(actions).ConfigureAwait(false);

					// store the promised next signature if it is secret
					// if(genesisBlock.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
					// 	chainStateProvider.InsertModeratorKey(new TransactionId(), genesisBlock.SignatureSet.NextModeratorKey, genesisBlock.SignatureSet.ConvertToDehydratedKey());
					// }
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Fatal(ex, "Failed to insert genesis blocks into our model.");

				// this is very critical
				throw;
			}

			// now inform the wallet manager that a new block has been received
			await this.CentralCoordinator.RequestWalletSync(genesisBlock, true, false).ConfigureAwait(false);

			return true;
		}

		public virtual Task<bool> InsertInterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, LockContext lockContext, bool allowWalletSyncGrowth = true) {

			return this.PerformAtomicChainHeightOperation(async lc => {
				IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

				if(chainStateProvider.DiskBlockHeight == (block.BlockId.Value - 1)) {
					// good to go!
					if(!await this.InsertBlock(block, dehydratedBlock, syncWallet, lc).ConfigureAwait(false)) {
						return false;
					}
				}

				bool currentIncomplete = (chainStateProvider.BlockHeight == block.BlockId.Value) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted);
				bool previousCompleted = (chainStateProvider.BlockHeight == (block.BlockId.Value - 1)) && (chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted);

				if(currentIncomplete || previousCompleted) {
					// good to go
					return await this.InterpretBlock(block, dehydratedBlock, syncWallet, null, lc, allowWalletSyncGrowth).ConfigureAwait(false);
				}

				return false;
			}, lockContext);
		}

		/// <summary>
		/// update the block signature delta cache
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		protected virtual Task<SafeArrayHandle> UpdateModeratorKeyDelta(IBlock block) {
		
			return UpdateModeratorKeyDelta(block.SignatureSet.ModeratorKeyOrdinal, block.SignatureSet.BlockSignature.Autograph);
		}
		
		protected virtual async Task<SafeArrayHandle> UpdateModeratorKeyDelta(byte moderatorKeyOrdinal, SafeArrayHandle autograph) {
			SafeArrayHandle lastBlockXmssKeySignaturePathCache = null;

			try {
				var entry = await this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKeyAndIndex(moderatorKeyOrdinal).ConfigureAwait(false);

				if(entry.key is IXmssCryptographicKey xmssCryptographicKey) {
					lastBlockXmssKeySignaturePathCache = SafeArrayHandle.CreateClone(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockXmssKeySignaturePathCache);

					using XMSSProvider provider = new XMSSProvider(xmssCryptographicKey.HashType, xmssCryptographicKey.BackupHashType, xmssCryptographicKey.TreeHeight, GlobalSettings.ApplicationSettings.XmssThreadMode);

					provider.Initialize();

					if(lastBlockXmssKeySignaturePathCache == null || lastBlockXmssKeySignaturePathCache.IsZero) {
						using var cache = provider.CreateSignaturePathCache();
						lastBlockXmssKeySignaturePathCache = (SafeArrayHandle) cache.Save();
					}

					lastBlockXmssKeySignaturePathCache = provider.UpdateFromSignature(autograph, lastBlockXmssKeySignaturePathCache);
				}
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to update moderator key delta");

				throw;
			}

			return lastBlockXmssKeySignaturePathCache;
		}
		
		public virtual async Task<bool> InsertBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, LockContext lockContext, bool useTransaction = true) {

			// make sure we have enough memory to proceed.
			if(!(await this.CentralCoordinator.CheckAvailableMemory().ConfigureAwait(false))) {
				throw new ApplicationException("Not enough memory. cannot insert.");
			}

			using(LockHandle handle = await this.insertBlockLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

				if((chainStateProvider.DiskBlockHeight == 0) || block is IGenesisBlock || (block.BlockId.Value == 1)) {
					if((block.BlockId.Value == 1) && (chainStateProvider.DiskBlockHeight == 0) && block is IGenesisBlock genesisBlock) {
						// ok, this is a genesisModeratorAccountPresentation block, we install its
						await this.InstallGenesisBlock(genesisBlock, dehydratedBlock, handle).ConfigureAwait(false);

						return true;
					}

					throw new ApplicationException("A genesis block must first exist before a standard block can be added");
				}

				if(chainStateProvider.DiskBlockHeight < (block.BlockId.Value - 1)) {
					this.CentralCoordinator.Log.Warning($"The block '{block.BlockId.Value}' received is further ahead than where we are. The chain must be synced");

					return false;
				}

				if(chainStateProvider.DiskBlockHeight >= block.BlockId.Value) {
					this.CentralCoordinator.Log.Warning("We are attempting to install a block that we already have");

					return false;
				}

				this.CentralCoordinator.Log.Information($"Installing block Id {block.BlockId} and Timestamp {block.FullTimestamp}.");

				// if we are already at the height intended, then the block is inserted
				bool isBlockAlreadyInserted = chainStateProvider.DiskBlockHeight >= block.BlockId.Value;

				// first and most important, add it to our archive
				if(!isBlockAlreadyInserted) {
					// lets transaction this operatoin

					async Task InsertBlockData() {
						this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlock(dehydratedBlock);

						// lets update our chain
						await Repeater.RepeatAsync(async () => {
							List<Func<IChainStateProvider, string[]>> actions = new List<Func<IChainStateProvider, string[]>>();

							actions.Add(prov => prov.SetDiskBlockHeightField(block.BlockId.Value));
							actions.Add(prov => prov.SetLastBlockTimestampField(block.FullTimestamp));

							// a hint as to when we should expect the next one
							actions.Add(prov => prov.SetLastBlockLifespanField(block.Lifespan));

							// lets store the block hash
							actions.Add(prov => prov.SetLastBlockHashField(block.Hash.ToExactByteArrayCopy()));

							SafeArrayHandle lastBlockXmssKeySignaturePathCache = await this.UpdateModeratorKeyDelta(block).ConfigureAwait(false);
							
							if(lastBlockXmssKeySignaturePathCache != null && ! lastBlockXmssKeySignaturePathCache.IsZero) {
								// in case we sometimes need it, we cache the previous signature path
								await CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CachePreviousBlockXmssKeySignaturePathCache().ConfigureAwait(false);
								
								actions.Add(prov => prov.SetLastBlockXmssKeySignaturePathCacheField(lastBlockXmssKeySignaturePathCache.ToExactByteArrayCopy()));
							}

							await chainStateProvider.UpdateFields(actions).ConfigureAwait(false);

							if(chainStateProvider.DiskBlockHeight == chainStateProvider.PublicBlockHeight) {
								// seems we are fully synced, lets mark the sync status as such
								this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTimeEx.CurrentTime;
							}

							// store the promised next signature if it is secret
							// if(block.SignatureSet.NextModeratorKey == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
							// 	chainStateProvider.UpdateModeratorKey(new TransactionId(), block.SignatureSet.NextModeratorKey, block.SignatureSet.ConvertToDehydratedKey());
							// } else 
							
							await chainStateProvider.UpdateModeratorExpectedNextKeyIndex(block.SignatureSet.ModeratorKeyOrdinal, block.SignatureSet.BlockSignature.KeyUseIndex.KeyUseSequenceId.Value, block.SignatureSet.BlockSignature.KeyUseIndex.KeyUseIndex+1).ConfigureAwait(false);

						}).ConfigureAwait(false);
					}

					if(useTransaction) {
						using IBlockInsertionTransactionProcessor blockStateSnapshotProcessor = this.CreateBlockInsertionTransactionProcessor(block.SignatureSet.ModeratorKeyOrdinal);

						await blockStateSnapshotProcessor.CreateSnapshot().ConfigureAwait(false);
						
						await InsertBlockData().ConfigureAwait(false);

						blockStateSnapshotProcessor.Commit();
					} else {
						await InsertBlockData().ConfigureAwait(false);
					}
				}

				// thats it really. now we have our block, lets update our chain stats.
				if(!isBlockAlreadyInserted) {
					// now, alert the world of this new block!
					this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInserted(block.BlockId.Value, block.FullTimestamp, block.Hash.Entry.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

					try {
						this.BlockInstalled(block, dehydratedBlock);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Fatal(ex, "Failed to invoke block installed callback.");
					}

					try {
						await Repeater.RepeatAsync(() => {
							// Now we clear the transaction pool of any transactions contained in this block
							return this.ClearTransactionPoolBlockTransactions(block, handle);
						}).ConfigureAwait(false);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Fatal(ex, "Failed to clear the transaction pool of block transactions.");
					}
				}

				if(syncWallet) {
					try {
						await this.CentralCoordinator.RequestWalletSync(block, true, false).ConfigureAwait(false);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
					}
				}

				return true;
			}

		}

		public virtual async Task<bool> InterpretBlock(IBlock block, IDehydratedBlock dehydratedBlock, bool syncWallet, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext, bool allowWalletSyncGrowth = true) {

			using(LockHandle handle = await this.interpretBlockLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

				//lets see if we are ready. a genesis block is alwasy ready
				if(block.BlockId.Value > 1) {
					long previousBlockId = block.BlockId.Value - 1;

					if((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
						this.CentralCoordinator.Log.Warning($"We are attempting to interpret block {block.BlockId.Value} but the previous installations are not complete. Current chain height is at {chainStateProvider.BlockHeight}. Syncing...");

						while((chainStateProvider.BlockHeight < previousBlockId) || ((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
							long originalBlockId = chainStateProvider.BlockHeight;
							(IBlock block1, IDehydratedBlock dehydratedBlock1) = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(originalBlockId);

							if(block1 == null) {
								throw new ApplicationException($"Previous block id {originalBlockId} could not be loaded!");
							}

							this.CentralCoordinator.Log.Information($"We are interpreting previous block {originalBlockId}.");

							if(!await this.InterpretBlock(block1, dehydratedBlock1, syncWallet, serializationTransactionProcessor, handle, allowWalletSyncGrowth).ConfigureAwait(false) || ((originalBlockId == chainStateProvider.BlockHeight) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted))) {
								this.CentralCoordinator.Log.Warning("We failed to catch up the missing blocks in interpretation.");
							}
						}
					}

					if((chainStateProvider.BlockHeight == previousBlockId) && (chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {
						this.CentralCoordinator.Log.Warning("We are attempting to interpret a block but the previous installation is not complete");

						if(this.lastIncompleteBlock == 0) {
							await this.CentralCoordinator.RequestBlockchainSync(true).ConfigureAwait(false);

							this.lastIncompleteBlock = chainStateProvider.BlockHeight;

							return false;
						}

						this.lastIncompleteBlock = 0;

						// lets try to interpret
					}
				}

				// launch the interpretation of this block
				async Task InterpretBlockLocal(SerializationTransactionProcessor transactionalProcessor) {
					this.CentralCoordinator.Log.Verbose($"Interpreting block id {block.BlockId}.");

					// thats it, lets begin interpretation only if ti was not already performed or completed
					if(chainStateProvider.BlockHeight == (block.BlockId.Value - 1)) {
						List<Func<IChainStateProvider, string[]>> actions = new List<Func<IChainStateProvider, string[]>>();

						actions.Add(prov => prov.SetBlockHeightField(block.BlockId.Value));
						actions.Add(prov => prov.SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses.Blank));

						await chainStateProvider.UpdateFields(actions).ConfigureAwait(false);
					} else if(chainStateProvider.BlockHeight < (block.BlockId.Value - 1)) {
						throw new ArgumentException("Block Id is too low for interpretation");
					}

					if(chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted) {
						return;
					}

					await this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(block, transactionalProcessor, handle).ConfigureAwait(false);

					await this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.InterpretNewBlockSnapshots(block, transactionalProcessor, handle).ConfigureAwait(false);
				}

				// if we are not already in a transaction, we make one
				if((chainStateProvider.BlockHeight == block.BlockId.Value) && (chainStateProvider.BlockInterpretationStatus == ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {
					// block has already been interpreted. go no further
					return true;
				}

				if(serializationTransactionProcessor == null) {
					string cachePath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetGeneralCachePath();

					using(serializationTransactionProcessor = new SerializationTransactionProcessor(cachePath, this.CentralCoordinator)) {
						await InterpretBlockLocal(serializationTransactionProcessor).ConfigureAwait(false);

						serializationTransactionProcessor.Commit();
					}
				} else {
					await InterpretBlockLocal(serializationTransactionProcessor).ConfigureAwait(false);
				}

				if(chainStateProvider.BlockInterpretationStatus != ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted) {
					throw new ApplicationException($"Failed to interpret block id {block.BlockId}.");
				}

				// thats it, we are at this block level now
				chainStateProvider.BlockHeight = block.BlockId.Value;

				try {
					this.BlockInterpreted(block, dehydratedBlock, handle);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Fatal(ex, "Failed to invoke block installed callback.");
				}

				this.CentralCoordinator.Log.Verbose($"block {block.BlockId.Value} has been successfully interpreted.");

				// now, alert the world of this new block newly interpreted!
				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.BlockInterpreted(block.BlockId.Value, block.FullTimestamp, block.Hash.Entry.ToBase58(), chainStateProvider.PublicBlockHeight, block.Lifespan));

				// perform election only if we are up to date
				if(syncWallet) {
					try {
						if(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled) {
							
							this.queuedElectionBlocks.Enqueue(block);
						}

						await this.CentralCoordinator.RequestWalletSync(block, true, allowWalletSyncGrowth).ConfigureAwait(false);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Fatal(ex, "Failed to insert block into wallet. Not critical, block insertion continuing still...");
					}
				} else {
					// we can give it a try
					await this.PerformElection(block, handle).ConfigureAwait(false);
				}

				return true;
			}

		}

		/// <summary>
		///     Here we perform th first part of an election and return the election results
		/// </summary>
		/// <param name="blockElectionDistillate"></param>
		/// <param name="chainEventPoolProvider"></param>
		/// <returns></returns>
		public virtual Task<List<ElectedCandidateResultDistillate>> PerformElectionComputation(BlockElectionDistillate blockElectionDistillate, LockContext lockContext) {
			return this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElectionComputations(blockElectionDistillate, lockContext);
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
		public virtual async Task<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, LockContext lockContext) {
			List<IElectionCandidacyMessage> messages = await this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults, lockContext).ConfigureAwait(false);

			var result = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchElectionMessages(messages, lockContext).ConfigureAwait(false);

			if(result == ChainNetworkingProvider.DispatchedMethods.Failed) {
				throw new ApplicationException("Failed to dispatch Election Candidacy Messages");
			}

			return true;
		}

		/// <summary>
		///     HEre we perform an entire election with the block we have locally
		/// </summary>
		/// <param name="block"></param>
		/// <param name="async"></param>
		/// <returns></returns>
		public virtual async Task PerformElection(IBlock block, LockContext lockContext) {

			if(block == null) {
				return;
			}
			try {
				if(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled) {
					// ok, we are mining, lets check this block

					if(GlobalSettings.ApplicationSettings.MobileMode || this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections) {
						await this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.PerformElection(block, messages => {

							return this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.DispatchElectionMessages(messages, lockContext);
						}, lockContext).ConfigureAwait(false);
					} else {
						this.CentralCoordinator.Log.Error($"Mining is enabled but we are not connected to any peers. Elections cancelled for blockId {block.BlockId}.");
					}
				}
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, $"Failed to perform mining election for blockId {block.BlockId}.");
			}
		}

		public virtual async Task<bool> InstallDigest(int digestId, LockContext lockContext) {
			this.CentralCoordinator.Log.Information($"Installing new digest Id {digestId}.");

			// first and most important, add it to our archive
			IBlockchainDigest digest = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestHeader(digestId);

			this.CentralCoordinator.Log.Information($"Loaded digest Id {digest.DigestId} and Timestamp {digest.FullTimestamp}.");

			// now validate

			try {
				ValidationResult result = await this.CentralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateDigest(digest, true, lockContext).ConfigureAwait(false);

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

				await Repeater.RepeatAsync(async () => {
					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// ok, now we update our states
					chainStateProvider.DigestHeight = digestId;
					chainStateProvider.DigestBlockHeight = digest.BlockId.Value;
					chainStateProvider.LastDigestHash = digest.Hash.ToExactByteArrayCopy();
					chainStateProvider.LastDigestTimestamp = this.timeService.GetTimestampDateTime(digest.Timestamp.Value, chainStateProvider.ChainInception);
					chainStateProvider.LastBlockHash = digest.BlockHash.Entry.ToExactByteArrayCopy();
					
					if(chainStateProvider.DigestBlockHeight > chainStateProvider.BlockHeight) {
						// ok, this digest is ahead for us, we must now update the snapshot state
						await this.UpdateAccountSnapshotFromDigest(digest, lockContext).ConfigureAwait(false);
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
				}).ConfigureAwait(false);

				// now, alert the world of this new digest!
				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.DigestInserted(digestId, digest.FullTimestamp, digest.Hash.Entry.ToBase58()));

				// and thats it, the digest is fully installed :)
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Fatal(ex, $"Failed to validate digest id {digestId}.");

				// this is very critical
				throw new ApplicationException($"Failed to validate digest id {digestId}.");
			}

			return true;
		}

		public virtual Task HandleBlockchainMessage(IBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext) {

			//save the message if we need so
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MessageSavingMode == AppSettingsBase.MessageSavingModes.Enabled) {
				try {
					// first and most important, add it to our archive
					this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SerializeBlockchainMessage(messageEnvelope.Contents);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Fatal(ex, "Failed to serialize message!.");

					// this is very critical
					throw;
				}
			}
			
			if(message is IAppointmentBlockchainMessage appointmentMessage) {
				return this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.HandleBlockchainMessage(appointmentMessage, messageEnvelope, lockContext);
			}
			
			return Task.CompletedTask;
		}

		/// <summary>
		///     a special method to sync the wallet and chain from an external source.
		/// </summary>
		/// <param name="synthesizedBlock"></param>
		public Task SynchronizeBlockchainExternal(string synthesizedBlock, LockContext lockContext) {
			return this.SynchronizeBlockchainExternalBatch(new[] {synthesizedBlock}, lockContext);
		}

		/// <summary>
		///     a special method to sync the wallet and chain from an external source.
		/// </summary>
		/// <param name="synthesizedBlocks"></param>
		public async Task SynchronizeBlockchainExternalBatch(IEnumerable<string> synthesizedBlocks, LockContext lockContext) {
			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				
				List<SynthesizedBlockAPI> synthesizedBlockAPIs = new List<SynthesizedBlockAPI>();

				foreach(string synthesizedBlock in synthesizedBlocks) {
					synthesizedBlockAPIs.Add(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.DeserializeSynthesizedBlockAPI(synthesizedBlock));
				}

				foreach(SynthesizedBlockAPI synthesizedBlockAPI in synthesizedBlockAPIs.OrderBy(p => p.BlockId)) {
					
					SynthesizedBlock synthesizedBlockInstance = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ConvertApiSynthesizedBlock(synthesizedBlockAPI, lockContext).ConfigureAwait(false);

					using IBlockInsertionTransactionProcessor blockStateSnapshotProcessor = this.CreateBlockInsertionTransactionProcessor(0);

					// lets cache the results
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CacheSynthesizedBlock(synthesizedBlockInstance, lockContext).ConfigureAwait(false);

					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// lets update our chain
					await Repeater.RepeatAsync(async () => {
						List<Func<IChainStateProvider, string[]>> actions = new List<Func<IChainStateProvider, string[]>>();

						if(synthesizedBlockAPI.BlockId == 1) {
							actions.Add(prov => prov.SetChainInceptionField(DateTime.ParseExact(synthesizedBlockAPI.SynthesizedGenesisBlockBase.Inception, "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)));
						}

						// we set the chain height to this block id
						actions.Add(prov => prov.SetDownloadBlockHeightField(synthesizedBlockInstance.BlockId));
						actions.Add(prov => prov.SetDiskBlockHeightField(synthesizedBlockInstance.BlockId));
						actions.Add(prov => prov.SetPublicBlockHeightField(synthesizedBlockInstance.BlockId));

						await chainStateProvider.UpdateFields(actions).ConfigureAwait(false);

					}).ConfigureAwait(false);

					// ensure that we run the general transactions
					string cachePath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetGeneralCachePath();

					using SerializationTransactionProcessor serializationTransactionProcessor = new SerializationTransactionProcessor(cachePath, this.CentralCoordinator);
					await this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(synthesizedBlockInstance, serializationTransactionProcessor, lockContext).ConfigureAwait(false);

					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight = synthesizedBlockInstance.BlockId;
					
					serializationTransactionProcessor.Commit();
					blockStateSnapshotProcessor.Commit();
				}

				// do a super force for mobile
				await this.CentralCoordinator.RequestWalletSync(null, true, true, true).ConfigureAwait(false);
				
			} else {
				throw new ApplicationException("This can only be invoked in mobile mode.");
			}
		}

		public virtual async Task<bool> EnsureBlockInstallInterpreted(IBlock block, bool syncWallet, LockContext lockContext) {
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// ok, the block was inserted, but its not complete?
			if((chainStateProvider.BlockHeight == block.BlockId.Value) && !chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.InterpretationCompleted)) {
				// seems we need to interpret this block
				(IBlock block, IDehydratedBlock dehydratedBlock) blockData = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockAndMetadata(chainStateProvider.BlockHeight);

				if(blockData == default) {
					throw new ApplicationException($"Failed to load block Id {chainStateProvider.BlockHeight}. could not interpet block, nor install block ID {block.BlockId.Value}");
				}

				await this.InterpretBlock(blockData.block, blockData.dehydratedBlock, syncWallet, null, lockContext).ConfigureAwait(false);
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
		protected virtual void BlockInterpreted(IBlock block, IDehydratedBlock dehydratedBlock, LockContext lockContext) {
		}

		/// <summary>
		///     Update the local snapshots with the connection provided by the digest
		/// </summary>
		/// <param name="digest"></param>
		protected virtual async Task UpdateAccountSnapshotFromDigest(IBlockchainDigest digest, LockContext lockContext) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestBlockHeight > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
				// ok, this digest is ahead for us, we must now update the snapshot state

				// this is an important moment, and very rare. we will run it in the serialization thread

				try {
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {
						token.ThrowIfCancellationRequested();

						await this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.ClearSnapshots().ConfigureAwait(false);

						List<IWalletAccount> walletAccounts = await provider.GetAccounts(lc).ConfigureAwait(false);

						token.ThrowIfCancellationRequested();

						// loop all accounts :)
						List<AccountId> accounts = new List<AccountId>();

						for(long accountSequenceId = 1; accountSequenceId <= digest.LastUserAccountId; accountSequenceId++) {
							accounts.Add(new AccountId(accountSequenceId, Enums.AccountTypes.User));
						}
						for(long accountSequenceId = 1; accountSequenceId <= digest.LastServerAccountId; accountSequenceId++) {
							accounts.Add(new AccountId(accountSequenceId, Enums.AccountTypes.Server));
						}
						for(long accountSequenceId = 1; accountSequenceId <= digest.LastModeratorAccountId; accountSequenceId++) {
							accounts.Add(new AccountId(accountSequenceId, Enums.AccountTypes.Moderator));
						}

						foreach(var accountId in accounts){
							token.ThrowIfCancellationRequested();
						
							IAccountSnapshotDigestChannelCard accountCard = null;

							// determine if its one of ours
							IWalletAccount localAccount = walletAccounts.SingleOrDefault(a => a.PublicAccountId == accountId);

							if(localAccount != null) {
								accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccount(accountId.ToLongRepresentation());

								// ok, this is one of ours, we will need to update the wallet snapshot
								await provider.UpdateWalletSnapshotFromDigest(accountCard, lc).ConfigureAwait(false);
							}

							Task<bool> task = this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId);

							if(task.WaitAndUnwrapException(token)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccount(accountId.ToLongRepresentation());
								}

								await this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard).ConfigureAwait(false);

								List<IStandardAccountKeysDigestChannelCard> accountKeyCards = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccountKeyCards(accountId.ToLongRepresentation());

								foreach(IStandardAccountKeysDigestChannelCard digestKey in accountKeyCards) {
									await this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateAccountKeysFromDigest(digestKey).ConfigureAwait(false);
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
								await provider.UpdateWalletSnapshotFromDigest(accountCard, lc).ConfigureAwait(false);
							}

							Task<bool> task1 = this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(accountId);

							if(task1.WaitAndUnwrapException(token)) {
								// ok, we update this account

								//TODO: perform this
								if(accountCard == null) {
									accountCard = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestJointAccount(accountSequenceId);
								}

								await this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateSnapshotDigestFromDigest(accountCard).ConfigureAwait(false);
							}
						}

						// now the accreditation certificates
						List<IAccreditationCertificateDigestChannelCard> certificates = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestAccreditationCertificateCards();

						foreach(IAccreditationCertificateDigestChannelCard certificate in certificates) {
							token.ThrowIfCancellationRequested();

							await this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.UpdateAccreditationCertificateFromDigest(certificate).ConfigureAwait(false);
						}

					}, lockContext).ConfigureAwait(false);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Fatal(ex, "Failed to update the blockchain snapshots from the digest.");

					throw;
				}
			}
		}

		/// <summary>
		///     Ensure a transaction is added to our chain event pool
		/// </summary>
		/// <param name="signedTransactionEnvelope"></param>
		/// <param name="lockContext"></param>
		protected async Task AddTransactionToEventPool(ITransactionEnvelope signedTransactionEnvelope, LockContext lockContext) {
			if(this.ChainEventPoolProvider.EventPoolEnabled) {
				this.CentralCoordinator.Log.Verbose($"inserting transaction {signedTransactionEnvelope.Contents.Uuid} into the chain pool. " + (this.ChainEventPoolProvider.SaveTransactionEnvelopes ? "The whole body will be saved" : "Only metadata will be saved"));

				// ok, we are saving the transactions to the transaction pool. first lets save the metadata to the pool
				await this.ChainEventPoolProvider.InsertTransaction(signedTransactionEnvelope, lockContext).ConfigureAwait(false);
			}
		}

		protected virtual async Task ClearTransactionPoolBlockTransactions(IBlock block, LockContext lockContext) {
			// get all transactions
			List<TransactionId> transactions = block.GetAllTransactions();

			await this.ChainEventPoolProvider.DeleteTransactions(transactions).ConfigureAwait(false);
		}

		public void PostChainEvent(SystemMessageTask messageTask, CorrelationContext correlationContext = default) {
			this.CentralCoordinator.PostSystemEvent(messageTask, correlationContext);
		}

		public void PostChainEvent(BlockchainSystemEventType message, CorrelationContext correlationContext = default) {
			this.CentralCoordinator.PostSystemEvent(message, correlationContext);
		}

	#region rpc methods

		public long GetBlockHeight() {
			return this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
		}

		public int GetDigestHeight() {
			return this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;
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

		
	}
}