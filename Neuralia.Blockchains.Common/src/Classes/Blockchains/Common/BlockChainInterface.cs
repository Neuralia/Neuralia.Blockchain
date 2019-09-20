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
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common {

	public interface IInterfaceSystemEventHandler {
		void ReceiveChainMessageTask(SystemMessageTask task);
	}

	public interface IBlockChainInterface : INetworkRouter, IRoutedTaskRoutingHandler, IInterfaceSystemEventHandler {

		ICentralCoordinator CentralCoordinatorBase { get; }
		bool IsMiningAllowed { get; }
		bool IsMiningEnabled { get; }
		void StartChain();

		void StopChain();

		void RunPeriodicTasks();

		void TriggerChainSynchronization();
		void TriggerChainWalletSynchronization();

		TaskResult<string> Test(string data);

		TaskResult<bool> CreateNextXmssKey(Guid accountUuid, byte ordinal);
		TaskResult<SafeArrayHandle> SignXmssMessage(Guid accountUuid, SafeArrayHandle message);
		TaskResult<long> QueryBlockHeight();
		TaskResult<object> QueryBlockChainInfo();
		List<MiningHistory> QueryMiningHistory();

		TaskResult<bool> IsWalletLoaded();
		TaskResult<ChainStatusAPI> QueryChainStatus();

		TaskResult<bool> IsBlockchainSynced();
		TaskResult<bool> IsWalletSynced();

		TaskResult<bool> SyncBlockchain(bool force);
		TaskResult<bool> SyncBlockchainExternal(string synthesizedBlock);
		TaskResult<bool> WalletExists();

		TaskResult<bool> LoadWallet(CorrelationContext correlationContext);

		TaskResult<bool> CreateNewWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, bool publishAccount);
		TaskResult<bool> CreateAccount(CorrelationContext correlationContext, string accountName, bool publishAccount, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases);

		TaskResult<bool> SetWalletPassphrase(int correlationId, int keyCorrelationCode, string passphrase);
		TaskResult<bool> SetWalletKeyPassphrase(int correlationId, int keyCorrelationCode, string passphrase);
		TaskResult<bool> WalletKeyFileCopied(int correlationId, int keyCorrelationCode);

		TaskResult<List<WalletTransactionHistoryHeaderAPI>> QueryWalletTransactionHistory(Guid accountUuid);
		TaskResult<WalletTransactionHistoryDetailsAPI> QueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId);

		TaskResult<List<WalletAccountAPI>> QueryWalletAccounts();
		TaskResult<WalletAccountDetailsAPI> QueryWalletAccountDetails(Guid accountUuid);
		TaskResult<TransactionId> QueryWalletAccountPresentationTransactionId(Guid accountUuid);

		TaskResult<bool> ChangeKey(byte changingKeyOrdinal, string note, CorrelationContext correlationContext);
		TaskResult<IBlock> LoadBlock(long blockId);

		TaskResult<bool> PresentAccountPublicly(CorrelationContext correlationContext, Guid? accountUuId);

		TaskResult<List<ElectedCandidateResultDistillate>> PerformOnDemandElection(BlockElectionDistillate blockElectionDistillate);
		TaskResult<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults);

		ImmutableList<string> QueryPeersList();
		int QueryPeerCount();

		void PrintChainDebug(string item);

		event DelegatesBase.SimpleDelegate BlockchainStarted;
		event DelegatesBase.SimpleDelegate BlockchainLoaded;
		event DelegatesBase.SimpleDelegate BlockChainSynced;
		event DelegatesBase.SimpleDelegate WalletLoadRequest;

		event Delegates.ChainEventDelegate ChainEventRaised;

		void EnableMining(AccountId delegateAccountId = null);

		void DisableMining();

		TaskResult<bool> QueryBlockchainSynced();
		TaskResult<ElectionContextAPI> QueryElectionContext(long blockId);
		TaskResult<bool> QueryWalletSynced();

		TaskResult<string> QueryBlock(long blockId);
		TaskResult<byte[]> QueryCompressedBlock(long blockId);

		TaskResult<Dictionary<TransactionId, byte[]>> QueryBlockBinaryTransactions(long blockId);

		TaskResult<object> BackupWallet();

		TaskResult<bool> CreateNewAccount(CorrelationContext correlationContext, string name, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases);
		TaskResult<bool> SetActiveAccount(string name);
		TaskResult<bool> SetActiveAccount(Guid accountUuid);
	}

	public interface IBlockChainInterface<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockChainInterface
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		CENTRAL_COORDINATOR CentralCoordinator { get; }
	}

	public abstract class BlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockChainInterface<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IDisposable2, IRoutedTaskRoutingHandler
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
		private bool poller_active = true;

		private AutoResetEvent pollerResetEvent;

		protected BlockChainInterface(CENTRAL_COORDINATOR centralCoordinator) {
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

		public void EnableMining(AccountId delegateAccountId = null) {
			this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.EnableMining(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetPublicAccountId(), delegateAccountId);
		}

		public void DisableMining() {
			this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.DisableMining();
		}

		public bool IsMiningAllowed => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAllowed;
		public bool IsMiningEnabled => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningEnabled;

		public event DelegatesBase.SimpleDelegate BlockchainStarted;
		public event DelegatesBase.SimpleDelegate BlockchainLoaded;
		public event DelegatesBase.SimpleDelegate BlockChainSynced;
		public event DelegatesBase.SimpleDelegate WalletLoadRequest;

		public event Delegates.ChainEventDelegate ChainEventRaised;

		// start our pletora of threads
		public virtual void StartChain() {
			try {

				Log.Information($"Starting blockchain {this.centralCoordinator.ChainName} with chain path: {this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainDirectoryPath()}");

				// if anything was running, we stop it
				this.StopChain();

				this.centralCoordinator.Start();

				this.StartOtherChainComponents();

				if(GlobalSettings.ApplicationSettings.P2pEnabled) {
					// if p2p is enabled, then we register our chain

					this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterChain(this);
				}

				this.IsChainStarted = true;

				this.TriggerBlockchainStarted();

				// we are ready and have started
				this.TriggerBlockchainLoaded();

				// start polling for system events
				this.StartEventsPoller();

			} catch(Exception ex) {
				Log.Error(ex, "Failed to start controllers");

				throw ex;
			}
		}

		public virtual void StopChain() {
			if(!this.centralCoordinator.IsCompleted && this.centralCoordinator.IsStarted) {
				try {
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemovePIDLock();

					this.poller_active = false;
					this.centralCoordinator.Stop();
				} catch(Exception ex) {
					Log.Error(ex, "Failed to stop controllers");

					throw ex;
				}
			}

			this.StopOtherChainComponents();

			this.IsChainStarted = false;
		}

		public void TriggerChainSynchronization() {
			this.RunBlockchainTaskMethod((service, taskRoutingContext) => {

				service.SynchronizeBlockchain(true);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public void TriggerChainWalletSynchronization() {

			this.RunBlockchainTaskMethod((service, taskRoutingContext) => {

				service.SynchronizeWallet(true, true);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		private void StartEventsPoller() {

			this.pollerResetEvent = new AutoResetEvent(false);

			// here we prepare the connection poller that will periodically check connections to ensure they are still active
			this.eventsPoller = new Task<bool>(() => {

				Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
				while(this.poller_active) {

					try {
						this.ColoredRoutedTaskReceiver.CheckTasks();
					} catch(Exception ex) {
						Log.Error(ex, "Failed to check for system events");
					}

					this.pollerResetEvent.WaitOne(TimeSpan.FromSeconds(5));
				}

				return true;
			});

			this.poller_active = true;
			this.eventsPoller.Start();
		}

		protected virtual void StartOtherChainComponents() {

		}

		protected virtual void StopOtherChainComponents() {

		}

	#region Interface Methods

		public abstract void PrintChainDebug(string item);

		public TaskResult<string> Test(string data) {
			return this.RunSerializationTaskMethod((service, taskRoutingContext) => {

				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ConnecableChanged(true));

				return "";

			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> SetWalletPassphrase(int correlationId, int keyCorrelationCode, string passphrase) {

			return this.RunTaskMethod(() => {

				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
					this.keyQueries[keyCorrelationCode]?.Invoke(passphrase);
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			});
		}

		public TaskResult<bool> SetWalletKeyPassphrase(int correlationId, int keyCorrelationCode, string passphrase) {

			return this.RunTaskMethod(() => {

				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
					this.keyQueries[keyCorrelationCode]?.Invoke(passphrase);
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			});
		}

		public TaskResult<bool> WalletKeyFileCopied(int correlationId, int keyCorrelationCode) {
			return this.RunTaskMethod(() => {
				if(this.keyQueries.ContainsKey(keyCorrelationCode)) {
					this.keyQueries[keyCorrelationCode]?.Invoke(null);
					this.keyQueries.Remove(keyCorrelationCode);
				}

				return true;
			});
		}

		public TaskResult<bool> CreateNextXmssKey(Guid accountUuid, byte ordinal) {

			return this.RunTaskMethod(() => {

				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNextXmssKey(accountUuid, ordinal);

				return true;
			});
		}

		public TaskResult<SafeArrayHandle> SignXmssMessage(Guid accountUuid, SafeArrayHandle message) {
			return this.RunTaskMethod(() => {

				return this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(accountUuid, message);
			});
		}

		/// <summary>
		///     Query the current blockchain height
		/// </summary>
		/// <returns></returns>
		public TaskResult<long> QueryBlockHeight() {

			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => service.GetBlockHeight(), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<object> QueryBlockChainInfo() {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => (object) service.GetBlockchainInfo(), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public List<MiningHistory> QueryMiningHistory() {
			return this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.GetMiningHistory().Select(e => e.ToApiHistory()).ToList();
		}

		public int QueryPeerCount() {
			this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.Test();

			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount;

		}

		public ImmutableList<string> QueryPeersList() {
			return this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AllConnectionsList.Select(c => c.ScoppedIp).ToImmutableList();
		}

		/// <summary>
		///     Query various basic status variables about the chain and it's wallet
		/// </summary>
		/// <returns></returns>
		public TaskResult<ChainStatusAPI> QueryChainStatus() {

			return this.RunTaskMethod(() => {
				IWalletProviderProxy walletProvider = this.centralCoordinator.ChainComponentProvider.WalletProviderBase;

				BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
				int minimumDispatchPeerCount = chainConfiguration.MinimumDispatchPeerCount;

				return new ChainStatusAPI(){
					WalletExists = walletProvider.WalletFileExists, IsWalletLoaded = walletProvider.IsWalletLoaded, WalletEncrypted = walletProvider.IsWalletEncrypted, WalletPath = walletProvider.GetChainDirectoryPath(),
					MinRequiredPeerCount = minimumDispatchPeerCount
				};
			});
		}

		public TaskResult<bool> IsBlockchainSynced() {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => service.BlockchainSynced, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> IsWalletSynced() {

			return this.QueryWalletSynced();
		}

		public TaskResult<bool> SyncBlockchain(bool force) {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => {

				service.SynchronizeBlockchain(force);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> SyncBlockchainExternal(string synthesizedBlock) {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => {

				service.SynchronizeBlockchainExternal(synthesizedBlock);

				return true;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> IsWalletLoaded() {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded);

		}

		public TaskResult<bool> WalletExists() {
			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.WalletFileExists);

		}

		public TaskResult<bool> LoadWallet(CorrelationContext correlationContext) {

			return this.RunTaskMethod(() => {

				ILoadWalletWorkflow workflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateLoadWalletWorkflow();

				this.centralCoordinator.PostWorkflow(workflow);

				return true;
			});
		}

		public TaskResult<bool> CreateNewWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, bool publishAccount) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteWallet(correlationContext, accountName, encryptWallet, encryptKey, encryptKeysIndividually, passphrases));
		}

		public TaskResult<bool> CreateAccount(CorrelationContext correlationContext, string accountName, bool publishAccount, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases));

		}

		public TaskResult<List<WalletTransactionHistoryHeaderAPI>> QueryWalletTransactionHistory(Guid accountUuid) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletTransactionHistory(accountUuid));
		}

		public TaskResult<WalletTransactionHistoryDetailsAPI> QueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletTransationHistoryDetails(accountUuid, transactionId));
		}

		public TaskResult<List<WalletAccountAPI>> QueryWalletAccounts() {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccounts());
		}

		public TaskResult<WalletAccountDetailsAPI> QueryWalletAccountDetails(Guid accountUuid) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccountDetails(accountUuid));
		}

		public TaskResult<TransactionId> QueryWalletAccountPresentationTransactionId(Guid accountUuid) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.APIQueryWalletAccountPresentationTransactionId(accountUuid));
		}

		public TaskResult<IBlock> LoadBlock(long blockId) {

			return this.RunSerializationTaskMethod((service, taskRoutingContext) => service.LoadBlock(new BlockId(blockId)), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<string> QueryBlock(long blockId) {
			return this.RunSerializationTaskMethod((service, taskRoutingContext) => {

				string json = service.LoadBlockJson(new BlockId(blockId));

				return json;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<byte[]> QueryCompressedBlock(long blockId) {
			return this.RunSerializationTaskMethod((service, taskRoutingContext) => {

				string json = service.LoadBlockJson(new BlockId(blockId));

				GzipCompression compressor = new GzipCompression();

				using(ByteArray simpleBytes = Encoding.UTF8.GetBytes(json)) {

					using(SafeArrayHandle compressed = compressor.Compress(simpleBytes)) {
						return compressed.ToExactByteArrayCopy();
					}

				}
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<Dictionary<TransactionId, byte[]>> QueryBlockBinaryTransactions(long blockId) {
			return this.RunSerializationTaskMethod((service, taskRoutingContext) => {

				IBlock block = service.LoadBlock(new BlockId(blockId));

				if(block != null) {
					GzipCompression compressor = new GzipCompression();

					return block.GetAllConfirmedTransactions().Select(t => {

						// now dehydrate each transaction into a byte array
						IDehydratedTransaction dehydratedTransaction = t.Value.Dehydrate(BlockChannelUtils.BlockChannelTypes.All);

						IDataDehydrator rehydrator = DataSerializationFactory.CreateDehydrator();
						dehydratedTransaction.Dehydrate(rehydrator);

						SafeArrayHandle bytes = rehydrator.ToArray();
						SafeArrayHandle compressed = compressor.Compress(bytes);
						
						var data = compressed.ToExactByteArrayCopy();

						compressed.Return();
						bytes.Return();

						return new {data, t.Key};
					}).ToDictionary(e => e.Key, e => e.data);
				}

				return new Dictionary<TransactionId, byte[]>();
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}

			});

		}

		public TaskResult<bool> PresentAccountPublicly(CorrelationContext correlationContext, Guid? accountUuId) {

			return this.RunTaskMethod(() => {

				AutoResetEvent resetEvent = new AutoResetEvent(false);

				var workflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreatePresentationTransactionChainWorkflow(correlationContext, accountUuId);

				workflow.Success += w => {
					resetEvent.Set();
				};

				this.centralCoordinator.PostWorkflow(workflow);

				resetEvent.WaitOne();

				return true;
			});
		}

		public TaskResult<bool> CreateNewAccount(CorrelationContext correlationContext, string name, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {

			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewCompleteAccount(correlationContext, name, encryptKeys, encryptKeysIndividually, passphrases, null));
		}

		public TaskResult<bool> SetActiveAccount(string name) {
			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetActiveAccount(name));
		}

		public TaskResult<bool> SetActiveAccount(Guid accountUuid) {
			return this.RunTaskMethod(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetActiveAccount(accountUuid));

		}

		public TaskResult<bool> QueryBlockchainSynced() {

			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => service.BlockchainSynced, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> QueryWalletSynced() {

			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => {

				var result = service.WalletSyncedNoWait;

				return result ?? false;
			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<object> BackupWallet() {

			return this.RunTaskMethod(() => {
				(string path, string passphrase, string salt, int iterations) results = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.BackupWallet();

				return (object) new WalletBackupAPI(){Path = results.path, Passphrase = results.passphrase, Salt = results.salt, Iterations = results.iterations};
			});
		}

		public TaskResult<List<ElectedCandidateResultDistillate>> PerformOnDemandElection(BlockElectionDistillate blockElectionDistillate) {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => service.PerformElectionComputation(blockElectionDistillate), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults) {
			return this.RunBlockchainTaskMethod((service, taskRoutingContext) => service.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults), (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<ElectionContextAPI> QueryElectionContext(long blockId) {
			return this.RunSerializationTaskMethod((service, taskRoutingContext) => {

				IBlock block = service.LoadBlock(blockId);

				if(block is IElectionBlock electionBlock) {

					GzipCompression compressor = new GzipCompression();

					using(SafeArrayHandle compressed = compressor.Compress(electionBlock.DehydratedElectionContext)) {

						return new ElectionContextAPI() {
							Type = electionBlock.ElectionContext.Version.Type.Value.Value, ContextBytes = compressed.ToExactByteArrayCopy(), BlockId = blockId, MaturityId = blockId + electionBlock.ElectionContext.Maturity,
							PublishId = blockId + electionBlock.ElectionContext.Maturity + electionBlock.ElectionContext.Publication
						};
					}
				}

				return new ElectionContextAPI() {Type = 0};

			}, (results, taskRoutingContext) => {
				if(results.Error) {
					//TODO: what to do here?
				}
			});
		}

		public TaskResult<bool> ChangeKey(byte changingKeyOrdinal, string note, CorrelationContext correlationContext) {

			return this.RunTaskMethod(() => {
				AutoResetEvent resetEvent = new AutoResetEvent(false);
				var workflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changingKeyOrdinal, note, correlationContext);

				workflow.Success += w => {
					resetEvent.Set();
				};

				this.centralCoordinator.PostWorkflow(workflow);

				resetEvent.WaitOne();

				return true;
			});
		}

	#endregion

	#region System Methods

		/// <summary>
		///     perform a check for any message that has arrived and invoke the callbacks if there are any on the calling thread
		/// </summary>
		public void RunPeriodicTasks() {
			try {
				this.CheckTasks();

			} catch(Exception ex) {
				Log.Error(ex, "Failed to check for events");
			}

		}

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

				AutoResetEvent autoResetEvent = new AutoResetEvent(false);

				T Runner() {

					ct.ThrowIfCancellationRequested();

					autoResetEvent.WaitOne();

					return message.Results;
				}

				// send the message
				this.DispatchTaskAsync(message);

				if(((InternalRoutedTask) message).RoutingStatus == RoutedTask.RoutingStatuses.Disposed) {
					// ok, seems it was executed in thread and tis all done. we can return now
					results.awaitableTask = new TaskFactory<T>().StartNew(() => message.Results, ct);

					return results;
				}

				results.awaitableTask = new Task<T>(Runner, ct);

				// handle exceptions here
				results.task = results.awaitableTask.WithAllExceptions().ContinueWith(t => {

					Log.Error(t.Exception, "Failed to query blockchain");
					results.TriggerError(t.Exception);

				}, ct, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

				message.OnCompleted += () => {
					this.returnedQueries.AddSafe(message.Id, message);

					results.TriggerCompleted(message.Results);
					autoResetEvent.Set();
				};

				// and start listening for the response
				results.awaitableTask.Start();

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

		public TaskResult<T> RunBlockchainTaskMethod<T>(Func<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, TaskRoutingContext, T> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<T>();

			blockchainTask.SetAction((service, taskRoutingContext) => {

				blockchainTask.Results = newAction(service, taskRoutingContext);

			}, newCompleted);

			return this.RunMethod(blockchainTask);
		}

		public TaskResult<T> RunSerializationTaskMethod<T>(Func<ISerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, TaskRoutingContext, T> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			var serializationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<T>();

			serializationTask.SetAction((service, taskRoutingContext) => {

				serializationTask.Results = newAction(service, taskRoutingContext);

			}, newCompleted);

			return this.RunMethod(serializationTask);
		}

		public TaskResult<T> RunTaskMethod<T>(Func<T> runner) {
			var results = new TaskResult<T>();

			results.awaitableTask = new Task<T>(runner);

			// handle exceptions here
			results.task = results.awaitableTask.WithAllExceptions().ContinueWith(t => {

			}, TaskContinuationOptions.OnlyOnFaulted);

			// and start listening for the response
			results.awaitableTask.Start();

			return results;
		}

	#endregion

	#region event triggers

		public void TriggerBlockchainStarted() {
			this.BlockchainStarted?.Invoke();
		}

		public void TriggerBlockchainLoaded() {
			this.BlockchainLoaded?.Invoke();
		}

		public void TriggerBlockChainSynced() {
			this.BlockChainSynced?.Invoke();
		}

		protected void TriggerLoadWalletRequest(LoadWalletSystemMessageTask loadWalletSystemTask) {
			this.WalletLoadRequest?.Invoke();

			loadWalletSystemTask.Completed();
		}

		private readonly Dictionary<int, Action<string>> keyQueries = new Dictionary<int, Action<string>>();

		protected async void TriggerRequestWalletPassphrase(RequestWalletPassphraseSystemMessageTask requestWalletPassphraseSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestWalletPassphraseSystemMessageTask.correlationCode, passphrase => {

				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new ApplicationException("Invalid passphrase");
				}

				requestWalletPassphraseSystemMessageTask.Passphrase = passphrase.ConvertToSecureString();

				requestWalletPassphraseSystemMessageTask.Completed();
			});
		}

		protected async void TriggerRequestWalletKeyPassphrase(RequestWalletKeyPassphraseSystemMessageTask requestWalletKeyPassphraseSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestWalletKeyPassphraseSystemMessageTask.correlationCode, passphrase => {

				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new ApplicationException("Invalid key passphrase");
				}

				requestWalletKeyPassphraseSystemMessageTask.Passphrase = passphrase.ConvertToSecureString();

				requestWalletKeyPassphraseSystemMessageTask.Completed();
			});
		}

		protected async void TriggerRequestWalletKeyCopyFile(RequestWalletKeyCopyFileSystemMessageTask requestWalletKeyCopyFileSystemMessageTask) {

			// store it for a callback
			this.keyQueries.Add(requestWalletKeyCopyFileSystemMessageTask.correlationCode, passphrase => {

				requestWalletKeyCopyFileSystemMessageTask.Completed();
			});
		}

		protected void TriggerSystemEvent(CorrelationContext correlationContext, BlockchainSystemEventType eventType, object[] parameters) {
			this.ChainEventRaised?.Invoke(correlationContext, eventType, this.CentralCoordinator.ChainId, parameters);

		}

	#endregion

	#region dispose

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					this.StopChain();
				} finally {
					this.IsDisposed = true;
				}

				try {
					this.centralCoordinator.Dispose();

					this.DisposeOtherComponents();
				} finally {
					this.IsDisposed = true;
				}
			}
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
		protected List<Guid> CheckTasks() {
			return this.RoutedTaskReceiver.CheckTasks();
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

		public void StashTask(InternalRoutedTask task) {
			this.RoutedTaskReceiver.StashTask(task);
		}

		public void RestoreStashedTask(InternalRoutedTask task) {
			this.RoutedTaskReceiver.RestoreStashedTask(task);
		}

		public bool CheckSingleTask(Guid taskId) {
			return this.RoutedTaskReceiver.CheckSingleTask(taskId);
		}

		public void Wait() {
			this.RoutedTaskReceiver.Wait();
		}

		public void Wait(TimeSpan timeout) {
			this.RoutedTaskReceiver.Wait(timeout);
		}

		public void DispatchSelfTask(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchSelfTask(task);
		}

		public void DispatchTaskAsync(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchTaskAsync(task);
		}

		public void DispatchTaskNoReturnAsync(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchTaskNoReturnAsync(task);
		}

		public bool DispatchTaskSync(IRoutedTask task) {
			return this.RoutedTaskReceiver.DispatchTaskSync(task);
		}

		public bool DispatchTaskNoReturnSync(IRoutedTask task) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnSync(task);
		}

		public bool WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskReceiver.WaitSingleTask(task);
		}

		public bool WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskReceiver.WaitSingleTask(task, timeout);
		}

		public void ReceiveChainMessageTask(SystemMessageTask task) {
			this.ColoredRoutedTaskReceiver.ReceiveTask(task);

			// free the poller to pickup the event(s)
			this.pollerResetEvent?.Set();
		}

		// handle labeledTasks
		protected virtual void HandleMessages(IColoredTask task) {
			//TODO: review this list of events
			if(task is SystemMessageTask systemTask) {
				if(systemTask.message == BlockchainSystemEventTypes.Instance.BlockchainSyncEnded) {
					this.TriggerBlockChainSynced();
				} else if((systemTask.message == BlockchainSystemEventTypes.Instance.WalletLoadingStarted) && systemTask is LoadWalletSystemMessageTask loadWalletSystemTask) {
					// ok, request a load wallet
					this.TriggerLoadWalletRequest(loadWalletSystemTask);
				} else if((systemTask.message == BlockchainSystemEventTypes.Instance.RequestWalletPassphrase) && systemTask is RequestWalletPassphraseSystemMessageTask requestWalletPassphraseSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestWalletPassphrase(requestWalletPassphraseSystemMessageTask);
				} else if((systemTask.message == BlockchainSystemEventTypes.Instance.RequestKeyPassphrase) && systemTask is RequestWalletKeyPassphraseSystemMessageTask requestWalletKeyPassphraseSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestWalletKeyPassphrase(requestWalletKeyPassphraseSystemMessageTask);
				} else if((systemTask.message == BlockchainSystemEventTypes.Instance.RequestCopyKeyFile) && systemTask is RequestWalletKeyCopyFileSystemMessageTask requestWalletKeyCopyFileSystemMessageTask) {
					// ok, request a load wallet
					this.TriggerRequestWalletKeyCopyFile(requestWalletKeyCopyFileSystemMessageTask);
				}

				// no matter what, lets alert of this event
				this.TriggerSystemEvent(systemTask.correlationContext, systemTask.message, systemTask.parameters);
			}
		}

		/// <summary>
		///     ensure the data is routed to the central coordinator's workflow manager
		/// </summary>
		/// <param name="messageSet"></param>
		/// <param name="data"></param>
		/// <param name="connection"></param>
		public void RouteNetworkMessage(IRoutingHeader header, SafeArrayHandle data, PeerConnection connection) {
			this.centralCoordinator.RouteNetworkMessage(header, data, connection);
		}

		/// <summary>
		///     ensure the data is routed to the central coordinator's workflow manager
		/// </summary>
		/// <param name="messageSet"></param>
		/// <param name="data"></param>
		/// <param name="connection"></param>
		public void RouteNetworkGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection) {
			this.centralCoordinator.RouteNetworkGossipMessage(gossipMessageSet, connection);
		}

	#endregion

	}
}