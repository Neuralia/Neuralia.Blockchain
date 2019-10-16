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
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync {
	public interface ISyncWalletWorkflow : IChainWorkflow {
		Dictionary<BlockId, IBlock> LoadedBlocks { get; }

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

		/// <summary>
		///     The latest block that may have been received
		/// </summary>
		public Dictionary<BlockId, IBlock> LoadedBlocks { get; } = new Dictionary<BlockId, IBlock>();

		protected virtual bool CheckShouldStop() {

			return this.shutdownRequest || this.CheckCancelRequested();
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

			if(!BlockchainUtilities.UsesBlocks(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockSavingMode) && !GlobalSettings.ApplicationSettings.MobileMode) {
				return;
			}

			if(!this.centralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
				return;
			}

			var walletAccounts = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts();

			long startBLockHeight = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight;

			long currentBlockHeight = startBLockHeight;

			var syncableAccounts = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(currentBlockHeight);

			if(!syncableAccounts.Any()) {
				// no syncing possible
				return;
			}

			try {

				this.centralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;

				this.IsBusy = true;
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.WalletSyncStarted);
				Log.Verbose("Wallet sync started");

				bool sequentialSync = true;

				if(syncableAccounts.All(a => (WalletAccountChainState.BlockSyncStatuses) this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(a.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus == WalletAccountChainState.BlockSyncStatuses.FullySynced) || (startBLockHeight == 0)) {
					// all accounts are fully synced. we can move on to the next block
					if(GlobalSettings.ApplicationSettings.MobileMode) {
						// in mobile, we see we are up to date, no need to do the sequential
						sequentialSync = false;
					} else {
						// we are fully synced, we move up
						currentBlockHeight += 1;
					}
				}

				long nextBlockHeight = currentBlockHeight + 1;

				SynthesizedBlock currentSynthesizedBlock = null;
				SynthesizedBlock nextSynthesizedBlock = null;

				long currentHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

				if(GlobalSettings.ApplicationSettings.MobileMode) {
					// in mobile mode, we dont want to sync every block, we only sync the ones we have. lets start with the current one only
					currentHeight = currentBlockHeight;
				}

				if(sequentialSync && (currentBlockHeight <= currentHeight)) {
					nextSynthesizedBlock = this.GetSynthesizedBlock(currentBlockHeight);
				}

				// now we get the lowest blockheight account; our point to start

				// load the blocks one at a time
				while(sequentialSync && (currentBlockHeight <= currentHeight)) {

					if(this.CheckShouldStop()) {
						break;
					}

					currentSynthesizedBlock = nextSynthesizedBlock;

					Task nextBlockTask = null;

					// get the next synthesized block in parallel
					if(nextBlockHeight <= currentHeight) {
						long height = nextBlockHeight;

						nextBlockTask = new Task(() => {
							// since there can be a transaction below, lets make sure to whitelist ourselves. we dont need anything extravagant anyways. no real risk of collision
							this.centralCoordinator.ChainComponentProvider.WalletProviderBase.RequestFriendlyAccess(() => {
								nextSynthesizedBlock = this.GetSynthesizedBlock(height);
							});
						});

						nextBlockTask.Start();
					}

					// update this block now. everything happens in the wallet service

					this.SynchronizeBlock(currentSynthesizedBlock, currentBlockHeight, currentBlockHeight - 1, taskRoutingContext);

					// make sure it is completed before we move forward
					nextBlockTask?.Wait();

					currentBlockHeight += 1;
					nextBlockHeight += 1;

					if((this.AllowGrowth.HasValue && this.AllowGrowth.Value) || this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.AllowWalletSyncDynamicGrowth) {
						//this seems to cause issues. better to not grow and run it explicitely later
						currentHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
					}
				}

				if(GlobalSettings.ApplicationSettings.MobileMode) {

					this.CheckShouldCancel();

					currentBlockHeight = startBLockHeight;

					// after we synced the baseline, in mobile we can increment by blocks
					var synthesizedBlocks = this.GetFutureSynthesizedBlocks(currentBlockHeight + 1);

					foreach(SynthesizedBlock synthesizedBlock in synthesizedBlocks) {

						this.SynchronizeBlock(synthesizedBlock, currentBlockHeight, currentBlockHeight, taskRoutingContext);

						currentBlockHeight = synthesizedBlock.BlockId;
					}
				}

				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CleanSynthesizedBlockCache();

				Log.Verbose("Wallet sync completed");
			} catch(Exception ex) {
				Log.Error(ex, "Wallet sync failed");

				throw;
			} finally {
				this.IsBusy = false;

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.WalletSyncEnded);
				this.centralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;

				try {
					var synced = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

					if(synced.HasValue && synced.Value) {
						this.centralCoordinator.TriggerWalletSyncedEvent();
					}
				} catch {

				}
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

		private void SynchronizeBlock(SynthesizedBlock synthesizedBlock, long currentHeight, long previousBlock, TaskRoutingContext taskRoutingContext) {

			Log.Information($"Performing Wallet sync for block {synthesizedBlock.BlockId} out of {Math.Max(currentHeight, synthesizedBlock.BlockId)}");

			// alert that we are syncing a block
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.WalletSyncStepEvent(synthesizedBlock.BlockId, currentHeight));

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
					Log.Verbose($"updating wallet blocks for block {synthesizedBlock.BlockId}...");

					this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(token => {
						// update the chain height
						this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletBlock(synthesizedBlock, previousBlock);

						token.ThrowIfCancellationRequested();

						allAccountsUpdatedWalletBlock = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AllAccountsUpdatedWalletBlock(synthesizedBlock);
					});
				}

				if(allAccountsUpdatedWalletBlock) {

					(allAccountsWalletKeyLogSet, allImmediateAccountsImpactsPerformed, allInterpretatonsCompleted) = CheckOthersStatus();

					IndependentActionRunner.Run(() => {
						if(this.CheckShouldStop()) {
							return;
						}

						Log.Verbose($"UpdateWalletKeyLogs  for block {synthesizedBlock.BlockId}...");

						this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(token => {
							token.ThrowIfCancellationRequested();

							if(!allAccountsWalletKeyLogSet) {

								// update the key logs
								this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletKeyLogs(synthesizedBlock);
							}
						});
					}, () => {
						if(this.CheckShouldStop()) {
							return;
						}

						Log.Verbose($"ProcessBlockImmediateAccountsImpact for block {synthesizedBlock.BlockId}...");

						if(!allImmediateAccountsImpactsPerformed) {

							// run the interpretation if any account is tracked
							this.centralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateAccountsImpact(synthesizedBlock, taskRoutingContext);
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

			if(GlobalSettings.ApplicationSettings.MobileMode) {
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