using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
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

		public SyncWalletWorkflow(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {
			// allow only one at a time
			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;
		}

		public bool? AllowGrowth { get; set; }
		private bool IsBusy { get; set; } = false;
		private bool shutdownRequest = false;
		
		protected readonly RateCalculator rateCalculator = new RateCalculator();

		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.LongRunning;
		
		/// <summary>
		///     The latest block that may have been received
		/// </summary>
		public ConcurrentDictionary<BlockId, IBlock> LoadedBlocks { get; } = new ConcurrentDictionary<BlockId, IBlock>();

		/// <summary>
		/// this is a hack
		/// TODO: improve this, a static variable is bad
		/// </summary>
		private static DateTime lastClearTimedout = DateTime.MinValue;
		
		protected virtual bool CheckShouldStop() {

			return this.shutdownRequest || this.CheckCancelRequested();
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.NodeShareType().DoesNotShare && !GlobalSettings.ApplicationSettings.SynclessMode) {
				
				this.TriggerWalletSynced();
				return;
			}

			if(!this.centralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
				return;
			}

			var walletAccounts = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts();

			long startBLockHeight = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight.Value;

			long currentBlockHeight = startBLockHeight;

			var syncableAccounts = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(currentBlockHeight);

			if(!syncableAccounts.Any()) {
				// no syncing possible
				this.TriggerWalletSynced();
				
				return;
			}
			
			try {

				this.centralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;

				this.IsBusy = true;
				
				
				Log.Verbose("Wallet sync started");

				bool sequentialSync = true;

				if(syncableAccounts.All(a => (WalletAccountChainState.BlockSyncStatuses) this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(a.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus == WalletAccountChainState.BlockSyncStatuses.FullySynced) || (startBLockHeight == 0)) {
					// all accounts are fully synced. we can move on to the next block
					if(GlobalSettings.ApplicationSettings.SynclessMode) {
						// in mobile, we see we are up to date, no need to do the sequential
						sequentialSync = false;
					} else {
						// we are fully synced, we move up
						currentBlockHeight += 1;
					}
				}

				long nextBlockHeight = currentBlockHeight + 1;

				SynthesizedBlock nextSynthesizedBlock = null;

				long currentHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					// in mobile mode, we dont want to sync every block, we only sync the ones we have. lets start with the current one only
					currentHeight = currentBlockHeight;
				}

				if(sequentialSync && (currentBlockHeight <= currentHeight)) {
					nextSynthesizedBlock = this.GetSynthesizedBlock(currentBlockHeight);
				}

				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncStarted(currentBlockHeight, currentHeight));
				
				// now we get the lowest blockheight account; our point to start

				// load the blocks one at a time
				while(sequentialSync && (currentBlockHeight <= currentHeight) && nextSynthesizedBlock != null) {

					if(this.CheckShouldStop()) {
						break;
					}

					SynthesizedBlock currentSynthesizedBlock = nextSynthesizedBlock;

					Task nextBlockTask = null;

					// get the next synthesized block in parallel
					if(nextBlockHeight <= currentHeight) {
						long height = nextBlockHeight;

						nextBlockTask = Task.Run(() => {
							// since there can be a transaction below, lets make sure to whitelist ourselves. we dont need anything extravagant anyways. no real risk of collision
							nextSynthesizedBlock = this.GetSynthesizedBlock(height);
						});
					}

					// update this block now. everything happens in the wallet service

					this.SynchronizeBlock(currentSynthesizedBlock, currentBlockHeight, currentBlockHeight - 1, currentHeight, taskRoutingContext);

					// make sure it is completed before we move forward
					nextBlockTask?.Wait();

					currentBlockHeight += 1;
					nextBlockHeight += 1;

					if((this.AllowGrowth.HasValue && this.AllowGrowth.Value) || this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.AllowWalletSyncDynamicGrowth) {
						//this seems to cause issues. better to not grow and run it explicitely later
						currentHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
					}
				}

				if(GlobalSettings.ApplicationSettings.SynclessMode) {

					this.CheckShouldCancel();

					currentBlockHeight = startBLockHeight;

					// after we synced the baseline, in mobile we can increment by blocks
					var synthesizedBlocks = this.GetFutureSynthesizedBlocks(currentBlockHeight + 1);

					foreach(SynthesizedBlock synthesizedBlock in synthesizedBlocks) {

						this.SynchronizeBlock(synthesizedBlock, currentBlockHeight, currentBlockHeight, currentBlockHeight, taskRoutingContext);

						currentBlockHeight = synthesizedBlock.BlockId;
					}
				}

				
				if(currentBlockHeight > this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
					currentBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
				}
				
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CleanSynthesizedBlockCache();

				// now we ensure that all timed out in the wallet are updated
				if(lastClearTimedout < DateTime.Now) {
					bool changed = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ResetAllTimedOut();
					// do it again, but not too often. if we did not change anything, perhaps
					//TODO: review this timeout. 
					lastClearTimedout = DateTime.Now.AddMinutes(10);
				}

				Log.Verbose("Wallet sync completed");
			} catch(Exception ex) {
				Log.Error(ex, "Wallet sync failed");

				throw;
			} finally {
				this.IsBusy = false;

				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncEnded(currentBlockHeight, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight));
				
				this.centralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;

				this.TriggerWalletSynced();
			}
		}

		private void TriggerWalletSynced() {
			try {
				var synced = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

				if(synced.HasValue && synced.Value) {
					this.centralCoordinator.TriggerWalletSyncedEvent();
				}
			} catch {

			}
		}
		/// <summary>
		///     Ensure that we dont stop during a sync step if a shutdown has been requested
		/// </summary>
		/// <param name="beacons"></param>
		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {

			this.shutdownRequest = true;

			// ok, if this happens while we are syncing, we ask for a grace period until we are ready to clean exit
			if(this.IsBusy) {
				beacons.Add(Task.Run(() => {

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

		private void SynchronizeBlock(SynthesizedBlock synthesizedBlock, long currentHeight, long previousBlock, long targetHeight, TaskRoutingContext taskRoutingContext) {

			this.rateCalculator.AddHistoryEntry(synthesizedBlock.BlockId);
			
			Log.Information($"Performing Wallet sync for block {synthesizedBlock.BlockId} out of {Math.Max(currentHeight, synthesizedBlock.BlockId)}");

			string syncRate = this.rateCalculator.CalculateSyncingRate(targetHeight-synthesizedBlock.BlockId);
			// alert that we are syncing a block
			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletSyncStepEvent(currentHeight, targetHeight, syncRate));
			

			bool allAccountsUpdatedWalletBlock = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsUpdatedWalletBlock(synthesizedBlock, previousBlock);

			(bool, bool, bool) CheckOthersStatus() {
				bool a = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsWalletKeyLogSet(synthesizedBlock);
				bool b = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsHaveSyncStatus(synthesizedBlock, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed);
				bool c = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsHaveSyncStatus(synthesizedBlock, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted);

				return (a, b, c);
			}

			bool allAccountsWalletKeyLogSet = false;
			bool allImmediateAccountsImpactsPerformed = false;
			bool allInterpretatonsCompleted = false;

			if(allAccountsUpdatedWalletBlock) {
				(allAccountsWalletKeyLogSet, allImmediateAccountsImpactsPerformed, allInterpretatonsCompleted) = CheckOthersStatus();
			}

			if(!allAccountsUpdatedWalletBlock || !allAccountsWalletKeyLogSet || !allImmediateAccountsImpactsPerformed || !allInterpretatonsCompleted) {

				// the update wallet block must be run before anything else is run. hence we run it independently first.
				if(!allAccountsUpdatedWalletBlock) {

					if(this.CheckShouldStop()) {
						return;
					}
					Log.Verbose($"Preparing to update wallet blocks for block {synthesizedBlock.BlockId}...");

					this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {
						// update the chain height
						Log.Verbose($"updating wallet blocks for block {synthesizedBlock.BlockId}...");
						provider.UpdateWalletBlock(synthesizedBlock, previousBlock);

						token.ThrowIfCancellationRequested();

						allAccountsUpdatedWalletBlock = provider.AllAccountsUpdatedWalletBlock(synthesizedBlock);
						Log.Verbose($"updated wallet blocks for block {synthesizedBlock.BlockId}...");
					});
				}

				if(allAccountsUpdatedWalletBlock) {

					(allAccountsWalletKeyLogSet, allImmediateAccountsImpactsPerformed, allInterpretatonsCompleted) = CheckOthersStatus();

					IndependentActionRunner.Run(() => {
						if(this.CheckShouldStop()) {
							return;
						}

						Log.Verbose($"Update Wallet Key Logs for block {synthesizedBlock.BlockId}...");

						if(!allAccountsWalletKeyLogSet) {
							this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {
								token.ThrowIfCancellationRequested();

								// update the key logs
								provider.UpdateWalletKeyLogs(synthesizedBlock);
							});
						}
					}, () => {
						if(this.CheckShouldStop()) {
							return;
						}

						Log.Verbose($"ProcessBlockImmediateAccountsImpact for block {synthesizedBlock.BlockId}...");

						if(!allImmediateAccountsImpactsPerformed) {

							// run the interpretation if any account is tracked
							this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateAccountsImpact(synthesizedBlock);
						}

					}, () => {
						if(this.CheckShouldStop()) {
							return;
						}

						Log.Verbose($"InterpretNewBlockLocalWallet for block {synthesizedBlock.BlockId}...");

						if(!allInterpretatonsCompleted) {
							// run the interpretation if any account is tracked
							this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.InterpretNewBlockLocalWallet(synthesizedBlock, taskRoutingContext);
						}

					});
				}

			}
		}

		private SynthesizedBlock GetSynthesizedBlock(long blockId) {
			SynthesizedBlock synthesizedBlock = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ExtractCachedSynthesizedBlock(blockId);

			if(synthesizedBlock != null) {
				return synthesizedBlock;
			}

			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				// in mobile mode, we will never have blocks. we can represent a block we dont ahve by an empoty synthesized block
				synthesizedBlock = this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.CreateSynthesizedBlock();
				synthesizedBlock.BlockId = blockId;

				return synthesizedBlock;
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
				synthesizedBlock = this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.SynthesizeBlock(block);
			}

			return synthesizedBlock;
		}

		private List<SynthesizedBlock> GetFutureSynthesizedBlocks(long blockId) {
			return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetCachedSynthesizedBlocks(blockId);
		}
	}
}