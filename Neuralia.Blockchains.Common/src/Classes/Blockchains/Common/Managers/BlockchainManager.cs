using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IBlockchainManager : IManagerBase {

		Task<bool> BlockchainSyncing(LockContext lockContext);
		Task<bool> BlockchainSynced(LockContext lockContext);

		Task<bool?> WalletSyncedNoWait(LockContext lockContext);
		Task<bool> WalletSynced(LockContext lockContext);
		Task<bool> WalletSyncing(LockContext lockContext);

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

		private readonly RecursiveAsyncLock asyncLocker = new RecursiveAsyncLock();

		// the sync workflow we keep as a reference.
		private IClientChainSyncWorkflow chainSyncWorkflow;
		private int lastConnectionCount;
		private DateTime? nextBlockchainSyncCheck;
		private DateTime? nextExpiredTransactionCheck;
		private DateTime? cleanAppointmentsRegistry;
		private readonly ClosureWrapper<DateTime?> checkOperatingMode = new ClosureWrapper<DateTime?>();
		private DateTime? checkTransactionRetries;
	
		private DateTime? nextWalletSyncCheck;
		private ISyncWalletWorkflow syncWalletWorkflow;

		public BlockchainManager(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, 1, 3000) {
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;

			// give it some time before the first run
			this.checkTransactionRetries = DateTimeEx.CurrentTime.AddMinutes(1);
		}

		protected bool NetworkPaused => this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsPaused;

		protected new CENTRAL_COORDINATOR CentralCoordinator => base.CentralCoordinator;

		protected readonly ClosureWrapper<Task> checkOperatingModeTask = new ClosureWrapper<Task>();
		
		/// <summary>
		///     every once in a while, we check for the sync status
		/// </summary>
		protected override async Task ProcessLoop(IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			await base.ProcessLoop(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);

			var appointmentProvider = this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase;
			if(this.ShouldAct(ref this.nextBlockchainSyncCheck)) {

				try {
					await this.CheckBlockchainSynchronizationStatus(lockContext).ConfigureAwait(false);

				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to do a blockchain sync check. will retry");
				}
			}

			if(this.ShouldAct(ref this.nextWalletSyncCheck)) {

				try {
					await this.CheckWalletSynchronizationStatus(lockContext).ConfigureAwait(false);

				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to a wallet sync check. will retry");
				}
			}

			if(this.ShouldAct(ref this.nextExpiredTransactionCheck)) {

				try {
					await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider.DeleteExpiredTransactions().ConfigureAwait(false);

					this.nextExpiredTransactionCheck = DateTimeEx.CurrentTime.AddMinutes(30);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to delete expired transactions. will retry");
					this.checkTransactionRetries = DateTimeEx.CurrentTime.AddSeconds(30);
				}
			}

			var checkOperatingModeTime = this.checkOperatingMode.Value;
			if(this.ShouldAct(ref checkOperatingModeTime) && (this.checkOperatingModeTask.Value == null || this.checkOperatingModeTask.Value.IsCompleted)) {

				this.checkOperatingMode.Value = DateTimeEx.MaxValue;
				
				this.checkOperatingModeTask.Value = Task.Run(async () => {
					
					await appointmentProvider.CheckOperatingMode(lockContext).ConfigureAwait(false);
					
				}).ContinueWith(t => {
					this.checkOperatingModeTask.Value = null;

					if(t.IsFaulted) {
						this.checkOperatingMode.Value = DateTimeEx.CurrentTime.AddSeconds(15);
					} else {
						if(appointmentProvider.OperatingMode == Enums.OperationStatus.Appointment) {
							// in an appointment, we can go faster
							this.checkOperatingMode.Value = DateTimeEx.CurrentTime.AddSeconds(15);
						} else if(appointmentProvider.IsValidator && appointmentProvider.IsValidatorWindow) {
							// we need to be proactive
							this.checkOperatingMode.Value = DateTimeEx.CurrentTime.AddSeconds(10);
						} else if(appointmentProvider.IsValidator && appointmentProvider.IsValidatorWindowProximity) {
							// we need to be proactive
							this.checkOperatingMode.Value = DateTimeEx.CurrentTime.AddSeconds(30);
						} else {
							// we can be very slow in this mode
							this.checkOperatingMode.Value = DateTimeEx.CurrentTime.AddMinutes(1);
						}
					}
				});
			}
			
			if(this.ShouldAct(ref this.cleanAppointmentsRegistry)) {

				try {
					await appointmentProvider.CleanAppointmentsRegistry().ConfigureAwait(false);

					this.cleanAppointmentsRegistry = DateTimeEx.CurrentTime.AddMinutes(60);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to clean the appointment registry. will retry");
					this.checkTransactionRetries = DateTimeEx.CurrentTime.AddMinutes(1);
				}
				
			}

			if(this.ShouldAct(ref this.checkTransactionRetries)) {

				try {
					await this.CheckTransactionCache().ConfigureAwait(false);
					
					this.checkTransactionRetries = DateTimeEx.CurrentTime.AddMinutes(5);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Debug(ex, "Failed to check the transaction cache. will retry");
					this.checkTransactionRetries = DateTimeEx.CurrentTime.AddSeconds(30);
				}
			}
		}

		/// <summary>
		/// check the transaction cache, clear expired and retry some missing ones
		/// </summary>
		/// <returns></returns>
		protected virtual async Task CheckTransactionCache() {
			if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {

				LockContext lockContext = null;
				
				// now we ensure that all timed out in the wallet are updated
				bool changed = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ResetAllTimedOut(lockContext).ConfigureAwait(false);

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableAutomaticRetry) {
					// now process any that requires a retry
					var retries = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetRetryEntriesBase(lockContext).ConfigureAwait(false);

					foreach(var retry in retries) {
						
						IWorkflow workflow = null;

						if(retry.EventType == WalletGenerationCache.DispatchEventTypes.Transaction) {
							workflow = this.CreateTransactionWorkflow(new ComponentVersion<TransactionType>(retry.Version));
						} else if(retry.EventType == WalletGenerationCache.DispatchEventTypes.Message) {
							workflow = this.CreateMessageWorkflow(new ComponentVersion<BlockchainMessageType>(retry.Version));
						}

						if(workflow is IEventGenerationWorkflowBase eventGenerationWorkflowBase) {
							eventGenerationWorkflowBase.WalletGenerationCache = retry;
							this.CentralCoordinator.PostWorkflow(eventGenerationWorkflowBase);
						} else {
							this.CentralCoordinator.Log.Warning($"Failed to map workflow for event type {retry.Version}.");
						}
					}
				}
			}
		}

		protected virtual IWorkflow CreateTransactionWorkflow(ComponentVersion<TransactionType> version) {
			var factory = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase;

			if(version.Type == TransactionTypes.Instance.STANDARD_PRESENTATION) {
				if(version == (1, 0)) {
					return factory.CreatePresentationTransactionChainWorkflow(new CorrelationContext(), "");
				}
			}
			else if(version.Type == TransactionTypes.Instance.JOINT_PRESENTATION) {
				// if(version == (1, 0)) {
				// 	workflow = factory.CreatePresentationTransactionChainWorkflow(new CorrelationContext(), accountCode, expiration);
				// }
				throw new NotImplementedException();
			}
			else if(version.Type == TransactionTypes.Instance.KEY_CHANGE) {
				if(version == (1, 0)) {
					return factory.CreateChangeKeyTransactionWorkflow(0, "", new CorrelationContext());
				}
			} 
			
			throw new NotImplementedException();
		}
		
		protected virtual IWorkflow CreateMessageWorkflow(ComponentVersion<BlockchainMessageType> version) {
			var factory = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase;

			if(version.Type == BlockchainMessageTypes.Instance.ELECTIONS_REGISTRATION) {
				if(version == (1, 0)) {
					return factory.CreateSendElectionsCandidateRegistrationMessageWorkflow(new AccountId(),Enums.MiningTiers.FirstTier, new ElectionsCandidateRegistrationInfo(), AppSettingsBase.ContactMethods.WebOrGossip, new CorrelationContext()  );
				}
			}
			else if(version.Type == BlockchainMessageTypes.Instance.INITIATION_APPOINTMENT_REQUESTED) {
				if(version == (1, 0)) {
					return factory.CreateSendInitiationAppointmentRequestMessageWorkflow(1, new CorrelationContext() );
				}
			} 
			else if(version.Type == BlockchainMessageTypes.Instance.APPOINTMENT_REQUESTED) {
				if(version == (1, 0)) {
					return factory.CreateSendAppointmentRequestMessageWorkflow( 1, new CorrelationContext() );
				}
			} 
			else if(version.Type == BlockchainMessageTypes.Instance.APPOINTMENT_VERIFICATION_RESULTS) {
				if(version == (1, 0)) {
					return factory.CreateSendAppointmentVerificationResultsMessageWorkflow( DateTime.MinValue, new CorrelationContext());
				}
			} 
			throw new NotImplementedException();
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

			BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(configuration.LoadWalletOnStart && !this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {

				bool exists = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.WalletFileExists(lockContext).ConfigureAwait(false);
				bool fullyCreated = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.WalletFullyCreated(lockContext).ConfigureAwait(false);
				
				if(configuration.CreateMissingWallet && !exists ||!fullyCreated) {
					//TODO: passphrases? this here is mostly for debug
					// if we must, we will create a new wallet

					Dictionary<int, string> passphrases = new Dictionary<int, string>();
					passphrases.Add(0, "toto");

					if(!await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(default, configuration.AccountType, configuration.EncryptWallet, configuration.EncryptWalletKeys, false, passphrases.ToImmutableDictionary(), lockContext).ConfigureAwait(false)) {
						throw new ApplicationException("Failed to create a new wallet");
					}

				}

				if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
					try {

						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadWallet(new CorrelationContext(), lockContext).ConfigureAwait(false);
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletLoaded(lockContext).ConfigureAwait(false);

					} catch(WalletNotLoadedException ex) {
						this.CentralCoordinator.Log.Warning("Failed to load wallet. Not loaded.");
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Warning("Failed to load wallet. Not loaded.", ex);
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

			if((this.lastConnectionCount < minimumSyncPeerCount) && (count >= minimumSyncPeerCount)) {
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
					this.nextBlockchainSyncCheck = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextBlockchainSyncCheck = DateTimeEx.CurrentTime.AddSeconds(2);
				}
			} else {
				this.nextBlockchainSyncCheck = DateTimeEx.MaxValue;
			}
		}

		/// <summary>
		///     Perform a blockchain sync
		/// </summary>
		public async Task SynchronizeBlockchain(bool force, LockContext lockContext) {
			if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableSync && GlobalSettings.ApplicationSettings.P2PEnabled) {

				if(force) {
					// let's for ce a sync by setting the chain as desynced
					this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTimeEx.MinValue;
				}

				if(!this.NetworkPaused && !await this.BlockchainSyncing(lockContext).ConfigureAwait(false) && !await this.BlockchainSynced(lockContext).ConfigureAwait(false) && this.CheckNetworkSyncable()) {

					IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

					// if we are not synchronized, we go ahead and do it.
					if(!this.hasBlockchainSyncedOnce || chainStateProvider.IsChainDesynced) {
						// that's it, we launch a chain sync
						using(LockHandle handle = await this.asyncLocker.LockAsync().ConfigureAwait(false)) {

							if((this.chainSyncWorkflow != null) && this.chainSyncWorkflow.IsCompleted) {
								Task task = Task.Run(() => this.chainSyncWorkflow?.Dispose());
								this.chainSyncWorkflow = null;
							}

							if(this.chainSyncWorkflow == null) {

								// ok, we did at least once
								this.hasBlockchainSyncedOnce = true;

								this.chainSyncWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateChainSynchWorkflow(this.CentralCoordinator.FileSystem);

								// when its done, we can clear it here. not necessary, but keeps things cleaner.
								this.chainSyncWorkflow.Completed += async (success, workflow) => {
									LockContext innerLockContext = null;

									using(await this.asyncLocker.LockAsync(innerLockContext).ConfigureAwait(false)) {

										if(success && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
											this.nextBlockchainSyncCheck = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.SyncDelay);
										} else {
											// we never synced, we need to check more often to be ready to do so
											this.nextBlockchainSyncCheck = DateTimeEx.CurrentTime.AddSeconds(5);
										}

										this.chainSyncWorkflow = null;
									}
								};

								this.CentralCoordinator.PostWorkflow(this.chainSyncWorkflow);
							}
						}
					}
				}
			}
		}

		/// <summary>
		///     Return the state of the network and if it is syncable for us.
		/// </summary>
		/// <returns></returns>
		protected virtual bool CheckNetworkSyncable() {
			IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> networkingProvider = this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase;

			var chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			int minimumSyncPeerCount = chainConfiguration.MinimumSyncPeerCount;

			bool hasPeers = networkingProvider.HasPeerConnections && (networkingProvider.CurrentPeerCount >= minimumSyncPeerCount);
			bool useWeb = chainConfiguration.ChainSyncMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			return hasPeers || useWeb;
		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public async Task<bool> BlockchainSyncing(LockContext lockContext) {

			using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return (this.chainSyncWorkflow != null) && !this.chainSyncWorkflow.IsCompleted;
			}

		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?
		/// </summary>
		public async Task<bool> BlockchainSynced(LockContext lockContext) {

			using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return (!(await this.BlockchainSyncing(handle).ConfigureAwait(false)) || (this.chainSyncWorkflow?.IsCompleted ?? true)) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced;

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

				await this.SynchronizeWallet(false, lockContext).ConfigureAwait(false);

				// lets check again in X seconds
				if(this.hasWalletSyncedOnce) {
					// ok, now we can wait the regular intervals
					this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
				} else {
					// we never synced, we need to check more often to be ready to do so
					this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(2);
				}
			} else {
				this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(5);
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

			bool? walletSynced = await this.WalletSyncedNoWait(lockContext).ConfigureAwait(false);

			if(!walletSynced.HasValue) {
				// we could not verify, try again later
				this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(1);

				return;
			}

			if(!this.NetworkPaused && this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded && ((mobileForce && GlobalSettings.ApplicationSettings.SynclessMode) || (!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().DisableWalletSync && force) || !walletSynced.Value)) {
				using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
					if((this.syncWalletWorkflow != null) && this.syncWalletWorkflow.IsCompleted) {
						Task task = Task.Run(() => this.syncWalletWorkflow?.Dispose());
						this.syncWalletWorkflow = null;
					}

					if(this.syncWalletWorkflow == null) {

						this.hasWalletSyncedOnce = true;
						this.syncWalletWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateSyncWalletWorkflow();

						this.syncWalletWorkflow.AllowGrowth = allowGrowth;

						if(blocks?.Any() ?? false) {
							foreach(IBlock block in blocks.Where(b => b != null).OrderBy(b => b.BlockId.Value)) {
								this.syncWalletWorkflow.LoadedBlocks.AddSafe(block.BlockId, block);
							}
						}

						// when its done, we can clear it here. not necessary, but keeps things cleaner.
						this.syncWalletWorkflow.Completed += async (success, workflow) => {

							// ok, now we can wait the regular intervals
							LockContext lockContext2 = null;
							bool? walletIsSynced = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced(lockContext2).ConfigureAwait(false);

							if(success && walletIsSynced.HasValue && walletIsSynced.Value && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
								this.nextWalletSyncCheck = this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.WalletSyncDelay);
							} else {
								this.nextWalletSyncCheck = DateTimeEx.CurrentTime.AddSeconds(5);
							}

							this.syncWalletWorkflow = null;

						};

						this.CentralCoordinator.PostWorkflow(this.syncWalletWorkflow);
					}
				}
			}

		}

		/// <summary>
		///     are we in the active process of syncing?
		/// </summary>
		public async Task<bool> WalletSyncing(LockContext lockContext) {

			using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				return (this.syncWalletWorkflow != null) && !this.syncWalletWorkflow.IsCompleted;
			}

		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public async Task<bool?> WalletSyncedNoWait(LockContext lockContext) {

			using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				bool? walletSynced = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait(handle).ConfigureAwait(false);

				if(!walletSynced.HasValue) {
					return null;
				}

				return (!await this.WalletSyncing(handle).ConfigureAwait(false) || (this.syncWalletWorkflow?.IsCompleted ?? true)) && walletSynced.Value;

			}
		}

		/// <summary>
		///     Is the chain not actively syncing and in a synched state?  if we got stuck by a wallet transaction, we dont wait
		///     and return null, or uncertain state.
		/// </summary>
		public async Task<bool> WalletSynced(LockContext lockContext) {

			using(LockHandle handle = await this.asyncLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				bool? walletIsSynced = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.Synced(handle).ConfigureAwait(false);

				if(!walletIsSynced.HasValue) {
					return false;
				}

				return (!await this.WalletSyncing(handle).ConfigureAwait(false) || (this.syncWalletWorkflow?.IsCompleted ?? true)) && walletIsSynced.Value;
			}

		}

	#endregion

	#region wallet manager

		private async Task CopyWalletRequest(CorrelationContext correlationContext, int attempt, LockContext lockContext) {
			this.CentralCoordinator.Log.Information("Requesting loading wallet.");

			using(AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false)) {

				RequestCopyWalletSystemMessageTask requestCopyWalletTask = new RequestCopyWalletSystemMessageTask(() => {
					resetEvent.Set();
				});

				this.PostChainEvent(requestCopyWalletTask);

				// wait up to 5 minutes for the wallet to be ready to load
				await resetEvent.WaitAsync(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
			}
		}

		private async Task<(SecureString passphrase, bool keysToo)> WalletProviderOnWalletPassphraseRequest(CorrelationContext correlationContext, int attempt, LockContext lockContext) {
			this.CentralCoordinator.Log.Information("Requesting wallet passphrase.");

			using AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);

			RequestWalletPassphraseSystemMessageTask loadWalletPassphraseTask = new RequestWalletPassphraseSystemMessageTask(attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletPassphraseTask, correlationContext);

			// wait until we get the passphrase back
			await resetEvent.WaitAsync().ConfigureAwait(false);

			return (loadWalletPassphraseTask.Passphrase, loadWalletPassphraseTask.KeysToo);

		}

		private async Task<SecureString> WalletProviderOnWalletKeyPassphraseRequest(CorrelationContext correlationContext, string accountCode, string keyname, int attempt, LockContext lockContext) {
			this.CentralCoordinator.Log.Information($"Requesting wallet key {keyname} passphrase.");

			using AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);

			RequestWalletKeyPassphraseSystemMessageTask loadWalletKeyPasshraseTask = new RequestWalletKeyPassphraseSystemMessageTask(accountCode, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadWalletKeyPasshraseTask);

			// wait up to 5 hours for the wallet to be ready to load
			await resetEvent.WaitAsync(TimeSpan.FromHours(5)).ConfigureAwait(false);

			return loadWalletKeyPasshraseTask.Passphrase;

		}

		private async Task WalletProviderOnWalletCopyKeyFileRequest(CorrelationContext correlationContext, string accountCode, string keyname, int attempt, LockContext lockContext) {
			this.CentralCoordinator.Log.Information($"Requesting wallet key {keyname} passphrase.");

			using AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);

			RequestCopyWalletKeyFileSystemMessageTask loadCopyWalletKeyFileTask = new RequestCopyWalletKeyFileSystemMessageTask(accountCode, keyname, attempt, () => {
				resetEvent.Set();
			});

			this.PostChainEvent(loadCopyWalletKeyFileTask);

			// wait up to 5 hours for the wallet to be ready to load
			await resetEvent.WaitAsync(TimeSpan.FromHours(5)).ConfigureAwait(false);
		}

		public Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {

			return this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token, lc) => {
				return provider.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases, lc);
			}, lockContext);

		}

	#endregion

	}
}