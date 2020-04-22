using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
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
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IBlockchainManager : IManagerBase {

		Task<bool> BlockchainSyncing(LockContext lockContext);
		Task<bool> BlockchainSynced(LockContext lockContext);

		Task<bool?> WalletSyncedNoWait(LockContext lockContext);
		Task<bool>  WalletSynced(LockContext lockContext);
		Task<bool>  WalletSyncing(LockContext lockContext);

		Task SynchronizeBlockchain(bool force, LockContext lockContext);
		Task SynchronizeWallet(bool force, LockContext lockContext, bool? allowGrowth = null);
		Task SynchronizeWallet(IBlock block, bool force, LockContext lockContext, bool? allowGrowth = null);
		Task SynchronizeWallet(List<IBlock> blocks, bool force, LockContext lockContext, bool? allowGrowth = null);
		Task SynchronizeWallet(List<IBlock> blocks, bool force, bool mobileForce, LockContext lockContext, bool? allowGrowth = null);
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

		private RecursiveAsyncLock asyncLocker         = new RecursiveAsyncLock();
		private int                lastConnectionCount = 0;

		protected bool NetworkPaused => this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused;

		// the sync workflow we keep as a reference.
		private IClientChainSyncWorkflow chainSynchWorkflow;
		private DateTime?                nextBlockchainSynchCheck;
		private DateTime?                nextExpiredTransactionCheck;
		private DateTime?                nextWalletSynchCheck;
		private ISyncWalletWorkflow      synchWalletWorkflow;

		public BlockchainManager(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, 1) {
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;

		}

		protected new CENTRAL_COORDINATOR CentralCoordinator => base.CentralCoordinator;

		/// <summary>
		///     every once in a while, we check for the sync status
		/// </summary>
		protected override async Task ProcessLoop(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			await base.ProcessLoop(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);

			if(this.ShouldAct(ref this.nextBlockchainSynchCheck)) {

				await this.CheckBlockchainSynchronizationStatus(lockContext).ConfigureAwait(false);
			}

			if(this.ShouldAct(ref this.nextWalletSynchCheck)) {

				await this.CheckWalletSynchronizationStatus(lockContext).ConfigureAwait(false);
			}

			if(this.ShouldAct(ref this.nextExpiredTransactionCheck)) {

				await CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider.DeleteExpiredTransactions().ConfigureAwait(false);

				this.nextExpiredTransactionCheck = DateTime.UtcNow.AddMinutes(30);
			}
		}

		protected override async Task Initialize(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			await base.Initialize(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);

			this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PeerConnectionsCountUpdated += this.ChainNetworkingProviderBaseOnPeerConnectionsCountUpdated;

			// make sure we check our status when starting
			await this.CheckBlockchainSynchronizationStatus(lockContext).ConfigureAwait(false);

			await this.CheckWalletSynchronizationStatus(lockContext).ConfigureAwait(false);

			// connect to the wallet events
			this.SetPassphraseHandlers(lockContext);

			await this.LoadWalletIfRequired(lockContext).ConfigureAwait(false);

			await this.RoutedTaskRoutingReceiver.CheckTasks().ConfigureAwait(false);
		}

		protected virtual async Task LoadWalletIfRequired(LockContext lockContext) {

			var configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(configuration.LoadWalletOnStart && !this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {

				if(configuration.CreateMissingWallet && !await CentralCoordinator.ChainComponentProvider.WalletProviderBase.WalletFileExists(lockContext).ConfigureAwait(false)) {
					//TODO: passphrases? this here is mostly for debug
					// if we must, we will create a new wallet
					
						Dictionary<int, string> passphrases = new Dictionary<int, string>();
						passphrases.Add(0, "toto");

						if(!await CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(default, configuration.EncryptWallet, configuration.EncryptWalletKeys, false, passphrases.ToImmutableDictionary(), lockContext).ConfigureAwait(false)) {
							throw new ApplicationException("Failed to create a new wallet");
						}
					
				}

				if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
					try {

						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadWallet(new CorrelationContext(), lockContext).ConfigureAwait(false);
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletLoaded(lockContext).ConfigureAwait(false);

					} catch(WalletNotLoadedException ex) {
						Log.Warning("Failed to load wallet. Not loaded.");
					} catch(Exception ex) {
						Log.Warning("Failed to load wallet. Not loaded.", ex);
					}
				}
			}
		}

		protected virtual void SetPassphraseHandlers(LockContext lockContext) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().PassphraseCaptureMethod == AppSettingsBase.PassphraseQueryMethod.Event) {
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetExternalPassphraseHandlers(this.WalletProviderOnWalletPassphraseRequest, this.WalletProviderOnWalletKeyPassphraseRequest, this.WalletProviderOnWalletCopyKeyFileRequest, this.CopyWalletRequest, lockContext).WaitAndUnwrapException();
			}
		}

		protected virtual async Task ChainNetworkingProviderBaseOnPeerConnectionsCountUpdated(int count, LockContext lockContext) {

			int minimumSyncPeerCount = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MinimumSyncPeerCount;

			if(this.lastConnectionCount < minimumSyncPeerCount && count >= minimumSyncPeerCount) {
				// we just got enough peers to potentially first peer, let's sync
				await this.SynchronizeBlockchain(true, lockContext).ConfigureAwait(false);
			}

			this.lastConnectionCount = count;
		}

	#region blockchain sync

		/// <summary>
		///     store if we have synced at least once since we launched the server.
		/// </summary>
		private bool hasBlockchainSyncedOnce;

		/// <summary>
		///     this method determine's if it is time to run a synchronization on our blockchain
		/// </summary>
		protected virtual async Task CheckBlockchainSynchronizationStatus(LockContext lockContext) {

			if(this.CentralCoordinator.IsShuttingDown) {
				return;
			}

			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2PEnabled) {

				await this.SynchronizeBlockchain(false, lockContext).ConfigureAwait(false);

				// lets check again in X seconds
				if(this.hasBlockchainSyncedOnce) {
					// ok, now we can wait the regular intervals
					this.nextBlockchainSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextBlockchainSynchCheck = DateTime.UtcNow.AddSeconds(2);
				}
			} else {
				this.nextBlockchainSynchCheck = DateTime.MaxValue;
			}
		}

		/// <summary>
		///     Perform a blockchain sync
		/// </summary>
		public async Task SynchronizeBlockchain(bool force, LockContext lockContext) {
			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2PEnabled) {

				if(force) {
					// let's for ce a sync by setting the chain as desynced
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTime.MinValue;
				}

				if((!NetworkPaused && !await BlockchainSyncing(lockContext).ConfigureAwait(false) && !await BlockchainSynced(lockContext).ConfigureAwait(false) && this.CheckNetworkSyncable())) {

					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// if we are not synchronized, we go ahead and do it.
					if(!this.hasBlockchainSyncedOnce || chainStateProvider.IsChainDesynced) {
						// that's it, we launch a chain sync
						using(var handle = await this.asyncLocker.LockAsync().ConfigureAwait(false)) {

							if(this.chainSynchWorkflow != null && this.chainSynchWorkflow.IsCompleted) {
								var task = Task.Run(() => this.chainSynchWorkflow?.Dispose());
								this.chainSynchWorkflow = null;
							}

							if(this.chainSynchWorkflow == null) {

								// ok, we did at least once
								this.hasBlockchainSyncedOnce = true;

								this.chainSynchWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateChainSynchWorkflow(this.CentralCoordinator.FileSystem);

								// when its done, we can clear it here. not necessary, but keeps things cleaner.
								this.chainSynchWorkflow.Completed += async (success, workflow) => {
									LockContext innerLockContext = null;

									using(var handle = await this.asyncLocker.LockAsync(innerLockContext).ConfigureAwait(false)) {

										if(success && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
											this.nextBlockchainSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
										} else {
											// we never synced, we need to check more often to be ready to do so
											this.nextBlockchainSynchCheck = DateTime.UtcNow.AddSeconds(5);
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
		/// Return the state of the network and if it is syncable for us.
		/// </summary>
		/// <returns></returns>
		protected virtual bool CheckNetworkSyncable() {
			var networkingProvider   = this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase;
			int minimumSyncPeerCount = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MinimumSyncPeerCount;

			return networkingProvider.HasPeerConnections && networkingProvider.CurrentPeerCount >= minimumSyncPeerCount;
		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public async Task<bool> BlockchainSyncing(LockContext lockContext) {

			using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return this.chainSynchWorkflow != null && !this.chainSynchWorkflow.IsCompleted;
			}

		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?
		/// </summary>
		public async Task<bool> BlockchainSynced(LockContext lockContext) {

			using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return (!await this.BlockchainSyncing(handle).ConfigureAwait(false) || (this.chainSynchWorkflow?.IsCompleted ?? true)) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced;

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
		protected virtual async Task CheckWalletSynchronizationStatus(LockContext lockContext) {

			if(this.CentralCoordinator.IsShuttingDown) {
				return;
			}

			if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {

				await this.SynchronizeWallet(false, lockContext, true).ConfigureAwait(false);

				// lets check again in X seconds
				if(this.hasWalletSyncedOnce) {
					// ok, now we can wait the regular intervals
					this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(2);
				}
			} else {
				this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(5);
			}
		}

		public virtual Task SynchronizeWallet(bool force, LockContext lockContext, bool? allowGrowth = null) {
			return this.SynchronizeWallet((List<IBlock>) null, force, lockContext, allowGrowth);
		}

		/// <summary>
		///     a new block has been received, lets sync our wallet
		/// </summary>
		/// <param name="block"></param>
		public virtual Task SynchronizeWallet(IBlock block, bool force, LockContext lockContext, bool? allowGrowth = null) {

			return this.SynchronizeWallet(new[] {block}.ToList(), force, lockContext, allowGrowth);
		}

		public virtual Task SynchronizeWallet(List<IBlock> blocks, bool force, LockContext lockContext, bool? allowGrowth = null) {
			return this.SynchronizeWallet(blocks, force, false, lockContext, allowGrowth);
		}

		public virtual async Task SynchronizeWallet(List<IBlock> blocks, bool force, bool mobileForce, LockContext lockContext, bool? allowGrowth = null) {

			var walletSynced = await WalletSyncedNoWait(lockContext).ConfigureAwait(false);

			if(!walletSynced.HasValue) {
				// we could not verify, try again later
				this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(1);

				return;
			}

			if(!this.NetworkPaused && this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded && (mobileForce && GlobalSettings.ApplicationSettings.SynclessMode || !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableWalletSync && force || !walletSynced.Value)) {
				using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
					if(this.synchWalletWorkflow != null && this.synchWalletWorkflow.IsCompleted) {
						var task = Task.Run(() => this.synchWalletWorkflow?.Dispose());
						this.synchWalletWorkflow = null;
					}

					if(this.synchWalletWorkflow == null) {

						this.hasWalletSyncedOnce = true;
						this.synchWalletWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateSyncWalletWorkflow();

						this.synchWalletWorkflow.AllowGrowth = allowGrowth;

						if(blocks?.Any() ?? false) {
							foreach(IBlock block in blocks.Where(b => b != null).OrderBy(b => b.BlockId.Value)) {
								this.synchWalletWorkflow.LoadedBlocks.AddSafe(block.BlockId, block);
							}
						}

						// when its done, we can clear it here. not necessary, but keeps things cleaner.
						this.synchWalletWorkflow.Completed += async (success, workflow) => {

							// ok, now we can wait the regular intervals
							LockContext lockContext2   = null;
							var         walletIsSynced = await CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced(lockContext2).ConfigureAwait(false);

							if(success && walletIsSynced.HasValue && walletIsSynced.Value && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
								this.nextWalletSynchCheck = this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
							} else {
								this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(5);
							}

							this.synchWalletWorkflow = null;

						};

						this.CentralCoordinator.PostWorkflow(this.synchWalletWorkflow);
					}
				}
			}

		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public async Task<bool> WalletSyncing(LockContext lockContext) {

			using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return this.synchWalletWorkflow != null && !this.synchWalletWorkflow.IsCompleted;
			}

		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public async Task<bool?> WalletSyncedNoWait(LockContext lockContext) {

			using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				var walletSynced = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait(handle).ConfigureAwait(false);

				if(!walletSynced.HasValue) {
					return null;
				}

				return (!await WalletSyncing(handle).ConfigureAwait(false) || (this.synchWalletWorkflow?.IsCompleted ?? true)) && walletSynced.Value;

			}
		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public async Task<bool> WalletSynced(LockContext lockContext) {

			using(var handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				var walletIsSynced = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced(handle).ConfigureAwait(false);

				if(!walletIsSynced.HasValue) {
					return false;
				}

				return (!await WalletSyncing(handle).ConfigureAwait(false) || (this.synchWalletWorkflow?.IsCompleted ?? true)) && walletIsSynced.Value;
			}

		}

	#endregion

	#region wallet manager

		private Task CopyWalletRequest(CorrelationContext correlationContext, int attempt, LockContext lockContext) {
			Log.Information("Requesting loading wallet.");

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				RequestCopyWalletSystemMessageTask requestCopyWalletTask = new RequestCopyWalletSystemMessageTask(() => {
					resetEvent.Set();
				});

				this.PostChainEvent(requestCopyWalletTask);

				// wait up to 5 minutes for the wallet to be ready to load
				resetEvent.Wait(TimeSpan.FromMinutes(5));
			}
			
			return Task.CompletedTask;
		}

		private Task<(SecureString passphrase, bool keysToo)> WalletProviderOnWalletPassphraseRequest(CorrelationContext correlationContext, int attempt, LockContext lockContext) {
			Log.Information("Requesting wallet passphrase.");

			using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			if(correlationContext.IsNew) {
				correlationContext.InitializeNew();
			}

			RequestWalletPassphraseSystemMessageTask loadWalletPassphraseTask = new RequestWalletPassphraseSystemMessageTask(attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletPassphraseTask, correlationContext);

			// wait until we get the passphrase back
			resetEvent.Wait();

			return Task.FromResult((loadWalletPassphraseTask.Passphrase, loadWalletPassphraseTask.KeysToo));

		}

		private Task<SecureString> WalletProviderOnWalletKeyPassphraseRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt, LockContext lockContext) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");

			using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			RequestWalletKeyPassphraseSystemMessageTask loadWalletKeyPasshraseTask = new RequestWalletKeyPassphraseSystemMessageTask(accountuuid, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletKeyPasshraseTask);

			// wait up to 5 hours for the wallet to be ready to load
			resetEvent.Wait(TimeSpan.FromHours(5));

			return Task.FromResult(loadWalletKeyPasshraseTask.Passphrase);

		}

		private Task WalletProviderOnWalletCopyKeyFileRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt, LockContext lockContext) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");

			using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			RequestCopyWalletKeyFileSystemMessageTask loadCopyWalletKeyFileTask = new RequestCopyWalletKeyFileSystemMessageTask(accountuuid, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadCopyWalletKeyFileTask);

			// wait up to 5 hours for the wallet to be ready to load
			resetEvent.Wait(TimeSpan.FromHours(5));

			return System.Threading.Tasks.Task.CompletedTask;
		}

		public Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {

			return this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token, lc) => {
				return provider.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases, lc);
			}, lockContext);

		}

	#endregion

	}
}