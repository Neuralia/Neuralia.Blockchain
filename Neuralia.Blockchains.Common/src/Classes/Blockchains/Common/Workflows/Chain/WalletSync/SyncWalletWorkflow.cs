using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync {
	public interface ISyncWalletWorkflow : IChainWorkflow {
		ConcurrentDictionary<BlockId, IBlock> LoadedBlocks { get; }

		bool? AllowGrowth { get; set; }
	}

	public interface ISyncWalletWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISyncWalletWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This workflow will ensure that the wallet is in sync with the chain.
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public abstract class SyncWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISyncWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		// the relative cost of operations to ensure a uniform time cost per transaction
		private const int SINGLE_TRANSACTION_UNIT_COUNT = 1000;
		private const int SINGLE_EXTENDED_TRANSACTION_UNIT_COUNT = SINGLE_TRANSACTION_UNIT_COUNT * 3;
		private const int FULL_BLOCK_UNIT_COUNT = 100;
		private const int INTERPOLATED_MAJOR_UNIT_COUNT = 5;
		private const int INTERPOLATED_MINOR_UNIT_COUNT = 1;
		
		private readonly RateCalculator rateCalculator = new RateCalculator();
		private bool shutdownRequest;

		public SyncWalletWorkflow(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {
			// allow only one at a time
			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;
		}

		private bool IsBusy { get; set; }

		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.LongRunning;

		public bool? AllowGrowth { get; set; }

		/// <summary>
		///     The latest block that may have been received
		/// </summary>
		public ConcurrentDictionary<BlockId, IBlock> LoadedBlocks { get; } = new ConcurrentDictionary<BlockId, IBlock>();

		protected virtual bool CheckShouldStop() {

			return this.shutdownRequest || this.CheckCancelRequested();
		}

		protected void CheckShouldStopThrow() {
			if(this.CheckShouldStop()) {
				this.CancelTokenSource.Cancel();
				this.CancelToken.ThrowIfCancellationRequested();
			}
		}

		private readonly object uniqueLocker = new object();
		private bool working = false;
		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			try {
				// just make sure it is always a unique instance. just in case
				lock(this.uniqueLocker) {
					if(this.working) {
						return;
					}

					this.working = true;
				}
				await this.PerformSyncWork(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
			} 
			catch(OutOfMemoryException oex) {
				// thats bad, lets clear everything

				this.LoadedBlocks.Clear();
				GC.Collect();
					
				throw;
			}
			finally {
				lock(this.uniqueLocker) {
					this.working = false;
				}
			}
			
		}
		protected async Task PerformSyncWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.NodeShareType().DoesNotShare && !GlobalSettings.ApplicationSettings.SynclessMode) {

				await this.TriggerWalletSynced(lockContext).ConfigureAwait(false);

				return;
			}

			if(!this.centralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded || ! await centralCoordinator.ChainComponentProvider.WalletProviderBase.HasAccount(lockContext).ConfigureAwait(false)) {
				return;
			}

			long? lowestAccountBlockSyncHeight = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);

			if(lowestAccountBlockSyncHeight == null) {
				return;
			}

			long startBlockHeight = lowestAccountBlockSyncHeight.Value;

			long currentBlockHeight = startBlockHeight;
			
			long? previousSyncedBlockHeightEntry = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountPreviousBlockSyncHeight(lockContext).ConfigureAwait(false);

			long previousSyncedBlockHeight = 0;
			if(!previousSyncedBlockHeightEntry.HasValue || previousSyncedBlockHeightEntry.Value == 0) {
				previousSyncedBlockHeight = currentBlockHeight - 1;

				if(previousSyncedBlockHeight == -1) {
					// genesis will be at 0
					previousSyncedBlockHeight = 0;
				}
			} else {
				previousSyncedBlockHeight = previousSyncedBlockHeightEntry.Value;
			}
			List<IWalletAccount> syncableAccounts = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(null, currentBlockHeight, lockContext).ConfigureAwait(false);

			if(!syncableAccounts.Any()) {
				// no syncing possible
				await this.TriggerWalletSynced(lockContext).ConfigureAwait(false);

				return;
			}

			try {

				this.centralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;

				this.IsBusy = true;

				this.CentralCoordinator.Log.Verbose("Wallet sync started");

				long targetBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					//In mobile, we don't want to sync every blocks, we target what is in the cache
					BlockId highestCachedSynthesizedBlockId = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetHighestCachedSynthesizedBlockId(lockContext);

					if(highestCachedSynthesizedBlockId == null) {
						return;
					}

					targetBlockHeight = highestCachedSynthesizedBlockId;
				}

				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncStarted(currentBlockHeight, targetBlockHeight)).ConfigureAwait(false);

				// now lets run then sequence
				await this.RunWalletUpdateSequence(currentBlockHeight, previousSyncedBlockHeight, targetBlockHeight, taskRoutingContext, lockContext).ConfigureAwait(false);

				lowestAccountBlockSyncHeight = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);

				if(lowestAccountBlockSyncHeight.HasValue) {
					currentBlockHeight = lowestAccountBlockSyncHeight.Value;
				}

				if(currentBlockHeight > this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
					currentBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
				}

				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CleanSynthesizedBlockCache(lockContext).ConfigureAwait(false);

				this.CentralCoordinator.Log.Verbose("Wallet sync completed");
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Wallet sync failed");

				//In mobile, no need to resync every blocks when it fails. We only need to resume at the last block that worked.
				if(GlobalSettings.ApplicationSettings.SynclessMode && ex is WalletSyncException walletSyncException) {
					currentBlockHeight = walletSyncException.BlockId - 1;
				}

				throw;
			} finally {
				this.IsBusy = false;

				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncEnded(currentBlockHeight, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight)).ConfigureAwait(false);

				this.centralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;

				await this.TriggerWalletSynced(lockContext).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Prepare sync plans and run batch updates on the wallet
		/// </summary>
		/// <param name="currentBlockHeight"></param>
		/// <param name="targetBlockHeight"></param>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		private async Task RunWalletUpdateSequence(long currentBlockHeight, long previousSyncedBlockHeight, long targetBlockHeight, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			Func<long, LockContext, bool> isBlockAvailableCallback = null;

			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				isBlockAvailableCallback = (blockId, lc) => this.centralCoordinator.ChainComponentProvider.WalletProviderBase.IsSynthesizedBlockCached(blockId, lc);
			} else {
				isBlockAvailableCallback = (blockId, lc) => true;
			}

			int totalIncrement = 0;

			// ensure that captured closures dont lock variables
			ClosureState closureState = new ClosureState();

			// lets make sure that the currentBlockHeight was fully inserted otherwise we have to retry it to complete the workflow
			// The latestSyncedBlockId is the same as the currentBlockHeight, so the same value is used for both parameters.
			if(!await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsHaveSyncStatus(currentBlockHeight, currentBlockHeight, WalletAccountChainState.BlockSyncStatuses.FullySynced, lockContext).ConfigureAwait(false)) {
				// let's run a resync of the latest just in case
				currentBlockHeight = previousSyncedBlockHeight;
			}

			// Get initialized outside the loop once and must stay outside because the sync is different for mobile. It gets updated at every full block sync (which is not every block for mobile, but it is for desktop).
			closureState.lastFullBlockSynced = currentBlockHeight;

			while (currentBlockHeight < targetBlockHeight) {

				closureState.nextPlanBlockHeight = currentBlockHeight + totalIncrement;
				closureState.targetBlockHeight = targetBlockHeight;

				Task<(List<(long planBlockId, BlockModes mode)> currentPlan, Dictionary<long, SynthesizedBlock> loadedSynthesizedBlocks, int increment)> prefetchBlocksTask = null;

				if(closureState.nextPlanBlockHeight < targetBlockHeight) {

					prefetchBlocksTask = Task.Run(async () => {

						(List<(long planBlockId, BlockModes mode)> nextPlan, int increment) = this.PrepareTransactionPlan(closureState.nextPlanBlockHeight, closureState.targetBlockHeight, isBlockAvailableCallback, lockContext);

						Dictionary<long, SynthesizedBlock> loadedBlocks = new Dictionary<long, SynthesizedBlock>();

						// TODO: Load blocks in batches instead of one at a time.
						foreach((long planBlockId, BlockModes mode) in nextPlan.OrderBy(e => e.planBlockId)) {
							if(mode == BlockModes.FullBlock) {
								// load block
								loadedBlocks.Add(planBlockId, await this.GetSynthesizedBlock(planBlockId, lockContext).ConfigureAwait(false));
							}
						}

						return (nextPlan, loadedBlocks, increment);
					});
				}

				if(closureState.currentPlan != null) {

					// here we execute the plan and run the insertion transaction
					await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {
					
						// run transaction and insert blocks

						foreach((long planBlockId, BlockModes mode) in closureState.currentPlan.OrderBy(e => e.planBlockId)) {
							(Func<BlockId, BlockId, Task> Action, BlockId blockId, BlockId target)? syncEvent = null;
							try {
								if(mode == BlockModes.FullBlock) {
									
									await this.SynchronizeBlock(provider, closureState.loadedSynthesizedBlocks[planBlockId], planBlockId, closureState.lastFullBlockSynced, closureState.targetBlockHeight, taskRoutingContext, lc).ConfigureAwait(false);
									this.rateCalculator.AddHistoryEntry(planBlockId);
									//TODO: since the new refactor, the sync rate calculations should change. as it is, it wont be constant anymore.
									closureState.syncRate = this.rateCalculator.CalculateSyncingRate(closureState.targetBlockHeight - planBlockId);
									
									//For desktop, the lastFullBlockSynced will always be the previous one. For mobile, it's not always the case.
									closureState.lastFullBlockSynced = planBlockId;
					
									syncEvent = ((b, h) => {
											            // alert that we are syncing a block
											            return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncStepEvent(b, h, closureState.syncRate));
										            }, planBlockId, closureState.targetBlockHeight);
								} else if(mode == BlockModes.InterpolatedMajor) {
									syncEvent = ((b, h) => {
											            return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncStepEvent(b, h, ""));
										            }, planBlockId, closureState.targetBlockHeight);
					
								} else if(mode == BlockModes.InterpolatedMinor) {
									//TODO: do something?
								}
					
								if(syncEvent.HasValue) {
									(Func<BlockId, BlockId, Task> Action, BlockId blockId, BlockId target) action = syncEvent.Value;
									await action.Action(action.blockId, action.target).ConfigureAwait(false);
								}
					
							} catch(Exception ex) {
								throw new WalletSyncException(planBlockId, $"Failed to sync block Id {planBlockId} during wallet sync.", ex);
							}
						}
					
					}, lockContext).ConfigureAwait(false);

					currentBlockHeight += totalIncrement;

					closureState.currentPlan = null;
					closureState.loadedSynthesizedBlocks = null;
				}

				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CleanSynthesizedBlockCache(lockContext).ConfigureAwait(false);

				// reset our total increment no matter what (sometimes the next plan fetch doesnt move, so this must be reset every time)
				totalIncrement = 0;
				
				if(prefetchBlocksTask != null) {
					(closureState.currentPlan, closureState.loadedSynthesizedBlocks, totalIncrement) = await prefetchBlocksTask.ConfigureAwait(false);
				}

				// expand the target?
				if((this.AllowGrowth.HasValue && this.AllowGrowth.Value) || this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.AllowWalletSyncDynamicGrowth) {
					//this seems to cause issues. better to not grow and run it explicitely later
					targetBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
				}
			}
		}

		private (List<(long planBlockId, BlockModes mode)> currentPlan, int increment) PrepareTransactionPlan(long currentBlockHeight, long targetBlockHeight, Func<long, LockContext, bool> isBlockAvailableCallback, LockContext lockContext) {

			List<(long planBlockId, BlockModes mode)> plan = new List<(long planBlockId, BlockModes mode)>();
			int transactionUnits = SINGLE_TRANSACTION_UNIT_COUNT;

			// if the chain is synced, then we have more resources to take bigger bites at the sync task
			if(this.centralCoordinator.IsChainSynchronized) {
				transactionUnits = SINGLE_EXTENDED_TRANSACTION_UNIT_COUNT;
			}

			int delta = (int) (targetBlockHeight - currentBlockHeight);

			int increment = 0;

			foreach(int index in Enumerable.Range(1, delta)) {

				long synthesizedBlockId = currentBlockHeight + index;

				BlockModes mode = BlockModes.Unknown;

				if(isBlockAvailableCallback(synthesizedBlockId, lockContext)) {

					transactionUnits -= FULL_BLOCK_UNIT_COUNT;
					mode = BlockModes.FullBlock;
				} else if(((delta - index) <= 10) || (((delta - index) <= 50) && ((index % 3) == 0)) || ((index % 10) == 0)) {
					//interpolation. We use a modulo to limit interpolation events above 10 blocks remaining as to not overwhelm the system

					transactionUnits -= INTERPOLATED_MAJOR_UNIT_COUNT;
					mode = BlockModes.InterpolatedMajor;
				} else {
					//interpolation

					transactionUnits -= INTERPOLATED_MINOR_UNIT_COUNT;
					mode = BlockModes.InterpolatedMinor;
				}

				plan.Add((synthesizedBlockId, mode));

				// we passed a new block
				++increment;

				if(transactionUnits <= 0) {
					// we have fill all the space available
					break;
				}
			}

			return (plan, increment);
		}

		private async Task TriggerWalletSynced(LockContext lockContext) {
			try {
				bool? synced = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait(lockContext).ConfigureAwait(false);

				if(synced.HasValue && synced.Value) {
					await this.centralCoordinator.TriggerWalletSyncedEvent(lockContext).ConfigureAwait(false);
				}
			} catch {

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

		private async Task SynchronizeBlock(IWalletProvider provider, SynthesizedBlock synthesizedBlock, long currentHeight, long lastSyncedBlockId, long targetHeight, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			this.CentralCoordinator.Log.Information($"Performing Wallet sync for block {synthesizedBlock.BlockId} out of {Math.Max(currentHeight, synthesizedBlock.BlockId)}");

			// run the workflow sequence!
			this.CheckShouldStopThrow();

			await provider.UpdateWalletBlock(synthesizedBlock, lastSyncedBlockId, async (sb, lc) => {

				this.CheckShouldStopThrow();
				this.CentralCoordinator.Log.Verbose($"ProcessBlockImmediateAccountsImpact for block {synthesizedBlock.BlockId}...");

				// run the interpretation if any account is tracked
				await this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateAccountsImpact(synthesizedBlock, lastSyncedBlockId, lockContext).ConfigureAwait(false);

				this.CheckShouldStopThrow();
				this.CentralCoordinator.Log.Verbose($"InterpretNewBlockLocalWallet for block {synthesizedBlock.BlockId}...");

				// run the interpretation if any account is tracked
				await this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.InterpretNewBlockLocalWallet(synthesizedBlock, lastSyncedBlockId, taskRoutingContext, lockContext).ConfigureAwait(false);

			}, lockContext).ConfigureAwait(false);

		}

		private async Task<SynthesizedBlock> GetSynthesizedBlock(long blockId, LockContext lockContext) {
			SynthesizedBlock synthesizedBlock = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ExtractCachedSynthesizedBlock(blockId);

			if(synthesizedBlock != null) {
				return synthesizedBlock;
			}

			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				// In mobile mode, every synthesized block should always be cached, or else it means the wallet is missing transactions from the Light Server.
				throw new WalletSyncException(blockId, $"Failed to sync block Id {blockId} during wallet sync.");
			}

			IBlock block = null;

			if(this.LoadedBlocks.ContainsKey(blockId)) {
				// lets try to use our loaded blocks 
				block = this.LoadedBlocks[blockId];
			}

			if(block == null) {
				block = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(blockId);
			}

			if(block != null) {
				synthesizedBlock = await this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.SynthesizeBlock(block, lockContext).ConfigureAwait(false);
			}

			return synthesizedBlock;
		}

		private List<SynthesizedBlock> GetFutureSynthesizedBlocks(long blockId, LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetCachedSynthesizedBlocks(blockId, lockContext);
		}

		private class ClosureState {
			public List<(long planBlockId, BlockModes mode)> currentPlan;
			public long lastFullBlockSynced;
			public Dictionary<long, SynthesizedBlock> loadedSynthesizedBlocks;
			public long nextPlanBlockHeight;
			public long targetBlockHeight;
			public string syncRate;
		}

		private enum BlockModes {
			Unknown,
			FullBlock,
			InterpolatedMajor,
			InterpolatedMinor
		}
	}
}