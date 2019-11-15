using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.EntityFrameworkCore.Internal;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
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

		void PerformElection(IBlock currentBlock, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager, Action<List<IElectionCandidacyMessage>> electedCallback);
		List<ElectedCandidateResultDistillate> PerformElectionComputations(BlockElectionDistillate blockElectionDistillate, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager);
		List<IElectionCandidacyMessage> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager);
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

		/// <summary>
		/// amount of seconds to wait for the blockchain to sync before giving up as out of sync
		/// </summary>
		private const int CHAIN_SYNC_WAIT_TIMEOUT = 30;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		/// <summary>
		///     Here we store the elections that have yet to come to maturity
		/// </summary>
		protected readonly Dictionary<long, List<BlockElectionDistillate>> electionBlockCache = new Dictionary<long, List<BlockElectionDistillate>>();

		/// <summary>
		/// Queue of events stored for later
		/// </summary>
		protected readonly ConcurrentQueue<Action> callbackQueue = new ConcurrentQueue<Action>();

		protected readonly IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> factory;

		private readonly object historyLocker = new object();

		private readonly object locker = new object();

		protected readonly Queue<MiningHistoryEntry> MiningHistory = new Queue<MiningHistoryEntry>();

		protected readonly ITimeService timeService;
		protected bool miningEnabled;

		protected BlockchainNetworkingService.MiningRegistrationParameters registrationParameters;

		private Timer updateMiningRegistrationTimer;

		private Timer updateMiningStatusTimer;

		/// <summary>
		/// If we request a mining callback to enable mining when blockchain is ready. AppSettings chain config EnableMiningPreload must be set to true
		/// </summary>
		private bool miningPreloadRequested = false;

		private Action miningPreloadCallback;

		public ChainMiningProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.centralCoordinator.BlockchainSynced += this.OnSyncedEvent;
			this.centralCoordinator.WalletSynced += this.OnSyncedEvent;
			
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;

			this.factory = this.GetElectionProcessorFactory();
		}

		protected BlockChainConfigurations ChainConfiguration => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
		protected IWalletProviderProxy WalletProvider => this.centralCoordinator.ChainComponentProvider.WalletProviderBase;

		public AccountId MiningAccountId { get; private set; }

		/// <summary>
		///     Is mining allowed on this chain
		/// </summary>
		public abstract bool MiningAllowed { get; }

		/// <summary>
		///     This is NOT saved to the filesystem. we hold it in memory and we start fresh every time we load the daemon
		/// </summary>
		public bool MiningEnabled => this.MiningAllowed && this.miningEnabled;

		public IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ElectionProcessorFactory => this.factory;

		public virtual void EnableMining(AccountId miningAccountId, AccountId delegateAccountId) {

			if(this.MiningEnabled || this.miningPreloadRequested) {

				if(this.MiningAccountId == null) {
					this.DisableMining();
				}

				return;
			}

			IWalletAccount miningWalletAccount = null;

			if(miningAccountId == null) {

				if(!this.WalletProvider.IsDefaultAccountPublished) {

					string message = "Failed to mine. The mining account has not yet been fully published. Mining is not yet possible until the account is presented and confirmed on the blockchain.";
					Log.Error(message);

					throw new ApplicationException(message);
				}

				miningAccountId = this.WalletProvider.GetPublicAccountId();

				if(miningAccountId == null) {
					string message = "Failed to mine. We could not load the default published account.";
					Log.Error(message);

					throw new ApplicationException(message);
				}

				miningWalletAccount = this.WalletProvider.GetActiveAccount();
			} else {

				miningWalletAccount = this.WalletProvider.GetWalletAccount(miningAccountId);

				if(miningWalletAccount == null) {
					string message = "Failed to mine. Account does not exist.";
					Log.Error(message);

					throw new ApplicationException(message);
				}

				if(!this.WalletProvider.IsAccountPublished(miningWalletAccount.AccountUuid)) {
					string message = "Failed to mine. The mining account has not yet been fully published. Mining is not yet possible until the account is presented and confirmed on the blockchain.";
					Log.Error(message);

					throw new ApplicationException(message);
				}
			}

			if(!this.WalletProvider.IsWalletLoaded) {
				string message = "Failed to mine. A wallet must be loaded to mine.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			if(!this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web) && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
				string message = "Failed to mine. Your must be connected to some peers to mine.";
				Log.Error(message);

				throw new ApplicationException(message);
			}

			void QueryKeyPassphrase() {
				// now, if the message key is encrypted, we need the passphrase
				if(miningWalletAccount.KeysEncrypted) {

					//ok, we must reqeset it
					this.WalletProvider.EnsureWalletKeyIsReady(miningWalletAccount.AccountUuid, GlobalsService.MESSAGE_KEY_ORDINAL_ID);

				}
			}

			if(!this.centralCoordinator.IsChainLikelySynchronized) {
				if(!this.CheckSyncStatus()) {
					// chain is still not synced, we wither fail, or register for full sync to start the mininig automatically
					void Catcher() {
						try {
							this.EnableMining(miningAccountId, delegateAccountId);
						} finally {
							this.miningPreloadRequested = false;

							if(this.miningPreloadCallback != null) {
								this.centralCoordinator.BlockchainSynced -= this.miningPreloadCallback;
								this.miningPreloadCallback = null;
							}
						}
					}

					if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableMiningPreload && !this.miningPreloadRequested) {

						QueryKeyPassphrase();

						this.miningPreloadRequested = true;

						this.miningPreloadCallback = Catcher;
						this.centralCoordinator.BlockchainSynced += this.miningPreloadCallback;

						Log.Information("Mining could not be enabled as the blockchain is not fully synced. A callback has been set and mining will be enabled automatically when the blockchain is fully synced.");

						return;
					}

					string message = "Mining could not be enabled as the blockchain is not yet fully synced.";
					Log.Error(message);

					throw new ApplicationException(message);
				}
			}

			QueryKeyPassphrase();

			this.MiningAccountId = miningAccountId;
			this.miningEnabled = true;

			try {
				if(this.MiningEnabled) {

					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.MiningStarted);
					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningStatusChanged(this.MiningEnabled));

					Log.Information("Mining is now enabled.");

					// create the elector cache wallet file
					IWalletAccount miningAccount = this.WalletProvider.GetWalletAccount(this.MiningAccountId);
					this.WalletProvider.CreateElectionCacheWalletFile(miningAccount);

					// register the chain in the network service, so we can answer the IP Validator

					if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.DisableMiningRegistration) {

						this.registrationParameters = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterMiningRegistrationParameters();

						this.registrationParameters.AccountId = miningAccountId;
						this.registrationParameters.DelegateAccountId = delegateAccountId;
						this.registrationParameters.Password = 0;

						// here we can reuse our existing password if its not too old
						if(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate >= (DateTime.UtcNow - GlobalsService.TimeoutMinerDelay)) {
							this.registrationParameters.Password = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.MiningPassword;

							if(this.registrationParameters.Password == 0) {
								// clear it all
								this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.MiningPassword = 0;
								this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate = DateTime.MinValue;
							}
						}

						if(this.registrationParameters.Password == 0) {
							// generate our random password
							do {
								this.registrationParameters.Password = GlobalRandom.GetNextLong();
							} while(this.registrationParameters.Password == 0);
						}

						// ok, now we must register for mining.  if we can, we will try the rest api first, its so much simpler. if we can't, we will publish a message on chain

						bool success = false;
						bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web);
						bool chain = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Gossip);

						if(web) {
							try {
								this.PerformWebapiRegistration();

								// start our account update service
								this.StartAccountUpdateController();
								this.StartMiningStatusUpdateCheck();

								success = true;
							} catch(Exception ex) {

								if(chain) {
									// do not raise an exception, we will try to mine on chain
									Log.Error(ex, "Failed to register for mining by webapi.");
								} else {
									throw new ApplicationException("Failed to register for mining by webapi.", ex);
								}
							}
						}

						if(chain && !success) {

							try {

								this.PerformOnchainRegistration();
								success = true;
							} catch(Exception ex) {
								throw new ApplicationException("Failed to register for mining by on chain message.", ex);
							}
						}

						if(success) {
							this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.MiningPassword = this.registrationParameters.Password;
							this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate = DateTime.UtcNow;
						}
					}
				} else {
					throw new ApplicationException();
				}
			} catch(Exception ex) {
				this.registrationParameters = null;
				Log.Error(ex, "Mining is disabled. Impossible to enable mining.");
				this.DisableMining();
			}

		}

		public virtual void DisableMining() {

			if(!this.MiningEnabled && !this.miningPreloadRequested && this.miningPreloadCallback == null) {
				return;
			}

			this.StopAccountUpdateController();

			this.miningEnabled = false;

			while(this.callbackQueue.TryDequeue(out Action callback)) {
				// do nothing, we are clearing.
			}
			
			if(this.miningPreloadRequested) {
				if(this.miningPreloadCallback != null) {
					this.centralCoordinator.BlockchainSynced += this.miningPreloadCallback;
					this.miningPreloadCallback = null;
				}

				this.miningPreloadRequested = false;
			}

			bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(ChainConfigurations.RegistrationMethods.Web);

			if(web && this.registrationParameters != null) {
				try {
					// lets try to release our registration if we can
					this.PerformWebapiRegistrationStop();

				} catch(Exception ex) {
					// do nothing if this failed, its not very important
					Log.Warning(ex, "Failed to stop mining through web registration");
				}
			}

			this.registrationParameters = null;

			this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.MiningEnded);
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningStatusChanged(this.MiningEnabled));
			
			Log.Information("Mining is now disabled.");

			// delete the file from the wallet
			IWalletAccount miningAccount = this.WalletProvider.GetWalletAccount(this.MiningAccountId);
			this.WalletProvider.DeleteElectionCacheWalletFile(miningAccount);

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
				TimeSpan waitTime = TimeSpan.FromMinutes(GlobalsService.UPDATE_MINING_REGISTRATION_DELAY);

				this.updateMiningRegistrationTimer = new Timer(state => {

					this.UpdateWebapiAccountRegistration();

				}, this, waitTime, waitTime);
			}
		}

		protected void StartMiningStatusUpdateCheck() {
			if(this.updateMiningStatusTimer == null && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableMiningStatusChecks) {

				this.updateMiningStatusTimer = new Timer(state => {

					this.CheckMiningStatus();

				}, this, TimeSpan.FromMinutes(GlobalsService.UPDATE_MINING_STATUS_START_DELAY), TimeSpan.FromMinutes(GlobalsService.UPDATE_MINING_STATUS_DELAY));
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

			try {
				this.updateMiningRegistrationTimer?.Dispose();
			} catch(Exception ex) {
				Log.Error(ex, $"Failed to dispose {nameof(this.updateMiningRegistrationTimer)}");
			}

			this.updateMiningRegistrationTimer = null;

			try {
				this.updateMiningStatusTimer?.Dispose();
			} catch(Exception ex) {
				Log.Error(ex, $"Failed to dispose {nameof(this.updateMiningStatusTimer)}");
			}

			this.updateMiningStatusTimer = null;

			return wasRunning;
		}

		/// <summary>
		///     this method allows to check if its time to act, or if we should sleep more
		/// </summary>
		/// <returns></returns>
		private bool ShouldAct(ref DateTime? action) {
			if(!action.HasValue) {
				return true;
			}

			if(action.Value < DateTime.UtcNow) {
				action = null;

				return true;
			}

			return false;
		}

		protected ElectionsCandidateRegistrationInfo PrepareRegistrationInfo() {

			if(this.registrationParameters == null) {
				throw new ArgumentException("registration parameters are null");
			}

			ElectionsCandidateRegistrationInfo info = new ElectionsCandidateRegistrationInfo();

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp != null) {
				NodeAddressInfo node = new NodeAddressInfo(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp, GlobalSettings.ApplicationSettings.Port, Enums.PeerTypes.Unknown);
				info.Ip = node.Ip; // always as IpV6
			}

			info.Port = GlobalSettings.ApplicationSettings.Port;

			info.AccountId = this.registrationParameters.AccountId;
			info.DelegateAccountId = this.registrationParameters.DelegateAccountId;

			info.ChainType = this.centralCoordinator.ChainId;
			info.Password = this.registrationParameters.Password;

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
					this.PerformWebapiRegistrationUpdate();

					this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastMiningRegistrationUpdate = DateTime.UtcNow;

				} catch(Exception ex) {
					Log.Error(ex, "Failed to update mining registration by webapi.");
					this.DisableMining();
				}
			}
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected void PerformWebapiRegistrationUpdate() {

			var registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == 0) {
				throw new ApplicationException("Failed to update mining registration. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

			Repeater.Repeat(() => {
				string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add("accountId", registrationInfo.AccountId.ToLongRepresentation());

				if(registrationInfo.DelegateAccountId != null) {
					parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToLongRepresentation());
				}

				parameters.Add("password", registrationInfo.Password);

				var result = restUtility.Put(url, "registration/update-registration", parameters);

				if(result.Wait(TimeSpan.FromSeconds(20)) && !result.IsFaulted) {

					// ok, check the result
					if(result.Result.StatusCode == HttpStatusCode.OK) {

						// we just udpated
						return;
					}
				}

				throw new ApplicationException("Failed to update mining registration through web");

			});
		}

		/// <summary>
		///     try to unregister a stop through the public webapi interface
		/// </summary>
		protected void PerformWebapiRegistrationStop() {

			var registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == 0) {
				throw new ApplicationException("Failed to stop mining registration. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

			Repeater.Repeat(() => {
				string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add("accountId", registrationInfo.AccountId.ToLongRepresentation());
				parameters.Add("password", registrationInfo.Password);

				var result = restUtility.Put(url, "registration/stop", parameters);

				if(result.Wait(TimeSpan.FromSeconds(20)) && !result.IsFaulted) {

					// ok, check the result
					if(result.Result.StatusCode == HttpStatusCode.OK) {
						// ok, we are not registered. we can await a response from the IP Validator
						return;
					}
				}

				throw new ApplicationException("Failed to stop mining through web");
			});
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected void PerformWebapiRegistration() {

			var registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == 0) {
				throw new ApplicationException("Failed to register for mining. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

			using(IXmssWalletKey key = this.WalletProvider.LoadKey<IXmssWalletKey>(GlobalsService.MESSAGE_KEY_NAME)) {

				// and sign the whole thing with our key
				SafeArrayHandle password = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(registrationInfo.Password, password.Span);
				var autograph = this.WalletProvider.SignMessageXmss(password, key);
				registrationInfo.Autograph = autograph.ToExactByteArrayCopy();
				autograph.Return();

				if(registrationInfo.Autograph != null) {

					Log.Verbose("Message successfully signed.");

					var autograph64 = Convert.ToBase64String(registrationInfo.Autograph);
					string url = this.ChainConfiguration.WebElectionsRegistrationUrl;
					var longAccountId = registrationInfo.AccountId.ToLongRepresentation();

					Repeater.Repeat(() => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();
						parameters.Add("accountId", longAccountId);

						if(registrationInfo.DelegateAccountId != null) {
							parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToLongRepresentation());
						}

						parameters.Add("password", registrationInfo.Password);
						parameters.Add("autograph", autograph64);

						var result = restUtility.Put(url, "registration/register", parameters);

						if(result.Wait(TimeSpan.FromSeconds(5)) && !result.IsFaulted) {

							// ok, check the result
							if(result.Result.StatusCode == HttpStatusCode.OK) {
								// ok, we are not registered. we can await a response from the IP Validator
								return;
							}
						}

						throw new ApplicationException("Failed to register for mining through web");

					});

				} else {
					throw new ApplicationException("Failed to register for mining through web; autograph was null");
				}
			}
		}

		protected void CheckMiningStatus() {

			if(this.ChainConfiguration.EnableMiningStatusChecks && (this.registrationParameters != null)) {
				RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings);

				Repeater.Repeat(() => {
					string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

					var electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo();

					if(electionsCandidateRegistrationInfo.Password == 0) {
						throw new ApplicationException("Failed to check mining registration status. Password was not set.");
					}

					var parameters = new Dictionary<string, object>();
					parameters.Add("accountId", electionsCandidateRegistrationInfo.AccountId.ToLongRepresentation());
					parameters.Add("password", electionsCandidateRegistrationInfo.Password);

					var result = restUtility.Post(url, "registration/query-mining-status", parameters);

					// ok, check the result
					if(result.Wait(TimeSpan.FromSeconds(20)) && !result.IsFaulted && result.Result.StatusCode == HttpStatusCode.OK && bool.TryParse(result.Result.Content, out bool status)) {

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
		}

		protected void PerformOnchainRegistration() {

			ElectionsCandidateRegistrationInfo registrationInfo = this.PrepareRegistrationInfo();

			// ok, well this is it, we will register for mining on chain
			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIp == null || string.IsNullOrWhiteSpace(registrationInfo.Ip)) {
				throw new ApplicationException("Our public IP is still undefined. We can not register for mining on chain without an IP address to provide.");
			}

			var sendWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateSendElectionsCandidateRegistrationMessageWorkflow(this.MiningAccountId, registrationInfo, ChainConfigurations.RegistrationMethods.Gossip, new CorrelationContext());

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
				long maturityId = blockElectionDistillate.currentBlockId + blockElectionDistillate.ElectionContext.Maturity;

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
				results.AddRange(this.electionBlockCache[maturityBlockId].Where(b => (b.ElectionContext.ElectionMode == ElectionModes.Active) && (matureBlockIds?.Contains(b.currentBlockId) ?? true)));
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

					question.TransactionIndex = (int?) blockTransactionIdElectionQuestion.TransactionIndex?.Value;

					question.SelectedTransactionSection = (byte) blockTransactionIdElectionQuestion.SelectedTransactionSection;
					question.SelectedComponent = (byte) blockTransactionIdElectionQuestion.SelectedComponent;

					intermediateElectionContext.SimpleQuestion = question;
				}

				if(entry.HardQuestion is IBlockTransactionIdElectionQuestion hardBlockTransactionIdElectionQuestion) {
					var question = new QuestionTransactionSectionDistillate();

					question.BlockId = hardBlockTransactionIdElectionQuestion.BlockId;
					question.TransactionIndex = (int?) hardBlockTransactionIdElectionQuestion.TransactionIndex?.Value;

					question.SelectedTransactionSection = (byte) hardBlockTransactionIdElectionQuestion.SelectedTransactionSection;
					question.SelectedComponent = (byte) hardBlockTransactionIdElectionQuestion.SelectedComponent;

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
			blockElectionDistillate.BlockTransactionIds.AddRange(currentBlock.GetAllTransactions().Select(t => t.ToString()));

			if(currentBlock is IElectionBlock currentElection) {

				blockElectionDistillate.ElectionContext = currentElection.ElectionContext;

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
			if((blockElectionDistillate.blockHash.Entry == null) || blockElectionDistillate.blockHash.IsEmpty) {
				if(string.IsNullOrWhiteSpace(blockElectionDistillate.blockHashSerialized)) {
					throw new ApplicationException("Invalid block distillate");
				}

				blockElectionDistillate.blockHash.Entry = ByteArray.FromBase58(blockElectionDistillate.blockHashSerialized).Entry;
			}

			if((blockElectionDistillate.blockType == (ComponentVersion<BlockType>) null) || blockElectionDistillate.blockType.IsNull) {
				if(string.IsNullOrWhiteSpace(blockElectionDistillate.blockTypeString)) {
					throw new ApplicationException("Invalid block type");
				}

				blockElectionDistillate.blockType = new ComponentVersion<BlockType>(blockElectionDistillate.blockTypeString);
			}

			blockElectionDistillate.MiningAccountId = miningAccountId;
			blockElectionDistillate.blockxxHash = HashingUtils.XxHash32(blockElectionDistillate.blockHash);

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
				finalResultDistillateEntry.TransactionIds.AddRange(finalElectionResult.ElectedCandidates[miningAccountId].Transactions.Select(t => t.ToString()));
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
		public virtual void PerformElection(IBlock currentBlock, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager, Action<List<IElectionCandidacyMessage>> electedCallback) {

			void Callback() {

				// this check is important, in case we are running in deferred mode, we could be out of date, and thus moot.
				if(!this.IsChainFullySynced || this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != currentBlock.BlockId) {
					// this wont work anymore, we must stop
					return;
				}
				
				BlockElectionDistillate blockElectionDistillate = this.PrepareBlockElectionContext(currentBlock, this.MiningAccountId);

				var electionResults = this.PerformElectionComputations(blockElectionDistillate, chainEventPoolProvider, blockchainManager);

				if(electionResults == null) {
					return;
				}

				// select transactions
				foreach(ElectedCandidateResultDistillate result in electionResults) {

					// select transactions for this election
					result.SelectedTransactionIds.AddRange(this.SelectTransactions(blockElectionDistillate.currentBlockId, result, chainEventPoolProvider).Select(t => t.ToString()));
				}

				var messages = this.PrepareElectionCandidacyMessages(blockElectionDistillate, electionResults, chainEventPoolProvider, blockchainManager);

				if(messages.Any()) {
					electedCallback(messages);
				}
			}

			if(this.IsChainFullySynced) {
				// we are all good, we can run it now
				Callback();
			} else {
				// store it for later and hope t will run in time
				this.callbackQueue.Enqueue(Callback);
				this.RequestSync();
			}
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

	#region sync status checking

		private bool IsChainFullySynced => this.IsChainSynced && this.IsWalletSynced;

		private bool IsChainSynced => this.centralCoordinator.IsChainLikelySynchronized;
		
		private bool IsWalletSynced {
			get {
				var walletSynced = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

				return walletSynced.HasValue && walletSynced.Value;
			}

		}

		protected virtual bool CheckSyncStatus() {

			if(!this.WaitForSync(() => this.IsChainSynced, service => service.SynchronizeBlockchain(false), catcher => this.centralCoordinator.BlockchainSynced += catcher, catcher => this.centralCoordinator.BlockchainSynced -= catcher, "blockchain")) {
				return false;
			}

			return this.WaitForSync(() => this.IsWalletSynced, service => service.SynchronizeWallet(false, true), catcher => this.centralCoordinator.WalletSynced += catcher, catcher => this.centralCoordinator.WalletSynced -= catcher, "wallet");
		}

		private bool WaitForSync(Func<bool> validityCheck, Action<IBlockchainManager> syncAction, Action<Action> register, Action<Action> unregister, string name) {

			if(validityCheck()) {
				return true;
			}

			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

				void Catcher() {
					resetEvent.Set();
				}

				register(Catcher);

				try {
					var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

					blockchainTask.SetAction((service, taskRoutingContext2) => {
						syncAction(service);
					});

					blockchainTask.Caller = null;
					this.centralCoordinator.RouteTask(blockchainTask);

					DateTime timeout = DateTime.Now.AddSeconds(CHAIN_SYNC_WAIT_TIMEOUT);

					while(DateTime.Now < timeout) {

						if(resetEvent.Wait(TimeSpan.FromSeconds(1)) || validityCheck()) {
							break;
						}
					}

					return validityCheck();
				} finally {
					unregister(Catcher);
				}
			}
		}

		private void RequestSync() {

			void Dispatch(Action<IBlockchainManager> syncAction) {
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction((service, taskRoutingContext2) => {
					syncAction(service);
				});

				blockchainTask.Caller = null;
				this.centralCoordinator.RouteTask(blockchainTask);
			}
			if(!this.IsChainSynced) {
				Dispatch(service => service.SynchronizeBlockchain(false));
			}
			else if(!this.IsWalletSynced) {
				Dispatch(service => service.SynchronizeWallet(false, true));
			}
		}

		/// <summary>
		/// this method is called when the blockchain has been synced. we can empty our callback queue when it is.
		/// </summary>
		/// <exception cref="NotImplementedException"></exception>
		protected virtual void OnSyncedEvent() {

			if(!this.IsChainFullySynced) {
				return;
			}

			// if we had any deferred actions, let's run them now
			while(this.callbackQueue.TryDequeue(out Action callback)) {
				try {
					callback?.Invoke();
				} catch {
					// do nothing for now
				}
			}
		}
		
	#endregion

		/// <summary>
		///     here we validate the election, and see if we are elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <returns></returns>
		public virtual List<ElectedCandidateResultDistillate> PerformElectionComputations(BlockElectionDistillate blockElectionDistillate, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager) {
			if(!this.MiningEnabled || (this.factory == null)) {
				return null; // sorry, not happening
			}
			
			if(!this.IsChainFullySynced || this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != blockElectionDistillate.currentBlockId) {
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

				Log.Information($"Block {blockElectionDistillate.currentBlockId} is an {blockElectionDistillate.ElectionContext.ElectionMode} election block and will be mature at height {blockElectionDistillate.currentBlockId + blockElectionDistillate.ElectionContext.Maturity} and published at height {blockElectionDistillate.currentBlockId + blockElectionDistillate.ElectionContext.Maturity + blockElectionDistillate.ElectionContext.Publication}");

				// ok, any transactions in this block must be removed from our election cache. First, sum all the transactions

				// and clear our election cache
				IWalletAccount account = this.WalletProvider.GetActiveAccount();
				this.WalletProvider.RemoveBlockElectionTransactions(blockElectionDistillate.currentBlockId, blockElectionDistillate.BlockTransactionIds.Select(t => new TransactionId(t)).ToList(), account);

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

						var question = (QuestionTransactionSectionDistillate) intermediaryElectionResult.HardQuestion;

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
							electionResultDistillate.MatureElectionContextVersion = electionBlock.ElectionContext.Version;

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
					electionResultDistillate.MatureElectionContextVersion = matureElectionBlock.ElectionContext.Version;

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

									TypeSerializer.Deserialize(block.Hash.Span.Slice(0, 8), out long result);

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
			} catch(Exception ex) {
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
		public virtual List<IElectionCandidacyMessage> PrepareElectionCandidacyMessages(BlockElectionDistillate blockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, IEventPoolProvider chainEventPoolProvider, IBlockchainManager blockchainManager) {
			if(!this.MiningEnabled || this.factory == null) {
				return null; // sorry, not happening
			}

			var messages = new List<IElectionCandidacyMessage>();
			if(!this.IsChainFullySynced || this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != blockElectionDistillate.currentBlockId) {
				// this wont work anymore, we must stop
				return messages;
			}

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

					if(useWeb && this.registrationParameters != null) {

						ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo();

						try {

							Repeater.Repeat(() => {
								string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

								Dictionary<string, object> parameters = null;
								string action = "";

								if(electionResult.ElectionMode == ElectionModes.Active) {
									parameters = matureElectionProcessor.PrepareActiveElectionWebConfirmation(blockElectionDistillate, electionResult, electionsCandidateRegistrationInfo.Password);
									action = "registration/record-active-election";
								} else if(electionResult.ElectionMode == ElectionModes.Passive) {
									parameters = matureElectionProcessor.PreparePassiveElectionWebConfirmation(blockElectionDistillate, electionResult, electionsCandidateRegistrationInfo.Password);
									action = "registration/record-passive-election";
								} else {
									throw new ApplicationException("Invalid election type");
								}

								var result = restUtility.Put(url, action, parameters);

								if(result.Wait(TimeSpan.FromSeconds(20)) && !result.IsFaulted) {

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
						} else if(electionResult.ElectionMode == ElectionModes.Passive) {
							electionConfirmationMessage = matureElectionProcessor.PreparePassiveElectionConfirmationMessage(blockElectionDistillate, electionResult);
						} else {
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