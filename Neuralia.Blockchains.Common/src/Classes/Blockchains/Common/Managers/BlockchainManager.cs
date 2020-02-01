using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
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
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {



	public interface IBlockchainManager : IManagerBase {


		bool BlockchainSyncing { get; }
		bool BlockchainSynced { get; }

		bool? WalletSyncedNoWait { get; }
		bool WalletSynced { get; }
		bool WalletSyncing { get; }


		void SynchronizeBlockchain(bool force);
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

		

		private int lastConnectionCount = 0;

		protected bool NetworkPaused => this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused;

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

				this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider.DeleteExpiredTransactions();

				this.nextExpiredTransactionCheck = DateTime.UtcNow.AddMinutes(30);
			}
		}

		

		protected override void Initialize(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext) {
			base.Initialize(workflow, taskRoutingContext);

			this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PeerConnectionsCountUpdated += ChainNetworkingProviderBaseOnPeerConnectionsCountUpdated;

			// make sure we check our status when starting
			this.CheckBlockchainSynchronizationStatus();

			this.CheckWalletSynchronizationStatus();

			// connect to the wallet events
			this.SetPassphraseHandlers();

			this.LoadWalletIfRequired();

			this.RoutedTaskRoutingReceiver.CheckTasks();
		}

		protected virtual void LoadWalletIfRequired() {
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
						Repeater.Repeat(() => {
							Dictionary<int, string> passphrases = new Dictionary<int, string>();
							passphrases.Add(0, "toto");
							if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(default, chainConfig.EncryptWallet, chainConfig.EncryptWalletKeys, false, passphrases.ToImmutableDictionary())) {
								throw new ApplicationException("Failed to create a new wallet");
							}
						}, 2);

						LoadWallet();
					}
				}
			}
		}
		
		protected virtual void SetPassphraseHandlers() {
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().PassphraseCaptureMethod == AppSettingsBase.PassphraseQueryMethod.Event) {
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetExternalPassphraseHandlers(this.WalletProviderOnWalletPassphraseRequest, this.WalletProviderOnWalletKeyPassphraseRequest, this.WalletProviderOnWalletCopyKeyFileRequest, this.CopyWalletRequest);
			}
		}
		
		protected virtual void ChainNetworkingProviderBaseOnPeerConnectionsCountUpdated(int count) {

			int minimumSyncPeerCount = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MinimumSyncPeerCount;

			if(this.lastConnectionCount < minimumSyncPeerCount && count >= minimumSyncPeerCount) {
				// we just got enough peers to potentially first peer, let's sync
				this.SynchronizeBlockchain(true);
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
		protected virtual void CheckBlockchainSynchronizationStatus() {

			if(this.CentralCoordinator.IsShuttingDown) {
				return;
			}

			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2PEnabled) {

				this.SynchronizeBlockchain(false);

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
		public void SynchronizeBlockchain(bool force) {
			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2PEnabled) {

				if(force) {
					// let's for ce a sync by setting the chain as desynced
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTime.MinValue;
				}

				if(!this.NetworkPaused && !this.BlockchainSyncing && !this.BlockchainSynced && this.CheckNetworkSyncable()) {

					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// if we are not synchronized, we go ahead and do it.
					if(!this.hasBlockchainSyncedOnce || chainStateProvider.IsChainDesynced) {
						// that's it, we launch a chain sync
						lock(this.locker) {
							
							if(this.chainSynchWorkflow != null && this.chainSynchWorkflow .IsCompleted) {
								System.Threading.Tasks.Task.Run(() => this.chainSynchWorkflow?.Dispose());
								this.chainSynchWorkflow = null;
							}

							if(this.chainSynchWorkflow == null) {

								// ok, we did at least once
								this.hasBlockchainSyncedOnce = true;

								this.chainSynchWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateChainSynchWorkflow(this.CentralCoordinator.FileSystem);

								// when its done, we can clear it here. not necessary, but keeps things cleaner.
								this.chainSynchWorkflow.Completed += (success, workflow) => {
									lock(this.locker) {

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
			var networkingProvider = this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase;
			int minimumSyncPeerCount = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.MinimumSyncPeerCount;

			return networkingProvider.HasPeerConnections && networkingProvider.CurrentPeerCount >= minimumSyncPeerCount;
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
					this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(2);
				}
			} else {
				this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(5);
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
				this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(1);

				return;
			}

			if(!this.NetworkPaused && this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded && ((mobileForce && GlobalSettings.ApplicationSettings.SynclessMode) || (!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableWalletSync && force) || !walletSynced.Value)) {
				lock(this.locker) {
					if(this.synchWalletWorkflow != null && this.synchWalletWorkflow .IsCompleted) {
						System.Threading.Tasks.Task.Run(() => this.synchWalletWorkflow?.Dispose());
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
						this.synchWalletWorkflow.Completed += (success, workflow) => {
							lock(this.locker) {

								// ok, now we can wait the regular intervals
								var walletIsSynced = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced;
								
								if(success && walletIsSynced.HasValue && walletIsSynced.Value && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
									this.nextWalletSynchCheck = this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
								} else {
									this.nextWalletSynchCheck = DateTime.UtcNow.AddSeconds(5);
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
					var walletIsSynced = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced;

					if(!walletIsSynced.HasValue) {
						return false;
					}
					return (!this.WalletSyncing || (this.synchWalletWorkflow?.IsCompleted ?? true)) && walletIsSynced.Value;
				}
			}
		}

		/// <summary>
		///     a special method to sync the wallet and chain from an external source.
		/// </summary>
		/// <param name="synthesizedBlock"></param>
		public void SynchronizeBlockchainExternal(string synthesizedBlock) {
			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				SynthesizedBlockAPI synthesizedBlockApi = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.DeserializeSynthesizedBlockAPI(synthesizedBlock);
				SynthesizedBlock synthesizedBlockInstance = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ConvertApiSynthesizedBlock(synthesizedBlockApi);

				// lets cache the results
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CacheSynthesizedBlock(synthesizedBlockInstance);

				if(synthesizedBlockApi.BlockId == 1) {

					// that's pretty important
					
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception = DateTime.ParseExact(synthesizedBlockApi.SynthesizedGenesisBlockBase.Inception, "o", CultureInfo.InvariantCulture,  DateTimeStyles.AdjustToUniversal);
				}

				// we set the chain height to this block id
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DownloadBlockHeight = synthesizedBlockInstance.BlockId;
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight = synthesizedBlockInstance.BlockId;
				this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight = synthesizedBlockInstance.BlockId;

				// ensure that we run the general transactions
				string cachePath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetGeneralCachePath();

				using(SerializationTransactionProcessor serializationTransactionProcessor = new SerializationTransactionProcessor(cachePath, this.CentralCoordinator.FileSystem)) {
					this.CentralCoordinator.ChainComponentProvider.InterpretationProviderBase.ProcessBlockImmediateGeneralImpact(synthesizedBlockInstance, serializationTransactionProcessor);

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

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				RequestCopyWalletSystemMessageTask requestCopyWalletTask = new RequestCopyWalletSystemMessageTask(() => {
					resetEvent.Set();
				});

				this.PostChainEvent(requestCopyWalletTask);

				// wait up to 5 minutes for the wallet to be ready to load
				resetEvent.Wait(TimeSpan.FromMinutes(5));
			}

			int g = 0;
		}

		private SecureString WalletProviderOnWalletPassphraseRequest(CorrelationContext correlationContext, int attempt) {
			Log.Information("Requesting wallet passphrase.");

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				if(correlationContext.IsNew) {
					correlationContext.InitializeNew();
				}

				RequestWalletPassphraseSystemMessageTask loadWalletPassphraseTask = new RequestWalletPassphraseSystemMessageTask(attempt, () => {
					resetEvent.Set();
				});

				this.PostChainEvent(loadWalletPassphraseTask, correlationContext);

				// wait until we get the passphrase back
				resetEvent.Wait();


				return loadWalletPassphraseTask.Passphrase;
			}
		}

		private SecureString WalletProviderOnWalletKeyPassphraseRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				RequestWalletKeyPassphraseSystemMessageTask loadWalletKeyPasshraseTask = new RequestWalletKeyPassphraseSystemMessageTask(accountuuid, keyname, attempt, () => {
					resetEvent.Set();
				});

				this.PostChainEvent(loadWalletKeyPasshraseTask);

				// wait up to 5 hours for the wallet to be ready to load
				resetEvent.Wait(TimeSpan.FromHours(5));


				return loadWalletKeyPasshraseTask.Passphrase;
			}
		}

		private void WalletProviderOnWalletCopyKeyFileRequest(CorrelationContext correlationContext, Guid accountuuid, string keyname, int attempt) {
			Log.Information($"Requesting wallet key {keyname} passphrase.");

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				RequestCopyWalletKeyFileSystemMessageTask loadCopyWalletKeyFileTask = new RequestCopyWalletKeyFileSystemMessageTask(accountuuid, keyname, attempt, () => {
					resetEvent.Set();
				});

				this.PostChainEvent(loadCopyWalletKeyFileTask);

				// wait up to 5 hours for the wallet to be ready to load
				resetEvent.Wait(TimeSpan.FromHours(5));
			}
		}

		public void ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {

			this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {
				provider.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases);
			});

		}
	#endregion
	

	}
}