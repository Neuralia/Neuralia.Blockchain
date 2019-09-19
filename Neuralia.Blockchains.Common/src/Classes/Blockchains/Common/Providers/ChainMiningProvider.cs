using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainMiningStatusProvider {

		// are we mining currently?  (this is not saved to chain state. we start fresh every time we load the app)
		bool MiningEnabled { get; }

		bool MiningAllowed { get; }

		List<MiningHistoryEntry> GetMiningHistory();

		BlockElectionDistillate PrepareBlockElectionContext(IBlock currentBlock, AccountId miningAccountId);
		void RehydrateBlockElectionContext(BlockElectionDistillate blockElectionDistillate);
	}

	public interface IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainMiningStatusProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ElectionProcessorFactory { get; }

		void EnableMining(AccountId miningAccountId, AccountId delegateAccountId);

		void DisableMining();

		List<IElectionCandidacyMessage> PerformElection(IBlock currentBlock, IEventPoolProvider chainEventPoolProvider);
		List<ElectedCandidateResultDistillate> PerformElectionComputations(BlockElectionDistillate blockElectionDistillate, IEventPoolProvider chainEventPoolProvider);
		List<IElectionCandidacyMessage> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, IEventPoolProvider chainEventPoolProvider);
	}

	public class MiningHistoryEntry {
		public readonly List<TransactionId> selectedTransactions = new List<TransactionId>();

		public BlockId blockId { get; set; }

		public virtual MiningHistory ToApiHistory() {
			MiningHistory miningHistory = this.CreateApiMiningHistory();

			miningHistory.blockId = this.blockId.Value;
			miningHistory.selectedTransactions.AddRange(this.selectedTransactions.Select(t => t.ToString()));

			return miningHistory;
		}

		public virtual MiningHistory CreateApiMiningHistory() {
			return new MiningHistory();
		}
	}

	/// <summary>
	///     A provider that maintains the required state for mining operations
	/// </summary>
	/// <typeparam name="CHAIN_STATE_DAL"></typeparam>
	/// <typeparam name="CHAIN_STATE_CONTEXT"></typeparam>
	/// <typeparam name="CHAIN_STATE_ENTRY"></typeparam>
	public abstract class ChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     how long to wait
		/// </summary>
		private const int SEND_WORKFLOW_TIMEOUT = 30;

		private const int UPDATE_MINING_REGISTRATION_DELAY = 60;
		private const int UPDATE_MINING_STATUS_DELAY = 10;
		private const int UPDATE_MINING_STATUS_START_DELAY = 3;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		/// <summary>
		///     Here we store the elections that have yet to come to maturity
		/// </summary>
		protected readonly Dictionary<long, List<BlockElectionDistillate>> electionBlockCache = new Dictionary<long, List<BlockElectionDistillate>>();

		protected readonly IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> factory;

		protected readonly xxHasher32 hasher = new xxHasher32();
		private readonly object historyLocker = new object();

		private readonly object locker = new object();

		protected readonly Queue<MiningHistoryEntry> MiningHistory = new Queue<MiningHistoryEntry>();

		protected readonly ITimeService timeService;
		protected bool miningEnabled;

		protected BlockchainNetworkingService.MiningRegistrationParameters registrationParameters;
		
		private Timer updateMiningRegistrationTimer;

		private Timer updateMiningStatusTimer;

		public ChainMiningProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;

			this.factory = this.GetElectionProcessorFactory();
		}

		protected BlockChainConfigurations ChainConfiguration => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

		public AccountId MiningAccountId { get; private set; }

		/// <summary>
		///     Is mining allowed on this chain
		/// </summary>
		public abstract bool MiningAllowed { get; }

		/// <summary>
		///     This is NOT saved to the filesystem. we hold it in memory and we start fresh every time we load the daemon
		/// </summary>
		public bool MiningEnabled {
			get {
				if(!this.MiningAllowed) {
					return false;
				}

				return this.miningEnabled;
			}
		}

		public IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ElectionProcessorFactory => this.factory;

		public virtual void EnableMining(AccountId miningAccountId, AccountId delegateAccountId) {

			if(miningAccountId == null) {
				string message = "Failed to mine. A mining account must be selected.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			if(!this.centralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
				string message = "Failed to mine. A wallet must be loaded to mine.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			IWalletAccount walletMiningAccount = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(miningAccountId);

			if(walletMiningAccount.Status != Enums.PublicationStatus.Published) {
				string message = "Failed to mine. The mining account has not yet been fully published. Mining is not yet possible until the account is presented and confirmed on the blockchain.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
				string message = "Failed to mine. Your must be connected to some peers to mine.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			if(!this.centralCoordinator.IsChainLikelySynchronized) {

				this.WaitForSync();
			}

			this.MiningAccountId = miningAccountId;
			this.miningEnabled = true;

			try {
				if(this.MiningEnabled) {

					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.MiningStarted);

					Log.Information("Mining is now enabled.");

					// create the elector cache wallet file
					IWalletAccount miningAccount = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(this.MiningAccountId);
					this.centralCoordinator.ChainComponentProvider.WalletProviderBase.CreateElectionCacheWalletFile(miningAccount);

					// register the chain in the network service, so we can answer the IP Validator

					BlockchainNetworkingService.MiningRegistrationParameters localRegistrationParameters = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterMiningRegistrationParameters();

					localRegistrationParameters.AccountId = miningAccountId;
					localRegistrationParameters.DelegateAccountId = delegateAccountId;

					// here we can reuse our existing password if its not too old
					if(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate >= (DateTime.Now - TimeSpan.FromMinutes(UPDATE_MINING_REGISTRATION_DELAY * 2))) {
						localRegistrationParameters.Password = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.MiningPassword;
					} else {
						// generate our random password
						localRegistrationParameters.Password = GlobalRandom.GetNextLong();
					}

					if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.DisableMiningRegistration) {

						// ok, now we must register for mining.  if we can, we will try the rest api first, its so much simpler. if we can't, we will publish a message on chain
						ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo(localRegistrationParameters);

						bool success = false;
						bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web);
						bool chain = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Gossip);

						// ok, well this is it, we will register for mining on chain
						if((this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp == null) || string.IsNullOrWhiteSpace(electionsCandidateRegistrationInfo.Ip)) {
							throw new ApplicationException("Our public IP is still undefined. We can not register for mining on chain without an IP address to provide.");
						}
						
						if(web) {
							try {
								this.PerformWebapiRegistration(electionsCandidateRegistrationInfo);

								this.registrationParameters = localRegistrationParameters;

								this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.MiningPassword = this.registrationParameters.Password;
								this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate = DateTime.Now;

								// start our account update service
								this.StartAccountUpdateController();
								this.StartMiningStatusUpdateCheck();

								success = true;
							} catch(Exception ex) {

								this.registrationParameters = null;

								if(chain) {
									// do not raise an exception, we will try to mine on chain
									Log.Error(ex, "Failed to register for mining by webapi.");
								} else {
									throw new ApplicationException("Failed to register for mining by webapi.", ex);
								}
							}
						}
						if(chain && !success) {
							// ok, well this is it, we will register for mining on chain
							if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp == null || string.IsNullOrWhiteSpace(electionsCandidateRegistrationInfo.Ip)) {
								throw new ApplicationException("Our public IP is still undefined. We can not register for mining on chain without an IP address to provide.");
							}

							try {
								this.registrationParameters = null;
								this.PerformOnchainRegistration(electionsCandidateRegistrationInfo);
								success = true;
							} catch(Exception ex) {
								throw new ApplicationException("Failed to register for mining by on chain message.", ex);
							}
						}
					}
				} else {
					throw new ApplicationException();
				}
			} catch(Exception ex) {
				Log.Error(ex, "Mining is disabled. Impossible to enable mining.");
				this.DisableMining();
			}

		}

		public virtual void DisableMining() {
			this.miningEnabled = false;

			this.registrationParameters = null;

			this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.MiningEnded);

			Log.Information("Mining is now disabled.");

			// delete the file from the wallet
			IWalletAccount miningAccount = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(this.MiningAccountId);
			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteElectionCacheWalletFile(miningAccount);

			// remove our network registration
			this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.UnRegisterMiningRegistrationParameters();

		}

		public virtual List<MiningHistoryEntry> GetMiningHistory() {
			lock(this.historyLocker) {
				return this.MiningHistory.ToList();
			}
		}

		protected void StartAccountUpdateController() {

			if(this.updateMiningRegistrationTimer == null) {
				TimeSpan waitTime = TimeSpan.FromMinutes(UPDATE_MINING_REGISTRATION_DELAY);

				this.updateMiningRegistrationTimer = new Timer(state => {

					this.UpdateWebapiAccountRegistration();

				}, this, waitTime, waitTime);
			}
		}

		protected void StartMiningStatusUpdateCheck() {
			if(this.updateMiningStatusTimer == null && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableMiningStatusChecks) {

				this.updateMiningStatusTimer = new Timer(state => {

					this.CheckMiningStatus();

				}, this, TimeSpan.FromMinutes(UPDATE_MINING_STATUS_START_DELAY), TimeSpan.FromMinutes(UPDATE_MINING_STATUS_DELAY));
			}
			
		}

		protected void ResetAccountUpdateController() {
			if(this.StopAccountUpdateController()) {
				this.StartAccountUpdateController();
				this.StartMiningStatusUpdateCheck();
			}
		}

		protected bool StopAccountUpdateController() {
			bool wasRunning = this.updateMiningRegistrationTimer != null;
			this.updateMiningRegistrationTimer?.Dispose();
			this.updateMiningRegistrationTimer = null;

			this.updateMiningStatusTimer?.Dispose();
			this.updateMiningStatusTimer = null;

			return wasRunning;
		}
		
		private void WaitForSync() {
			AutoResetEvent resetEvent = new AutoResetEvent(false);

			void Catcher() {
				resetEvent.Set();
			}

			this.centralCoordinator.BlockchainSynced += Catcher;

			try {
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction((service, taskRoutingContext2) => {
					service.SynchronizeBlockchain(false);
				});

				blockchainTask.Caller = null;
				this.centralCoordinator.RouteTask(blockchainTask);

				if(!resetEvent.WaitOne(TimeSpan.FromSeconds(10))) {

					string message = "Failed to mine. Your blockchain must be synchronized to mine.";
					Log.Error(message);

					throw new ApplicationException(message);
				}
			} finally {
				this.centralCoordinator.BlockchainSynced -= Catcher;
			}
		}

		/// <summary>
		///     this method allows to check if its time to act, or if we should sleep more
		/// </summary>
		/// <returns></returns>
		private bool ShouldAct(ref DateTime? action) {
			if(!action.HasValue) {
				return true;
			}

			if(action.Value < DateTime.Now) {
				action = null;

				return true;
			}

			return false;
		}

		protected ElectionsCandidateRegistrationInfo PrepareRegistrationInfo(BlockchainNetworkingService.MiningRegistrationParameters registrationParameters) {
			ElectionsCandidateRegistrationInfo info = new ElectionsCandidateRegistrationInfo();

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp != null) {
				NodeAddressInfo node = new NodeAddressInfo(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp, GlobalSettings.ApplicationSettings.port, Enums.PeerTypes.Unknown);
				info.Ip = node.Ip; // always as IpV6
			}

			info.Port = GlobalSettings.ApplicationSettings.port;

			info.AccountId = registrationParameters.AccountId;
			info.DelegateAccountId = registrationParameters.DelegateAccountId;

			info.ChainType = this.centralCoordinator.ChainId;
			info.Password = registrationParameters.Password;

			info.Timestamp = this.timeService.CurrentRealTime;

			return info;
		}
		
		/// <summary>
		///     upudate our registration to keep it active by sending our information again, with the very same password.
		///     Chain miners dont need to do this, since they will be contacted directly
		/// </summary>
		protected void UpdateWebapiAccountRegistration() {
			if(this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web) && (this.registrationParameters != null)) {
				try {
					ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo(this.registrationParameters);
					this.PerformWebapiRegistrationUpdate(electionsCandidateRegistrationInfo);

					this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate = DateTime.Now;

				} catch(Exception ex) {
					Log.Error(ex, "Failed to update mining registration by webapi.");
					this.DisableMining();
				}
			}
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected void PerformWebapiRegistrationUpdate(ElectionsCandidateRegistrationInfo registrationInfo) {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

			Repeater.Repeat(() => {
				string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add("accountId", registrationInfo.AccountId.ToCompactString());
				parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToCompactString());
				parameters.Add("password", registrationInfo.Password);
				
				var result = restUtility.Put(url, "registration/update", parameters);
				result.Wait();

				if(!result.IsFaulted) {

					// ok, check the result
					if(result.Result.StatusCode == HttpStatusCode.OK) {
						// ok, we are not registered. we can await a response from the IP Validator
						return;
					}
				}

				throw new ApplicationException("Failed to register for mining through web");

			});
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected void PerformWebapiRegistration(ElectionsCandidateRegistrationInfo registrationInfo) {

			using(IXmssWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(GlobalsService.MESSAGE_KEY_NAME)) {

				// and sign the whole thing with our key
				SafeArrayHandle password = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(registrationInfo.Password, password.Span);
				var autograph = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(password, key);
				registrationInfo.Autograph = autograph.ToExactByteArrayCopy();
				autograph.Return();

				Log.Verbose("Message successfully signed.");
			}

			try {
				RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

				if(registrationInfo.Autograph != null) {

					Repeater.Repeat(() => {

						string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

						Dictionary<string, object> parameters = new Dictionary<string, object>();
						parameters.Add("accountId", registrationInfo.AccountId.ToCompactString());
						parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToCompactString());
						parameters.Add("password", registrationInfo.Password);
						parameters.Add("autograph", registrationInfo.Autograph);

						var result = restUtility.Put(url, "registration/register", parameters);
						result.Wait();

						if(!result.IsFaulted) {

							// ok, check the result
							if(result.Result.StatusCode == HttpStatusCode.OK) {
								// ok, we are not registered. we can await a response from the IP Validator
								return;
							}
						}

						throw new ApplicationException("Failed to register for mining through web");

					});

				} else {
					throw new ApplicationException();
				}

			} catch(Exception ex) {
				Log.Error(ex, "Failed to register for web mining.");
				this.DisableMining();
			}
		}

		protected void CheckMiningStatus() {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

			Repeater.Repeat(() => {
				string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

				ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo(this.registrationParameters);

				var parameters = new Dictionary<string, object>();
				parameters.Add("accountId", electionsCandidateRegistrationInfo.AccountId.ToCompactString());
				parameters.Add("password", electionsCandidateRegistrationInfo.Password);
				
				var result = restUtility.Post(url, "registration/query-mining-status", parameters);
				result.Wait();

				// ok, check the result
				if(!result.IsFaulted && result.Result.StatusCode == HttpStatusCode.OK && bool.TryParse(result.Result.Content, out bool status)) {

					if(!status) {
						Log.Information("A status check demonstrated that we are not mining.");
						this.DisableMining();
					} else {
						// all is fine, we are confirmed as mining.
					}
				} else {
					Log.Warning("We could not verify if we are registered for mining. We might be, but we could not verify it.");
				}
			});
		}
		
		/// <summary>
		///     je dois reviser
		///     register through an onchain message
		/// </summary>
		protected void PerformOnchainRegistration(ElectionsCandidateRegistrationInfo registrationInfo) {
			this.PerformMiningregistration(registrationInfo, ChainConfigurations.RegistrationMethods.Gossip);
		}

		protected void PerformMiningregistration(ElectionsCandidateRegistrationInfo registrationInfo, ChainConfigurations.RegistrationMethods registrationMethod) {
			var sendWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateSendElectionsCandidateRegistrationMessageWorkflow(this.MiningAccountId, registrationInfo, registrationMethod, new CorrelationContext());

			this.centralCoordinator.PostImmediateWorkflow(sendWorkflow);

			sendWorkflow.Task.Wait(TimeSpan.FromSeconds(SEND_WORKFLOW_TIMEOUT));

			if(!sendWorkflow.IsCompleted) {
				// its taking too much time
				throw new ApplicationException("Sending miner registration message workflow is taking too much time");
			}
		}

		protected void AddMiningHistoryEntry(BlockElectionDistillate blockElectionDistillate, FinalElectionResultDistillate finalElectionResultDistillate) {

			MiningHistoryEntry entry = this.PrepareMiningHistoryEntry(blockElectionDistillate, finalElectionResultDistillate);

			lock(this.historyLocker) {
				this.MiningHistory.Enqueue(entry);

				while(this.MiningHistory.Count > 100) {
					this.MiningHistory.Dequeue();
				}
			}
		}

		protected abstract MiningHistoryEntry CreateMiningHistoryEntry();

		protected virtual MiningHistoryEntry PrepareMiningHistoryEntry(BlockElectionDistillate blockElectionDistillate, FinalElectionResultDistillate finalElectionResultDistillate) {

			MiningHistoryEntry entry = this.CreateMiningHistoryEntry();
			entry.blockId = blockElectionDistillate.currentBlockId - finalElectionResultDistillate.BlockOffset;
			entry.selectedTransactions.AddRange(finalElectionResultDistillate.TransactionIds.Select(t => new TransactionId(t)));

			return entry;
		}

	#region Utilities

		protected void AddElectionBlock(BlockElectionDistillate blockElectionDistillate) {

			if(blockElectionDistillate.HasActiveElection) {
				long maturityId = blockElectionDistillate.currentBlockId + blockElectionDistillate.electionContext.Maturity;

				if(!this.electionBlockCache.ContainsKey(maturityId)) {
					this.electionBlockCache.Add(maturityId, new List<BlockElectionDistillate>());
				}

				this.electionBlockCache[maturityId].Add(blockElectionDistillate);
			}

			// now clear any obsolete entries
			foreach(long entry in this.electionBlockCache.Keys.Where(k => k < blockElectionDistillate.currentBlockId).ToList()) {
				this.electionBlockCache.Remove(entry);
			}
		}

		protected List<BlockElectionDistillate> ObtainMatureActiveElectionBlocks(long maturityBlockId, List<long> matureBlockIds = null) {

			var results = new List<BlockElectionDistillate>();

			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {
				// we only care about active ones
				results.AddRange(this.electionBlockCache[maturityBlockId].Where(b => (b.electionContext.ElectionMode == ElectionModes.Active) && (matureBlockIds?.Contains(b.currentBlockId) ?? true)));
			}

			return results;
		}

		protected List<BlockElectionDistillate> ObtainMatureElectionBlocks(long maturityBlockId, List<long> matureBlockIds = null) {

			var results = new List<BlockElectionDistillate>();

			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {
				// we only care about active ones
				results.AddRange(this.electionBlockCache[maturityBlockId].Where(b => matureBlockIds?.Contains(b.currentBlockId) ?? true));
			}

			return results;
		}

		protected BlockElectionDistillate ObtainMatureElectionBlock(long maturityBlockId, long electionBlockId) {
			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {

				BlockElectionDistillate block = this.electionBlockCache[maturityBlockId].SingleOrDefault(b => b.currentBlockId == electionBlockId);

				if(block != null) {
					return block;
				}
			}

			// ok we dont have it in cache, lets get it from disk
			return this.PrepareBlockElectionContext(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock<IBlock>(electionBlockId), this.MiningAccountId);
		}

	#endregion

	#region Election

		/// <summary>
		///     Rehydrate the election context if it is required
		/// </summary>
		/// <param name="blockElectionDistillate"></param>
		public virtual void RehydrateBlockElectionContext(BlockElectionDistillate blockElectionDistillate) {

			blockElectionDistillate.RehydrateElectionContext(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
		}

		/// <summary>
		///     Here we take an election block and abstract it's election into an election context
		/// </summary>
		/// <param name="currentBlock"></param>
		/// <param name="miningAccountId"></param>
		/// <returns></returns>
		public virtual BlockElectionDistillate PrepareBlockElectionContext(IBlock currentBlock, AccountId miningAccountId) {

			if(miningAccountId == null) {
				throw new ApplicationException("Invalid mining account Id");
			}

			BlockElectionDistillate blockElectionDistillate = this.CreateBlockElectionContext();

			blockElectionDistillate.currentBlockId = currentBlock.BlockId.Value;
			blockElectionDistillate.blockHash.Entry = currentBlock.Hash.Entry;
			blockElectionDistillate.blockType = currentBlock.Version;

			foreach(IIntermediaryElectionResults entry in currentBlock.IntermediaryElectionResults) {

				var intermediateElectionContext = blockElectionDistillate.CreateIntermediateElectionContext();
				
				// now the questions
				//TODO: this needs major cleaning
				if(entry.SimpleQuestion is IBlockTransactionIdElectionQuestion blockTransactionIdElectionQuestion) {
					var question = new QuestionTransactionSectionDistillate();

					question.BlockId = blockTransactionIdElectionQuestion.BlockId;
					
					question.TransactionIndex = (int?)blockTransactionIdElectionQuestion.TransactionIndex?.Value;

					question.SelectedTransactionSection = (byte)blockTransactionIdElectionQuestion.SelectedTransactionSection;
					question.SelectedComponent = (byte)blockTransactionIdElectionQuestion.SelectedComponent;
					
					intermediateElectionContext.SimpleQuestion = question;
				}
				
				if(entry.HardQuestion is IBlockTransactionIdElectionQuestion hardBlockTransactionIdElectionQuestion) {
					var question = new QuestionTransactionSectionDistillate();

					question.BlockId = hardBlockTransactionIdElectionQuestion.BlockId;
					question.TransactionIndex = (int?)hardBlockTransactionIdElectionQuestion.TransactionIndex?.Value;

					question.SelectedTransactionSection = (byte)hardBlockTransactionIdElectionQuestion.SelectedTransactionSection;
					question.SelectedComponent = (byte)hardBlockTransactionIdElectionQuestion.SelectedComponent;
					
					intermediateElectionContext.HardQuestion = question;
				}
				
				if(entry is IPassiveIntermediaryElectionResults simplepassiveIntermediaryElectionResults) {

					if(simplepassiveIntermediaryElectionResults.ElectedCandidates.Contains(miningAccountId)) {
						// thats us!!
						intermediateElectionContext.PassiveElectionContextDistillate = blockElectionDistillate.CreatePassiveElectionContext();

						this.PreparePassiveElectionContext(currentBlock.BlockId.Value, miningAccountId, intermediateElectionContext.PassiveElectionContextDistillate, simplepassiveIntermediaryElectionResults, currentBlock);
					}
				}
				
				blockElectionDistillate.IntermediaryElectionResults.Add(intermediateElectionContext);
			}

			foreach(IFinalElectionResults entry in currentBlock.FinalElectionResults) {

				if(entry is IFinalElectionResults simpleFinalElectionResults) {

					if(simpleFinalElectionResults.ElectedCandidates.ContainsKey(miningAccountId)) {
						// thats us!!
						FinalElectionResultDistillate finalResultDistillateEntry = blockElectionDistillate.CreateFinalElectionResult();
						this.PrepareFinalElectionContext(currentBlock.BlockId.Value, miningAccountId, finalResultDistillateEntry, simpleFinalElectionResults, currentBlock);

						blockElectionDistillate.FinalElectionResults.Add(finalResultDistillateEntry);
					}
				}
			}

			// we have them, so let's set them ehre
			blockElectionDistillate.BlockTransactionIds = currentBlock.GetAllTransactions().Select(t => t.ToString()).ToList();

			if(currentBlock is IElectionBlock currentElection) {

				blockElectionDistillate.electionContext = currentElection.ElectionContext;

				blockElectionDistillate.HasActiveElection = true;
			}

			return blockElectionDistillate;
		}

		/// <summary>
		///     Here we take an election block and abstract ti's election into an election context
		/// </summary>
		/// <param name="currentBlock"></param>
		/// <param name="miningAccountId"></param>
		/// <returns></returns>
		public virtual void PrepareBlockElectionContext(BlockElectionDistillate blockElectionDistillate, AccountId miningAccountId) {
			if((blockElectionDistillate.blockHash == null) || blockElectionDistillate.blockHash.IsEmpty) {
				if(string.IsNullOrWhiteSpace(blockElectionDistillate.blockHash64)) {
					throw new ApplicationException("Invalid block distillate");
				}

				blockElectionDistillate.blockHash.Entry = ByteArray.FromBase64(blockElectionDistillate.blockHash64).Entry;
			}

			if((blockElectionDistillate.blockType == (ComponentVersion<BlockType>) null) || blockElectionDistillate.blockType.IsNull) {
				if(string.IsNullOrWhiteSpace(blockElectionDistillate.blockTypeString)) {
					throw new ApplicationException("Invalid block type");
				}

				blockElectionDistillate.blockType = new ComponentVersion<BlockType>(blockElectionDistillate.blockTypeString);
			}

			blockElectionDistillate.MiningAccountId = miningAccountId;
			blockElectionDistillate.blockxxHash = this.hasher.Hash(blockElectionDistillate.blockHash);

			this.RehydrateBlockElectionContext(blockElectionDistillate);
		}

		protected virtual void PreparePassiveElectionContext(long currentBlockId, AccountId miningAccountId, PassiveElectionContextDistillate passiveElectionContextDistillate, IIntermediaryElectionResults intermediaryElectionResult, IBlock currentBlock) {

			passiveElectionContextDistillate.electionBlockId = currentBlockId - intermediaryElectionResult.BlockOffset;

			long electionBlockId = currentBlockId - intermediaryElectionResult.BlockOffset;

			BlockElectionDistillate electionBlock = this.ObtainMatureElectionBlock(currentBlockId, electionBlockId);

		}

		protected virtual void PrepareFinalElectionContext(long currentBlockId, AccountId miningAccountId, FinalElectionResultDistillate finalResultDistillateEntry, IFinalElectionResults finalElectionResult, IBlock currentBlock) {

			finalResultDistillateEntry.BlockOffset = finalElectionResult.BlockOffset;

			if(finalElectionResult.ElectedCandidates.ContainsKey(miningAccountId)) {
				finalResultDistillateEntry.TransactionIds = finalElectionResult.ElectedCandidates[miningAccountId].Transactions.Select(t => t.ToString()).ToList();
				finalResultDistillateEntry.DelegateAccountId = finalElectionResult.ElectedCandidates[miningAccountId].DelegateAccountId?.ToString();
			}

			long electionBlockId = currentBlockId - finalElectionResult.BlockOffset;

		}

		protected abstract BlockElectionDistillate CreateBlockElectionContext();

		/// <summary>
		///     here we perform the entire election in a single shot since we have the block to read the transactions
		/// </summary>
		/// <param name="currentBlock"></param>
		/// <param name="chainEventPoolProvider"></param>
		/// <returns></returns>
		public virtual List<IElectionCandidacyMessage> PerformElection(IBlock currentBlock, IEventPoolProvider chainEventPoolProvider) {

			BlockElectionDistillate blockElectionDistillate = this.PrepareBlockElectionContext(currentBlock, this.MiningAccountId);

			var electionResults = this.PerformElectionComputations(blockElectionDistillate, chainEventPoolProvider);

			if(electionResults == null) {
				return new List<IElectionCandidacyMessage>();
			}

			// select transactions
			foreach(ElectedCandidateResultDistillate result in electionResults) {

				// select transactions for this election
				result.SelectedTransactionIds = this.SelectTransactions(blockElectionDistillate.currentBlockId, result, chainEventPoolProvider).Select(t => t.ToString()).ToList();
			}

			return this.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults, chainEventPoolProvider);
		}

		public List<TransactionId> SelectTransactions(BlockId currentBlockId, ElectedCandidateResultDistillate resultDistillate, IEventPoolProvider chainEventPoolProvider) {

			BlockElectionDistillate matureElectionBlock = this.ObtainMatureElectionBlock(currentBlockId.Value, resultDistillate.BlockId);

			if(matureElectionBlock != null) {
				IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(resultDistillate, this.centralCoordinator, chainEventPoolProvider);

				// select transactions for this election
				return matureElectionProcessor.SelectTransactions(currentBlockId.Value, matureElectionBlock);
			}

			return new List<TransactionId>();
		}

		/// <summary>
		///     here we validate the election, and see if we are elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <returns></returns>
		public virtual List<ElectedCandidateResultDistillate> PerformElectionComputations(BlockElectionDistillate blockElectionDistillate, IEventPoolProvider chainEventPoolProvider) {
			if(!this.MiningEnabled || (this.factory == null)) {
				return null; // sorry, not happening
			}

			if(!this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
				return null; // sorry, not happening, we can not mine while we are not synced
			}

			var electionResults = new List<ElectedCandidateResultDistillate>();

			this.PrepareBlockElectionContext(blockElectionDistillate, this.MiningAccountId);

			//ok, here is how we proceed
			//1. if we have an election block
			//	1.1 remove any confirmed transactions from our election cache
			//	1.2 if there are passive elections, see if we are part of them
			//2. if there are mature blocks, lets participate in the election

			// if it is not an election block, it will be null, which is fine

			if(blockElectionDistillate.HasActiveElection) {
				// first thing, record this block in our cache so that we are ready to process it when it reaches maturity
				this.AddElectionBlock(blockElectionDistillate);

				Log.Information($"Block {blockElectionDistillate.currentBlockId} is an {blockElectionDistillate.electionContext.ElectionMode} election block and will be mature at height {blockElectionDistillate.currentBlockId + blockElectionDistillate.electionContext.Maturity} and published at height {blockElectionDistillate.currentBlockId + blockElectionDistillate.electionContext.Maturity + blockElectionDistillate.electionContext.Publication}");

				// ok, any transactions in this block must be removed from our election cache. First, sum all the transactions

				// and clear our election cache
				IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.RemoveBlockElectionTransactions(blockElectionDistillate.currentBlockId, blockElectionDistillate.BlockTransactionIds.Select(t => new TransactionId(t)).ToList(), account);

			}

			// now, lets check if we are part of any confirmed election results
			if(blockElectionDistillate.FinalElectionResults.Any()) {

				foreach(FinalElectionResultDistillate finalElectionResult in blockElectionDistillate.FinalElectionResults) {

					this.ConfirmedPrimeElected(blockElectionDistillate, finalElectionResult);
				}
			}

			// now, lets check if we are part of any passive elections
			if(blockElectionDistillate.IntermediaryElectionResults.Any()) {

				foreach(var intermediaryElectionResult in blockElectionDistillate.IntermediaryElectionResults) {

					ElectedCandidateResultDistillate electionResultDistillate = this.CreateElectedCandidateResult();
					electionResultDistillate.ElectionMode = ElectionModes.Active;

					// let's answer the questions if we can
					var configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
					if(!GlobalSettings.ApplicationSettings.MobileMode && BlockchainUtilities.UsesPartialBlocks(configuration.BlockSavingMode) && intermediaryElectionResult.SimpleQuestion != null) {

						var question = (QuestionTransactionSectionDistillate) intermediaryElectionResult.SimpleQuestion;

						electionResultDistillate.simpleAnswer = this.AnswerQuestion(intermediaryElectionResult.SimpleQuestion);
					}
					
					if(!GlobalSettings.ApplicationSettings.MobileMode && BlockchainUtilities.UsesAllBlocks(configuration.BlockSavingMode) && intermediaryElectionResult.HardQuestion != null) {
					
						var question = (QuestionTransactionSectionDistillate)intermediaryElectionResult.HardQuestion;
							
						electionResultDistillate.hardAnswer = this.AnswerQuestion(intermediaryElectionResult.HardQuestion);
					}
					
					if(intermediaryElectionResult.PassiveElectionContextDistillate != null) {
						// ok, get the cached context
						BlockElectionDistillate electionBlock = this.ObtainMatureElectionBlock(blockElectionDistillate.currentBlockId, intermediaryElectionResult.PassiveElectionContextDistillate.electionBlockId);

						if(electionBlock != null) {
							
							electionResultDistillate.BlockId = electionBlock.currentBlockId;
							electionResultDistillate.MaturityBlockId = blockElectionDistillate.currentBlockId;

							electionResultDistillate.ElectionMode = ElectionModes.Passive;

							electionResultDistillate.MaturityBlockHash = electionBlock.blockxxHash;
							electionResultDistillate.MatureBlockType = electionBlock.blockType;
							electionResultDistillate.MatureElectionContextVersion = electionBlock.electionContext.Version;

							this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningElected(electionBlock.currentBlockId));
							Log.Information($"We are elected in Block {blockElectionDistillate.currentBlockId}");

							electionResults.Add(electionResultDistillate);
						}
					}
				}
			}

			// now, see if we have any elections that are mature now
			var matureElectionBlocks = this.ObtainMatureActiveElectionBlocks(blockElectionDistillate.currentBlockId);

			// ok, lets run the elections that are due right now!
			foreach(BlockElectionDistillate matureElectionBlock in matureElectionBlocks) {

				Log.Information($"We have a mature election block with Id {matureElectionBlock.currentBlockId}");
				IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(matureElectionBlock, this.centralCoordinator, chainEventPoolProvider);

				ElectedCandidateResultDistillate electionResultDistillate = matureElectionProcessor.PerformActiveElection(blockElectionDistillate.blockxxHash, matureElectionBlock, this.MiningAccountId);

				if(electionResultDistillate != null) {

					electionResultDistillate.MatureBlockType = matureElectionBlock.blockType;
					electionResultDistillate.MatureElectionContextVersion = matureElectionBlock.electionContext.Version;

					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningElected(matureElectionBlock.currentBlockId));
					Log.Information($"We are elected in Block {blockElectionDistillate.currentBlockId}");

					electionResults.Add(electionResultDistillate);
				}
			}

			return electionResults;
		}

		protected long? AnswerQuestion(ElectionQuestionDistillate question) {

			long? answer = null;

			try {
				if(question != null && question is QuestionTransactionSectionDistillate questionTransactionSectionDistillate) {
					//TODO: this needs much refining
					var block = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(questionTransactionSectionDistillate.BlockId);

					if(block == null) {
						return null;
					}

					answer = 0;

					var selectedTransactionSection = (BlockTransactionIdElectionQuestion.QuestionTransactionSection) questionTransactionSectionDistillate.SelectedTransactionSection;
					var selectedComponent = (BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents) questionTransactionSectionDistillate.SelectedComponent;

					List<TransactionIdExtended> transactionIds = new List<TransactionIdExtended>();

					switch(selectedTransactionSection) {
						case BlockTransactionIdElectionQuestion.QuestionTransactionSection.Block:

							switch(selectedComponent) {
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Hash:
									
									TypeSerializer.Deserialize(block.Hash.Span.Slice(0,8), out long result);
									return result;
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.BlockTimestamp:
									return block.Timestamp.Value;
							}

							break;
						case BlockTransactionIdElectionQuestion.QuestionTransactionSection.ConfirmedKeyedTransactions:
							transactionIds = block.ConfirmedKeyedTransactions.Select(e => e.TransactionId).ToList();

							break;
						case BlockTransactionIdElectionQuestion.QuestionTransactionSection.ConfirmedTransactions:
							transactionIds = block.ConfirmedTransactions.Select(e => e.TransactionId).ToList();

							break;
						case BlockTransactionIdElectionQuestion.QuestionTransactionSection.RejectedTransactions:
							transactionIds = block.RejectedTransactions.Select(e => e.TransactionId).ToList();

							break;
					}

					if(transactionIds.Any()) {

						try {
							var transactionId = transactionIds[(int) questionTransactionSectionDistillate.TransactionIndex.Value];

							switch(selectedComponent) {
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.AccountId:
									answer = transactionId.Account.SequenceId;

									break;
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Timestamp:
									answer = transactionId.Timestamp.Value;

									break;
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Scope:
									answer = transactionId.Scope;

									break;
							}
						} catch(Exception ex) {
							// do nothing, it will be a null answer
							Log.Verbose(ex, "Failed to answer mining question");
						}
					}
				}
			}
			catch(Exception ex) {
				Log.Error(ex, "Failed to answer mining question");	
			}
			
			return answer;
		}
		protected abstract ElectedCandidateResultDistillate CreateElectedCandidateResult();

		/// <summary>
		///     here we validate the election, and see if we are elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <returns></returns>
		public virtual List<IElectionCandidacyMessage> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, IEventPoolProvider chainEventPoolProvider) {
			if(!this.MiningEnabled || (this.factory == null)) {
				return null; // sorry, not happening
			}

			if(!this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {
				return null; // sorry, not happening, we can not mine while we are not synced
			}

			var messages = new List<IElectionCandidacyMessage>();
			RestUtility restUtility = null;
			
			//ok, here is how we proceed
			//1. if we have an election block
			//	1.1 remove any confirmed transactions from our election cache
			//	1.2 if there are passive elections, see if we are part of them
			//2. if there are mature blocks, lets participate in the election

			bool useWeb = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web);
			bool useChain = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Gossip);

			// ok, lets run the elections that are due right now!
			bool updateController = false;

			foreach(ElectedCandidateResultDistillate electionResult in electionResults) {

				BlockElectionDistillate matureElectionBlock = this.ObtainMatureElectionBlock(blockElectionDistillate.currentBlockId, electionResult.BlockId);

				if(matureElectionBlock != null) {

					if(useWeb && restUtility == null) {
						restUtility = new RestUtility(GlobalSettings.ApplicationSettings);
					}
					
					Log.Information($"We have a mature election block with Id {electionResult.BlockId}");
					IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(matureElectionBlock, this.centralCoordinator, chainEventPoolProvider);

					bool sent = false;

					if(useWeb) {
						try {

							Repeater.Repeat(() => {
								string url = this.ChainConfiguration.WebElectionsRegistrationUrl;
								
								Dictionary<string, object> parameters = null;
								string action = "";
							
								if(electionResult.ElectionMode == ElectionModes.Active) {
									parameters = matureElectionProcessor.PrepareActiveElectionWebConfirmation(blockElectionDistillate, electionResult);
									action = "record-active-election";
								} else if(electionResult.ElectionMode == ElectionModes.Passive) {
									parameters = matureElectionProcessor.PreparePassiveElectionWebConfirmation(blockElectionDistillate, electionResult);
									action = "record-passive-election";
								} else {
									throw new ApplicationException("Invalid election type");
								}
								
								var result = restUtility.Put(url, action, parameters);
								result.Wait();
							
								if(!result.IsFaulted) {
							
									// ok, check the result
									if(result.Result.StatusCode == HttpStatusCode.OK) {
										// ok, we are not registered. we can await a response from the IP Validator
										return;
									}
								}
							
								throw new ApplicationException("Failed to record election results through web");
							});

							updateController = true;
							sent = true;
						} catch(Exception ex) {
							Log.Error(ex, "Failed to record election results through web");
							// do nothing, we will sent it on chain
							sent = false;
						}
					}

					if(!sent && useChain) {
						// ok, we are going to send it on chain
						IElectionCandidacyMessage electionConfirmationMessage = null;

						if(electionResult.ElectionMode == ElectionModes.Active) {
							electionConfirmationMessage = matureElectionProcessor.PrepareActiveElectionConfirmationMessage(blockElectionDistillate, electionResult);
						} 
						else if(electionResult.ElectionMode == ElectionModes.Passive) {
							electionConfirmationMessage = matureElectionProcessor.PreparePassiveElectionConfirmationMessage(blockElectionDistillate, electionResult);
						}
						else {
							throw new ApplicationException("Invalid election type");
						}

						if(electionConfirmationMessage != null) {

							messages.Add(electionConfirmationMessage);
							sent = true;
						}
					}
				}
			}

			// ok, thats it. lets see if we have any messages to send out
			return messages.Where(m => m != null).ToList();
		}

		protected virtual void ConfirmedPrimeElected(BlockElectionDistillate blockElectionDistillate, FinalElectionResultDistillate finalElectionResultDistillate) {
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MininPrimeElected(blockElectionDistillate.currentBlockId));

			this.AddMiningHistoryEntry(blockElectionDistillate, finalElectionResultDistillate);

			Log.Information($"We were officially announced as a prime elected in Block {blockElectionDistillate.currentBlockId} for the election that was announced in block {blockElectionDistillate.currentBlockId - finalElectionResultDistillate.BlockOffset}");
		}

		protected abstract IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GetElectionProcessorFactory();

	#endregion

	}
}