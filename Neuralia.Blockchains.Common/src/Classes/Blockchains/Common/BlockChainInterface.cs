using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Extensions;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common {

	public interface IInterfaceSystemEventHandler {
		void ReceiveChainMessageTask(SystemMessageTask task);
		void ReceiveChainMessageTaskImmediate(SystemMessageTask task);
	}

	public interface IBlockChainInterface : INetworkRouter, IRoutedTaskRoutingHandler, IInterfaceSystemEventHandler {

		ICentralCoordinator CentralCoordinatorBase { get; }
		bool IsMiningAllowed { get; }
		bool IsMiningEnabled { get; }
		Task StartChain(LockContext lockContext);

		Task StopChain(LockContext lockContext);
		
		void TriggerChainSynchronization();
		void TriggerChainWalletSynchronization();

		void Pause();
		void Resume();
		
		TaskResult<string> Test(string data);

		TaskResult<bool> CreateNextXmssKey(Guid accountUuid, byte ordinal);
		TaskResult<SafeArrayHandle> SignXmssMessage(Guid accountUuid, SafeArrayHandle message);
		TaskResult<long> QueryBlockHeight();
		TaskResult<long> QueryLowestAccountBlockSyncHeight();
		
		TaskResult<BlockchainInfo> QueryBlockChainInfo();
		TaskResult<List<MiningHistory>> QueryMiningHistory(int page, int pageSize, byte maxLevel);

		TaskResult<bool> IsWalletLoaded();
		TaskResult<ChainStatusAPI> QueryChainStatus();
		TaskResult<WalletInfoAPI> QueryWalletInfo();

		TaskResult<bool> IsBlockchainSynced();
		TaskResult<bool> IsWalletSynced();

		TaskResult<bool> SyncBlockchain(bool force);
		TaskResult<bool> SyncBlockchainExternal(string synthesizedBlock);
		TaskResult<bool> SyncBlockchainExternalBatch(IEnumerable<string> synthesizedBlocks);
		TaskResult<bool> WalletExists();

		TaskResult<bool> LoadWallet(CorrelationContext correlationContext, string passphrase = null);

		TaskResult<bool> CreateNewWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, bool publishAccount);
		TaskResult<bool> CreateAccount(CorrelationContext correlationContext, string accountName, bool publishAccount, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases);

		TaskResult<bool> SetWalletPassphrase(int correlationId, int keyCorrelationCode, string passphrase, bool setKeysToo = false);
		TaskResult<bool> SetWalletKeyPassphrase(int correlationId, int keyCorrelationCode, string passphrase);
		TaskResult<bool> WalletKeyFileCopied(int correlationId, int keyCorrelationCode);

		TaskResult<List<WalletTransactionHistoryHeaderAPI>> QueryWalletTransactionHistory(Guid accountUuid);
		TaskResult<WalletTransactionHistoryDetailsAPI> QueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId);

		TaskResult<List<WalletAccountAPI>> QueryWalletAccounts();
		TaskResult<string> QueryDefaultWalletAccountId();
		TaskResult<Guid> QueryDefaultWalletAccountUuid();
		
		TaskResult<WalletAccountDetailsAPI> QueryWalletAccountDetails(Guid accountUuid);
		TaskResult<TransactionId> QueryWalletAccountPresentationTransactionId(Guid accountUuid);

		TaskResult<bool> ChangeKey(byte changingKeyOrdinal, string note, CorrelationContext correlationContext);
		TaskResult<IBlock> LoadBlock(long blockId);

		TaskResult<bool> PresentAccountPublicly(CorrelationContext correlationContext, Guid? accountUuid, byte expiration = 0);

		TaskResult<List<ElectedCandidateResultDistillate>> PerformOnDemandElection(BlockElectionDistillate blockElectionDistillate);
		TaskResult<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults);

		ImmutableList<string> QueryPeersList();
		ImmutableList<string> QueryIPsList();
		
		int QueryPeerCount();

		void PrintChainDebug(string item);

		event DelegatesBase.SimpleDelegate BlockchainStarted;
		event DelegatesBase.SimpleDelegate BlockchainLoaded;
		event DelegatesBase.SimpleDelegate BlockChainSynced;
		event DelegatesBase.SimpleDelegate CopyWalletFileRequest;
		event Action ImportantWalletUpdateRaised;

		event Delegates.ChainEventDelegate ChainEventRaised;

		Task EnableMining(LockContext lockContext, AccountId delegateAccountId = null);

		Task DisableMining(LockContext lockContext);

		TaskResult<bool> QueryBlockchainSynced();
		TaskResult<ElectionContextAPI> QueryElectionContext(long blockId);
		TaskResult<bool> QueryWalletSynced();

		TaskResult<string> QueryBlock(long blockId);
		TaskResult<byte[]> QueryCompressedBlock(long blockId);

		TaskResult<Dictionary<TransactionId, byte[]>> QueryBlockBinaryTransactions(long blockId);

		TaskResult<object> BackupWallet();
		TaskResult<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, int iterations);

		TaskResult<bool> CreateNewAccount(CorrelationContext correlationContext, string name, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases);
		TaskResult<bool> SetActiveAccount(string name);
		TaskResult<bool> SetActiveAccount(Guid accountUuid);
	}

	public interface IBlockChainInterface<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockChainInterface
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		CENTRAL_COORDINATOR CentralCoordinator { get; }
	}

	public abstract class BlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IDisposableExtended, IRoutedTaskRoutingHandler
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly AppSettingsBase AppSettingsBase;
		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		/// <summary>
		///     Used to receive targeted system events
		/// </summary>
		protected readonly ColoredRoutedTaskReceiver ColoredRoutedTaskReceiver;

		/// <summary>
		///     The receiver that allows us to act as a task endpoint mailbox
		/// </summary>
		protected readonly SpecializedRoutedTaskRoutingReceiver<IRoutedTaskRoutingHandler> RoutedTaskReceiver;

		private Task<bool> eventsPoller;
		private Timer tasksPoller;
		private bool poller_active = true;

		private ManualResetEventSlim pollerResetEvent;
		
		private static readonly TimeSpan TASK_CHECK_SPAN = TimeSpan.FromSeconds(2);

		private readonly TimeSpan taskCheckSpan;

		protected BlockChainInterface(CENTRAL_COORDINATOR centralCoordinator, TimeSpan? taskCheckSpan = null) {
			
			this.taskCheckSpan = taskCheckSpan??TASK_CHECK_SPAN;
			
			this.centralCoordinator = centralCoordinator;

			this.RoutedTaskReceiver = new SpecializedRoutedTaskRoutingReceiver<IRoutedTaskRoutingHandler>(this.centralCoordinator, this);

			this.ColoredRoutedTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleMessages);
		}

		public bool IsChainStarted { get; private set; }

		/// <summary>
		///     has the transactionchain been loaded from disk at startup?
		/// </summary>
		/// <returns></returns>
		public bool IsBlockChainLoaded { get; private set; }

		public CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;
		public ICentralCoordinator CentralCoordinatorBase => this.centralCoordinator;

		public Task EnableMining(LockContext lockContext, AccountId delegateAccountId = null) {

			
			return this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.EnableMining(null, delegateAccountId, lockContext);
		}

		public Task DisableMining(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.DisableMining(lockContext);
		}

		public bool IsMiningAllowed => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAllowed;
		public bool IsMiningEnabled => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled;

		public event DelegatesBase.SimpleDelegate BlockchainStarted;
		public event DelegatesBase.SimpleDelegate BlockchainLoaded;
		public event DelegatesBase.SimpleDelegate BlockChainSynced;
		public event DelegatesBase.SimpleDelegate CopyWalletFileRequest;

		public event Delegates.ChainEventDelegate ChainEventRaised;

		public event Action ImportantWalletUpdateRaised;
		
		// start our pletora of threads
		public virtual async Task StartChain(LockContext lockContext) {
			try {

				Log.Information($"Starting blockchain {this.centralCoordinator.ChainName} with chain path: {this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainDirectoryPath()}");

				// if anything was running, we stop it
				await this.StopChain(lockContext).ConfigureAwait(false);

				await this.centralCoordinator.Start().ConfigureAwait(false);

				this.StartOtherChainComponents();
				
				GlobalSettings.Instance.NodeInfo.AddChainSettings(this.centralCoordinator.ChainId, this.PrepareChainSettings());
				
				if(GlobalSettings.ApplicationSettings.P2PEnabled) {
					// if p2p is enabled, then we register our chain

					this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterChain(this);
				}

				this.IsChainStarted = true;

				this.TriggerBlockchainStarted();

				// we are ready and have started
				this.TriggerBlockchainLoaded();

				// start polling for system events
				await this.StartEventsPoller().ConfigureAwait(false);

			} catch(Exception ex) {
				Log.Error(ex, "Failed to start controllers");

				throw ex;
			}
		}

		public virtual async Task StopChain(LockContext lockContext) {
			if(!this.centralCoordinator.IsCompleted && this.centralCoordinator.IsStarted) {
				try {
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemovePIDLock().ConfigureAwait(false);

					this.poller_active = false;
					this.tasksPoller?.Dispose();
					
					await this.centralCoordinator.Stop().ConfigureAwait(false);
					this.centralCoordinator.Dispose();
				} catch(Exception ex) {
					Log.Error(ex, "Failed to stop controllers");

					throw ex;
				}
			}

			this.StopOtherChainComponents();

			this.IsChainStarted = false;
		}

		public void TriggerChainSynchronization() {
			this.RunBlockchainTaskMethod(async (service, taskRoutingContext, lc) => {

				await service.SynchronizeBlockchain(true, lc).ConfigureAwait(false);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public void TriggerChainWalletSynchronization() {

			this.RunBlockchainTaskMethod(async (service, taskRoutingContext, lc) => {

				await service.SynchronizeWallet(true, lc, true).ConfigureAwait(false);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		
		public void Pause() {
			this.centralCoordinator.Pause().WaitAndUnwrapException();
		}

		public void Resume() {
			this.centralCoordinator.Resume().WaitAndUnwrapException();
		}
		
		/// <summary>
		///     Prepare the publicly available chain settingsBase that we use
		/// </summary>
		/// <returns></returns>
		protected virtual ChainSettings PrepareChainSettings() {
			ChainSettings settings = new ChainSettings();

			// set the public chain settingsBase
			settings.ShareType = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().NodeShareType();

			return settings;
		}

		private async Task StartEventsPoller() {

			this.pollerResetEvent = new ManualResetEventSlim(false);

			//TODO: a timer here would be better
			// here we prepare the events poller that will check if we have events. This works better as a task than a timer.
			this.poller_active = true;
			this.eventsPoller = Task<Task<bool>>.Factory.StartNew(async () => {

				Thread.CurrentThread.IsBackground = true;
				LockContext lockContext = null;
				while(this.poller_active) {
					try {
						await this.ColoredRoutedTaskReceiver.CheckTasks().ConfigureAwait(false);
						
					} catch(Exception ex) {
						Log.Error(ex, "Failed to check for system events");
					}

					this.pollerResetEvent.Wait(TimeSpan.FromSeconds(3));
					this.pollerResetEvent.Reset();
				}

				return true;
			}, TaskCreationOptions.LongRunning).Unwrap();
			
	
			// perform a check for any message that has arrived and invoke the callbacks if there are any on the calling thread
			this.tasksPoller = new Timer(state => {
				try{
					this.CheckTasks().WaitAndUnwrapException();
				}
				catch(Exception ex){
					//TODO: do something?
					Log.Error(ex, "Timer exception");
				}
			}, this, this.taskCheckSpan, this.taskCheckSpan);

		}

		protected virtual void StartOtherChainComponents() {

		}

		protected virtual void StopOtherChainComponents() {

		}

	#region Interface Methods

		public abstract void PrintChainDebug(string item);

		public TaskResult<string> Test(string data) {
			
			return this.RunTaskMethodAsync(async (lc) => {


				//var card = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccounts(null).WaitAndUnwrapException();

				var acc = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(null).WaitAndUnwrapException();

				var dat = ByteArray.Create(8);
				TypeSerializer.Serialize(1L, dat.Span);
				var sig = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(acc.AccountUuid, dat, null).WaitAndUnwrapException();

				
				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ConnectableChanged(true));

				return "";
			}, null);
		}

		public TaskResult<bool> SetWalletPassphrase(int correlationId, int keyCorrelationCode, string passphrase, bool setKeysToo = false) {

			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {

				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
if(					this.keyQueries[keyCorrelationCode] != null){	this.keyQueries[keyCorrelationCode](passphrase, setKeysToo);}
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			}, lockContext);
		}

		public TaskResult<bool> SetWalletKeyPassphrase(int correlationId, int keyCorrelationCode, string passphrase) {
			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {

				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
if(					this.keyQueries[keyCorrelationCode] != null){ 					this.keyQueries[keyCorrelationCode](passphrase, false);}
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			}, lockContext);
		}

		public TaskResult<bool> WalletKeyFileCopied(int correlationId, int keyCorrelationCode) {
			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {
				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
if(					this.keyQueries[keyCorrelationCode] != null){ 					this.keyQueries[keyCorrelationCode](null, false);}
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			}, lockContext);
		}

		public TaskResult<bool> CreateNextXmssKey(Guid accountUuid, byte ordinal) {

			LockContext lockContext = null;
			return this.RunTaskMethodAsync(async (lc) => {

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNextXmssKey(accountUuid, ordinal, null).ConfigureAwait(false);

				return true;
			}, lockContext);
		}

		public TaskResult<SafeArrayHandle> SignXmssMessage(Guid accountUuid, SafeArrayHandle message) {
			LockContext lockContext = null;
			return this.RunTaskMethodAsync((lc) => {

				return this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(accountUuid, message, null);
			}, lockContext);
		}

		/// <summary>
		///     Query the current blockchain height
		/// </summary>
		/// <returns></returns>
		public TaskResult<long> QueryBlockHeight() {

			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {
				return this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.GetBlockHeight();
			}, lockContext);
		}
		
		public TaskResult<long> QueryLowestAccountBlockSyncHeight() {

			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {
				var result = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight(lc).WaitAndUnwrapException();

				return (long)(result ?? 0L);
			}, lockContext);
		}

		public TaskResult<BlockchainInfo> QueryBlockChainInfo() {
			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {
				return this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.GetBlockchainInfo();
			}, lockContext);
		}

		public TaskResult<List<MiningHistory>> QueryMiningHistory(int page, int pageSize, byte maxLevel) {
			LockContext lockContext = null;
			return this.RunTaskMethod((lc) => {
				return this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.GetMiningHistory(page, pageSize, maxLevel).Select(e => e.ToApiHistory()).ToList();
			}, lockContext);
		}

		public int QueryPeerCount() {
			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount;

		}

		public ImmutableList<string> QueryPeersList() {
			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AllConnectionsList.Select(c => c.ScoppedIp).ToImmutableList();
		}

		public ImmutableList<string> QueryIPsList() {
			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AllIPCache.ToImmutableList();
		}

		/// <summary>
		///     Query various basic status variables about the chain and it's wallet
		/// </summary>
		/// <returns></returns>
		public TaskResult<ChainStatusAPI> QueryChainStatus() {

			LockContext lockContext = null;
			return this.RunTaskMethodAsync(async (lc) => {

				LockContext lockContext = null;
				IWalletProviderProxy walletProvider = this.centralCoordinator.ChainComponentProvider.WalletProviderBase;

				BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
				int minimumDispatchPeerCount = chainConfiguration.MinimumDispatchPeerCount;
				int minigTier = (int)this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningTier;

				return new ChainStatusAPI { WalletInfo = await walletProvider.APIQueryWalletInfoAPI(lockContext).ConfigureAwait(false), MinRequiredPeerCount = minimumDispatchPeerCount, MiningTier = minigTier};
			}, lockContext);
		}

		/// <summary>
		///     Query various basic status variables about the chain and it's wallet
		/// </summary>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		public TaskResult<WalletInfoAPI> QueryWalletInfo() {
			LockContext lockContext = null;
			return this.RunTaskMethodAsync((lc) => {
				return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletInfoAPI(lc);
			}, lockContext);
		}

		public TaskResult<bool> IsBlockchainSynced() {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext, lc) => service.BlockchainSynced(lc).WaitAndUnwrapException(), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> IsWalletSynced() {

			return this.QueryWalletSynced();
		}

		public TaskResult<bool> SyncBlockchain(bool force) {
			return this.RunBlockchainTaskMethodAsync(async (service, taskRoutingContext, lc) => {

				await service.SynchronizeBlockchain(force, lc).ConfigureAwait(false);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> SyncBlockchainExternal(string synthesizedBlock) {
			
			return this.RunTaskMethodAsync(async (lc) => {
				await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.SynchronizeBlockchainExternal(synthesizedBlock, lc).ConfigureAwait(false);

				return true;
			});
		}

		public TaskResult<bool> SyncBlockchainExternalBatch(IEnumerable<string> synthesizedBlocks)
		{
			return this.RunTaskMethodAsync(async (lc) => {
				await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.SynchronizeBlockchainExternalBatch(synthesizedBlocks, lc).ConfigureAwait(false);

				return true;
			});
		}

		public TaskResult<bool> IsWalletLoaded() {

			return this.RunTaskMethod((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded);

		}

		public TaskResult<bool> WalletExists() {
			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.WalletFileExists(lc));

		}

		public TaskResult<bool> LoadWallet(CorrelationContext correlationContext, string passphrase = null) {

			return this.RunTaskMethodAsync(async (lc) => {

				ILoadWalletWorkflow workflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateLoadWalletWorkflow(correlationContext, passphrase);

				this.centralCoordinator.PostWorkflow(workflow);

                await workflow.Wait().ConfigureAwait(false);
				
				return true;
			});
		}

		public TaskResult<bool> CreateNewWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, bool publishAccount) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(correlationContext, accountName, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lc));
		}

		public TaskResult<bool> CreateAccount(CorrelationContext correlationContext, string accountName, bool publishAccount, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, lc));

		}

		public TaskResult<List<WalletTransactionHistoryHeaderAPI>> QueryWalletTransactionHistory(Guid accountUuid) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletTransactionHistory(accountUuid, lc));
		}

		public TaskResult<WalletTransactionHistoryDetailsAPI> QueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletTransactionHistoryDetails(accountUuid, transactionId, lc));
		}

		public TaskResult<List<WalletAccountAPI>> QueryWalletAccounts() {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccounts(lc));
		}

		public TaskResult<string> QueryDefaultWalletAccountId() {
			return this.RunTaskMethodAsync(async (lc) => (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lc).ConfigureAwait(false)).GetAccountId().ToString());
		}
		
		public TaskResult<Guid> QueryDefaultWalletAccountUuid() {
			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountUuid(lc));
		}
		
		public TaskResult<WalletAccountDetailsAPI> QueryWalletAccountDetails(Guid accountUuid) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccountDetails(accountUuid, lc));
		}

		public TaskResult<TransactionId> QueryWalletAccountPresentationTransactionId(Guid accountUuid) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccountPresentationTransactionId(accountUuid, lc));
		}

		public TaskResult<IBlock> LoadBlock(long blockId) {

			return this.RunTaskMethod((lc) => this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(new BlockId(blockId)));
		}

		public TaskResult<string> QueryBlock(long blockId) {
			return this.RunTaskMethod((lc) => this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockJson(new BlockId(blockId)));
		}

		public TaskResult<byte[]> QueryCompressedBlock(long blockId) {
			
			return this.RunTaskMethod((lc) => {

				string json = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockJson(new BlockId(blockId));
				
				BrotliCompression compressor = new BrotliCompression();

				using ByteArray simpleBytes = ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(json));

				using SafeArrayHandle compressed = compressor.Compress(simpleBytes, CompressionLevelByte.Optimal);

				return compressed.ToExactByteArrayCopy();

			});
		}

		public TaskResult<Dictionary<TransactionId, byte[]>> QueryBlockBinaryTransactions(long blockId) {
			
			return this.RunTaskMethod((lc) => {

				IBlock block = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(new BlockId(blockId));
				
				if(block != null) {
					BrotliCompression compressor = new BrotliCompression();

					return block.GetAllConfirmedTransactions().Select(t => {

						// now dehydrate each transaction into a byte array
						IDehydratedTransaction dehydratedTransaction = t.Value.Dehydrate(BlockChannelUtils.BlockChannelTypes.All);

						using IDataDehydrator rehydrator = DataSerializationFactory.CreateDehydrator();
						dehydratedTransaction.Dehydrate(rehydrator);

						SafeArrayHandle bytes = rehydrator.ToArray();
						SafeArrayHandle compressed = compressor.Compress(bytes, CompressionLevelByte.Optimal);
						
						var data = compressed.ToExactByteArrayCopy();

						compressed.Return();
						bytes.Return();

						return new {data, t.Key};
					}).ToDictionary(e => e.Key, e => e.data);
				}

				return new Dictionary<TransactionId, byte[]>();
			});
		}

		public TaskResult<bool> PresentAccountPublicly(CorrelationContext correlationContext, Guid? accountUuid, byte expiration = 0) {

			return this.RunTaskMethod((lc) => {

				using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

				var workflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreatePresentationTransactionChainWorkflow(correlationContext, accountUuid, expiration);

				workflow.Success += w => {
					resetEvent.Set();

					return Task.CompletedTask;
				};

				this.centralCoordinator.PostWorkflow(workflow);

				resetEvent.Wait();

				return true;

			});
		}

		public TaskResult<bool> CreateNewAccount(CorrelationContext correlationContext, string name, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {

			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteAccount(correlationContext, name, encryptKeys, encryptKeysIndividually, passphrases, null));
		}

		public TaskResult<bool> SetActiveAccount(string name) {
			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetActiveAccount(name, lc));
		}

		public TaskResult<bool> SetActiveAccount(Guid accountUuid) {
			return this.RunTaskMethodAsync((lc) => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetActiveAccount(accountUuid, lc));

		}

		public TaskResult<bool> QueryBlockchainSynced() {

			return this.RunBlockchainTaskMethodAsync(async (service, taskRoutingContext, lc) => {

				var synced = await service.BlockchainSynced(lc).ConfigureAwait(false);

				if(!synced) {
					await service.SynchronizeBlockchain(false, lc).ConfigureAwait(false);
				}

				return synced;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> QueryWalletSynced() {

			return this.RunBlockchainTaskMethodAsync(async (service, taskRoutingContext, lc) => {

				var synced = await service.WalletSynced(lc).ConfigureAwait(false);
				
				if(synced) {
					await service.SynchronizeWallet(false, lc).ConfigureAwait(false);
				}

				return synced;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<object> BackupWallet() {

			return this.RunTaskMethodAsync(async (lc) => {
				(string path, string passphrase, string salt, int iterations) results = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.BackupWallet(lc).ConfigureAwait(false);

				return (object) new WalletBackupAPI(){Path = results.path, Passphrase = results.passphrase, Salt = results.salt, Iterations = results.iterations};
			});
		}

		public TaskResult<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, int iterations)
		{
			return this.RunTaskMethodAsync(async (lc) =>
			{
				return await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RestoreWalletFromBackup(backupsPath, passphrase, salt, iterations, lc).ConfigureAwait(false);
			});
		}

		public TaskResult<List<ElectedCandidateResultDistillate>> PerformOnDemandElection(BlockElectionDistillate blockElectionDistillate) {
			
			return this.RunTaskMethodAsync((lc) => {
				return this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PerformElectionComputation(blockElectionDistillate, lc);

			});
		}

		public TaskResult<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults) {
			
			return this.RunTaskMethodAsync((lc) => {
				return this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults, lc);
			});
		}

		public TaskResult<ElectionContextAPI> QueryElectionContext(long blockId) {
			
			return this.RunTaskMethod((lc) => {

				IBlock block = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(new BlockId(blockId));
				
				if(block is IElectionBlock electionBlock && electionBlock.ElectionContext != null) {

					BrotliCompression compressor = new BrotliCompression();

					using SafeArrayHandle compressed = compressor.Compress(electionBlock.DehydratedElectionContext, CompressionLevelByte.Maximum);

					return new ElectionContextAPI() {
						Type = electionBlock.ElectionContext.Version.Type.Value.Value, ContextBytes = compressed.ToExactByteArrayCopy(), BlockId = blockId, MaturityId = blockId + electionBlock.ElectionContext.Maturity,
						PublishId = blockId + electionBlock.ElectionContext.Maturity + electionBlock.ElectionContext.Publication
					};

				}

				return new ElectionContextAPI() {Type = 0};
			});
		}

		public TaskResult<bool> ChangeKey(byte changingKeyOrdinal, string note, CorrelationContext correlationContext) {

			return this.RunTaskMethod((lc) => {
				using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

				var workflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changingKeyOrdinal, note, correlationContext);

				workflow.Success += w => {
					resetEvent.Set();
					
					return Task.CompletedTask;
				};

				this.centralCoordinator.PostWorkflow(workflow);

				resetEvent.Wait();

				return true;

			});
		}

	#endregion

	#region System Methods

		//run a return Task transformation method asynchronously and return the results in the calling thread via the Task. Example, getting a transactioncount as an int:
		private readonly ConcurrentDictionary<Guid, IRoutedTask> returnedQueries = new ConcurrentDictionary<Guid, IRoutedTask>();

		public TaskResult<T> RunMethod<T>(IRoutedTask<IRoutedTaskRoutingHandler, T> message) {
			return this.RunMethod(message, TimeSpan.FromSeconds(20));
		}

		public TaskResult<T> RunMethod<T>(IRoutedTask<IRoutedTaskRoutingHandler, T> message, TimeSpan timeout) {
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			var results = new TaskResult<T>();

			try {
				CancellationToken ct = tokenSource.Token;

				ManualResetEventSlim autoResetEvent = new ManualResetEventSlim(false);

				T Runner() {

					ct.ThrowIfCancellationRequested();

					autoResetEvent.Wait(ct);
					autoResetEvent?.Dispose();
					return message.Results;
				}

				// send the message
				//TODO: make this async
				this.DispatchTaskAsync(message, null).WaitAndUnwrapException(ct);

				if(((InternalRoutedTask) message).RoutingStatus == RoutedTask.RoutingStatuses.Disposed) {
					// ok, seems it was executed in thread and tis all done. we can return now
					results.awaitableTask = Task.FromResult(message.Results);

					return results;
				}
				
				// handle exceptions here
				
				message.OnCompleted += () => {
					this.returnedQueries.AddSafe(message.Id, message);

					results.TriggerCompleted(message.Results);
					autoResetEvent.Set();
				};

				// and start listening for the response
				results.awaitableTask = Task.Run(Runner, ct);
				
				results.task = results.awaitableTask.WithAllExceptions().ContinueWith(t => {

					Log.Error(t.Exception, "Failed to query blockchain");
					results.TriggerError(t.Exception);

				}, ct, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

				// set the required result information
				results.ctSource = tokenSource;

				return results;
			} catch(Exception ex) {
				Log.Error(ex, "Failed to create method task");

				throw;
			} finally {
				tokenSource.Dispose();
			}
		}

		public TaskResult<T> RunBlockchainTaskMethod<T>(Func<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, TaskRoutingContext, LockContext, T> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<T>();

			blockchainTask.SetAction(async (service, taskRoutingContext, lc) => {

				blockchainTask.Results = newAction(service, taskRoutingContext, lc);

			}, newCompleted);

			return this.RunMethod(blockchainTask);
		}
		
		public TaskResult<T> RunBlockchainTaskMethodAsync<T>(Func<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, TaskRoutingContext, LockContext, Task<T>> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<T>();

			blockchainTask.SetAction(async (service, taskRoutingContext, lc) => {

				blockchainTask.Results = await newAction(service, taskRoutingContext, lc).ConfigureAwait(false);

			}, newCompleted);

			return this.RunMethod(blockchainTask);
		}
		
		public TaskResult<T> RunTaskMethod<T>(Func<LockContext, T> runner, LockContext lockContext = null) {
			var results = new TaskResult<T>();
			
			// and start listening for the response
			results.awaitableTask = Task.Run(() => runner(lockContext));
			
			// handle exceptions here
			results.task = results.awaitableTask.WithAllExceptions().ContinueWith(t => {

			}, TaskContinuationOptions.OnlyOnFaulted);

			return results;
		}

		public TaskResult<T> RunTaskMethodAsync<T>(Func<LockContext, Task<T>> runner, LockContext lockContext = null) {
			var results = new TaskResult<T>();
			
			// and start listening for the response
			results.awaitableTask = runner(lockContext);
			
			// handle exceptions here
			results.task = results.awaitableTask.WithAllExceptions().ContinueWith(t => {

			}, TaskContinuationOptions.OnlyOnFaulted);

			return results;
		}

	#endregion

	#region event triggers

		public void TriggerBlockchainStarted() {
if(			this.BlockchainStarted != null){this.BlockchainStarted();}
		}

		public void TriggerBlockchainLoaded() {
if(			this.BlockchainLoaded != null){	this.BlockchainLoaded();}
		}

		public void TriggerBlockChainSynced() {
			if(this.BlockChainSynced != null) {
				this.BlockChainSynced();
			}
		}

		protected void  TriggerRequestCopyWalletFileRequest(RequestCopyWalletSystemMessageTask requestCopyWalletSystemTask) {
if(			this.CopyWalletFileRequest != null){this.CopyWalletFileRequest();}

			requestCopyWalletSystemTask.Completed();
		}

		private readonly Dictionary<int, Action<string, bool>> keyQueries = new Dictionary<int, Action<string, bool>>();

		protected void TriggerRequestWalletPassphrase(RequestWalletPassphraseSystemMessageTask requestWalletPassphraseSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestWalletPassphraseSystemMessageTask.correlationCode, (passphrase, keysToo) => {

				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new ApplicationException("Invalid passphrase");
				}

				requestWalletPassphraseSystemMessageTask.Passphrase = passphrase.ConvertToSecureString();
				requestWalletPassphraseSystemMessageTask.KeysToo = keysToo;
				
				requestWalletPassphraseSystemMessageTask.Completed();
			});
		}

		protected void TriggerRequestWalletKeyPassphrase(RequestWalletKeyPassphraseSystemMessageTask requestWalletKeyPassphraseSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestWalletKeyPassphraseSystemMessageTask.correlationCode, (passphrase, keysToo) => {

				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new ApplicationException("Invalid key passphrase");
				}

				requestWalletKeyPassphraseSystemMessageTask.Passphrase = passphrase.ConvertToSecureString();


				requestWalletKeyPassphraseSystemMessageTask.Completed();
			});
		}

		protected void TriggerRequestCopyWalletKeyFile(RequestCopyWalletKeyFileSystemMessageTask requestCopyWalletKeyFileSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestCopyWalletKeyFileSystemMessageTask.correlationCode, (passphrase, keysToo) => {

				requestCopyWalletKeyFileSystemMessageTask.Completed();
			});
		}

		protected void TriggerSystemEvent(CorrelationContext? correlationContext, BlockchainSystemEventType eventType, object[] parameters) {
			if(this.ChainEventRaised != null) {
				this.ChainEventRaised(correlationContext, eventType, this.CentralCoordinator.ChainId, parameters);
			}

		}

		protected void TriggerImportantWalletUpdate() {
if(			this.ImportantWalletUpdateRaised != null){		this.ImportantWalletUpdateRaised();}
		}

	#endregion

	#region dispose

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.StopChain(null).WaitAndUnwrapException();
				
				this.centralCoordinator.Dispose();

				this.DisposeOtherComponents();
				
				this.pollerResetEvent?.Dispose();

			}
			this.IsDisposed = true;
		}

		protected virtual void DisposeOtherComponents() {

		}

		~BlockChainInterface() {
			this.Dispose(false);
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	#region Task Handling

		/// <summary>
		///     Check if we received any tasks and process them
		/// </summary>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns></returns>
		protected async Task<List<Guid>> CheckTasks() {
			try {
				return await this.RoutedTaskReceiver.CheckTasks().ConfigureAwait(false);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to check and run tasks.");
			}
			return new List<Guid>();
		}

		/// <summary>
		///     interface method to receive tasks into our mailbox
		/// </summary>
		/// <param name="task"></param>
		public void ReceiveTask(IRoutedTask task) {
			this.RoutedTaskReceiver.ReceiveTask(task);
		}

		public void ReceiveTaskSynchronous(IRoutedTask task) {
			this.RoutedTaskReceiver.ReceiveTaskSynchronous(task);
		}

		public bool Synchronous {
			get => this.RoutedTaskReceiver.Synchronous;
			set => this.RoutedTaskReceiver.Synchronous = value;
		}

		public bool StashingEnabled => this.RoutedTaskReceiver.StashingEnabled;
		public ITaskRouter TaskRouter { get; }

		public Task StashTask(InternalRoutedTask task) {
			return this.RoutedTaskReceiver.StashTask(task);
		}

		public Task RestoreStashedTask(InternalRoutedTask task) {
			return this.RoutedTaskReceiver.RestoreStashedTask(task);
		}

		public Task<bool> CheckSingleTask(Guid taskId) {
			return this.RoutedTaskReceiver.CheckSingleTask(taskId);
		}

		public Task Wait() {
			return this.RoutedTaskReceiver.Wait();
		}

		public Task Wait(TimeSpan timeout) {
			return this.RoutedTaskReceiver.Wait(timeout);
		}

		public Task DispatchSelfTask(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchSelfTask(task, lockContext);
		}

		public Task DispatchTaskAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskAsync(task, lockContext);
		}

		public Task DispatchTaskNoReturnAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnAsync(task, lockContext);
		}

		public Task<bool> DispatchTaskSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskSync(task, lockContext);
		}

		public Task<bool> DispatchTaskNoReturnSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnSync(task, lockContext);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskReceiver.WaitSingleTask(task);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskReceiver.WaitSingleTask(task, timeout);
		}

		public async void ReceiveChainMessageTask(SystemMessageTask task) {
			this.ColoredRoutedTaskReceiver.ReceiveTask(task);

			// free the poller to pickup the event(s)
			this.pollerResetEvent?.Set();
		}

		public void ReceiveChainMessageTaskImmediate(SystemMessageTask task) {
			this.HandleMessages(task).WaitAndUnwrapException();
		}
		
		// handle labeledTasks
		protected virtual Task HandleMessages(IColoredTask task) {
			//TODO: review this list of events
			if(task is SystemMessageTask systemTask) {
				if(systemTask.message == BlockchainSystemEventTypes.Instance.BlockchainSyncEnded) {
					this.TriggerBlockChainSynced();
				} else if(systemTask.message == BlockchainSystemEventTypes.Instance.WalletLoadingStarted && systemTask is RequestCopyWalletSystemMessageTask loadWalletSystemTask) {
					// ok, request a load wallet
					this.TriggerRequestCopyWalletFileRequest(loadWalletSystemTask);
				} else if(systemTask.message == BlockchainSystemEventTypes.Instance.RequestWalletPassphrase && systemTask is RequestWalletPassphraseSystemMessageTask requestWalletPassphraseSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestWalletPassphrase(requestWalletPassphraseSystemMessageTask);
				} else if(systemTask.message == BlockchainSystemEventTypes.Instance.RequestKeyPassphrase && systemTask is RequestWalletKeyPassphraseSystemMessageTask requestWalletKeyPassphraseSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestWalletKeyPassphrase(requestWalletKeyPassphraseSystemMessageTask);
				} else if(systemTask.message == BlockchainSystemEventTypes.Instance.RequestCopyKeyFile && systemTask is RequestCopyWalletKeyFileSystemMessageTask requestWalletKeyCopyFileSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestCopyWalletKeyFile(requestWalletKeyCopyFileSystemMessageTask);
				}
				else if(systemTask.message == BlockchainSystemEventTypes.Instance.ImportantWalletUpdate) {
					// ok, request a load wallet
					this.TriggerImportantWalletUpdate();
				}
				
				// no matter what, lets alert of this event
				this.TriggerSystemEvent(systemTask.correlationContext, systemTask.message, systemTask.parameters);
			} 
			
			return Task.CompletedTask;
			
		}

		/// <summary>
		///     ensure the data is routed to the central coordinator's workflow manager
		/// </summary>
		/// <param name="header"></param>
		/// <param name="data"></param>
		/// <param name="connection"></param>
		/// <param name="messageSet"></param>
		public void RouteNetworkMessage(IRoutingHeader header, SafeArrayHandle data, PeerConnection connection) {
			this.centralCoordinator.RouteNetworkMessage(header, data, connection);
		}

		/// <summary>
		///     ensure the data is routed to the central coordinator's workflow manager
		/// </summary>
		/// <param name="gossipMessageSet"></param>
		/// <param name="connection"></param>
		/// <param name="messageSet"></param>
		/// <param name="data"></param>
		public void RouteNetworkGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection) {
			this.centralCoordinator.RouteNetworkGossipMessage(gossipMessageSet, connection);
		}

	#endregion

	}
}