using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx.Synchronous;
using RestSharp;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainMiningStatusProvider : IChainProvider {

		// are we mining currently?  (this is not saved to chain state. we start fresh every time we load the app)
		bool MiningEnabled { get; }
		bool IsValidator { get; }
		bool IsValidatorWindow { get; }
		bool IsValidatorWindowProximity { get; }
		AccountId MiningAccountId { get; }
		IPMode MiningRegistrationIpMode { get; }
		bool MiningAllowed { get; }
		Enums.MiningTiers MiningTier { get; }
		Func<(SafeArrayHandle hash, long difficulty)> ExtraHashLayer { get; set; }
		List<MiningHistoryEntry> GetMiningHistory(int page, int pageSize, byte maxLevel);
		Task<MiningStatisticSet> QueryMiningStatistics(AccountId miningAccountId);
		Task<bool> ClearCachedCredentials();
		long QueryCurrentDifficulty();
		void ClearElectionBlockCache();
		Task<BlockElectionDistillate> PrepareBlockElectionContext(IBlock currentBlock, AccountId miningAccountId, LockContext lockContext);
		void RehydrateBlockElectionContext(BlockElectionDistillate blockElectionDistillate);
		long? AnswerQuestion(ElectionQuestionDistillate question, bool hard);
		Task UpdateAccountMiningCacheExpiration(LockContext lockContext);
		(long? secondTierAnswer, long? digestAnswer, long? firstTierAnswer) AnswerQuestions(IElectionBlockQuestion secondTierQuestion, IElectionDigestQuestion digestQuestion, IElectionBlockQuestion firstTierQuestion, Enums.MiningTiers miningTier);
	}

	public interface IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainMiningStatusProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ElectionProcessorFactory { get; }

		Task EnableMining(AccountId miningAccountId, LockContext lockContext, int tier = 0, AccountId delegateAccountId = null);

		Task DisableMining(LockContext lockContext, Enums.MiningStatus status = Enums.MiningStatus.Unknown);

		Task PerformElection(IBlock currentBlock, Func<List<IElectionCandidacyMessage>, Task> electedCallback, LockContext lockContext);
		Task<List<ElectedCandidateResultDistillate>> PerformElectionComputations(BlockElectionDistillate currentBlockElectionDistillate, LockContext lockContext);
		Task<List<IElectionCandidacyMessage>> PrepareElectionCandidacyMessages(BlockElectionDistillate currentBlockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, LockContext lockContext);
	}

	public class MiningHistoryEntry {
		public readonly List<TransactionId> selectedTransactions = new List<TransactionId>();

		public BlockId blockId { get; set; }
		public BlockchainSystemEventType Message { get; set; }
		public DateTime Time { get; set; } = DateTimeEx.CurrentTime;
		public object[] Parameters { get; set; }
		public ChainMiningProvider.MiningEventLevel Level { get; set; }

		public virtual MiningHistory ToApiHistory() {
			MiningHistory miningHistory = this.CreateApiMiningHistory();

			miningHistory.blockId = this.blockId?.Value ?? 0;
			miningHistory.selectedTransactions.AddRange(this.selectedTransactions.Select(t => t.ToString()));

			miningHistory.Message = this.Message?.Value ?? 0;
			miningHistory.Timestamp = this.Time;
			miningHistory.Level = (byte) this.Level;
			miningHistory.Parameters = this.Parameters;

			return miningHistory;
		}

		public virtual MiningHistory CreateApiMiningHistory() {
			return new MiningHistory();
		}

		public struct MiningHistoryParameters {
			public BlockElectionDistillate blockElectionDistillate { get; set; }
			public BlockId blockId { get; set; }
			public FinalElectionResultDistillate finalElectionResultDistillate { get; set; }
			public BlockchainSystemEventType Message { get; set; }
			public object[] Parameters { get; set; }
			public ChainMiningProvider.MiningEventLevel Level { get; set; }
			public List<TransactionId> selectedTransactions { get; set; }
		}
	}

	public static class ChainMiningProvider {
		public enum MiningEventLevel : byte {
			Level1 = 1,
			Level2 = 2
		}
	}

	/// <summary>
	///     A provider that maintains the required state for mining operations
	/// </summary>
	/// <typeparam name="CHAIN_STATE_DAL"></typeparam>
	/// <typeparam name="CHAIN_STATE_CONTEXT"></typeparam>
	/// <typeparam name="CHAIN_STATE_ENTRY"></typeparam>
	public abstract class ChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     how long to wait
		/// </summary>
		private const int SEND_WORKFLOW_TIMEOUT = 30;

		/// <summary>
		///     amount of seconds to wait for the blockchain to sync before giving up as out of sync
		/// </summary>
		private const int CHAIN_SYNC_WAIT_TIMEOUT = 30;

		public const int MAXIMUM_MINING_EVENT_COUNT = 1000;

		/// <summary>
		///     Queue of events stored for later
		/// </summary>
		protected readonly WrapperConcurrentQueue<Func<Task>> callbackQueue = new WrapperConcurrentQueue<Func<Task>>();

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		protected CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;
		
		/// <summary>
		/// Any extra hashing that we want to add as a layer can be provided here
		/// </summary>
		public Func<(SafeArrayHandle hash, long difficulty)> ExtraHashLayer { get; set; }
		
		/// <summary>
		///     Elections where we are candidates but not elected yet
		/// </summary>
		/// <remarks>
		///     the key is the blockId where the election is to be published. the values are the original blocks in which we
		///     were elected which should result at the key block publication
		/// </remarks>
		private readonly Dictionary<long, List<long>> currentCandidates = new Dictionary<long, List<long>>();

		/// <summary>
		///     Here we store the elections that have yet to come to maturity
		/// </summary>
		protected readonly Dictionary<long, List<BlockElectionDistillate>> electionBlockCache = new Dictionary<long, List<BlockElectionDistillate>>();

		protected readonly IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> factory;

		private readonly object historyLocker = new object();

		protected readonly Queue<MiningHistoryEntry> miningHistory = new Queue<MiningHistoryEntry>();

		protected readonly ITimeService timeService;
		protected bool miningEnabled;
		protected Enums.MiningTiers? miningTierOverride;

		private Func<LockContext, Task> miningPreloadCallback;

		/// <summary>
		///     If we request a mining callback to enable mining when blockchain is ready. AppSettings chain config
		///     EnableMiningPreload must be set to true
		/// </summary>
		private bool miningPreloadRequested;

		public bool IsValidator => this.MiningEnabled && MiningTierUtils.IsFirstOrSecondTier(this.MiningTier);

		public bool IsValidatorWindow => this.IsValidator && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.InAppointmentWindow;
		public bool IsValidatorWindowProximity => this.IsValidator && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.InAppointmentWindowProximity;

		/// <summary>
		/// the IP mode of our registration
		/// </summary>
		public IPMode MiningRegistrationIpMode { get; private set; } = IPMode.Unknown;

		protected BlockchainNetworkingService.MiningRegistrationParameters registrationParameters;

		private ManagedTimer updateMiningRegistrationTimer;

		private ManagedTimer updateMiningStatusTimer;

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

		private async Task<bool> ExistingMiningRegistrationValid(LockContext lockContext) {

			if(this.MiningAccountId == null) {
				return false;
			}

			var miningCache = await this.WalletProvider.GetAccountMiningCache(this.MiningAccountId, lockContext).ConfigureAwait(false);

			if(miningCache != null) {
				return miningCache.LastMiningRegistrationUpdate >= (DateTimeEx.CurrentTime - GlobalsService.MinerTimeoutDelay);
			}

			return false;
		}

		/// <summary>
		///     Is mining allowed on this chain
		/// </summary>
		public abstract bool MiningAllowed { get; }

		/// <summary>
		///     This is NOT saved to the filesystem. we hold it in memory and we start fresh every time we load the daemon
		/// </summary>
		public bool MiningEnabled => this.MiningAllowed && this.miningEnabled;

		public IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ElectionProcessorFactory => this.factory;

		protected abstract IAppointmentValidatorDelegate CreateAppointmentValidatorDelegate();

		public Enums.MiningTiers GetMiningTier(AccountId accountId) {
			if(this.miningTierOverride.HasValue) {
				return this.miningTierOverride.Value;
			}

			return BlockchainUtilities.GetMiningTier(accountId, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight);

		}

		public Enums.MiningTiers MiningTier => this.GetMiningTier(this.MiningAccountId);

		public virtual async Task EnableMining(AccountId miningAccountId, LockContext lockContext, int tier = 0, AccountId delegateAccountId = null) {

			try {
				if(this.MiningEnabled || this.miningPreloadRequested) {

					if(this.MiningAccountId == default(AccountId)) {
						await this.DisableMining(lockContext, Enums.MiningStatus.NotMining).ConfigureAwait(false);
					}

					return;
				}

				IWalletAccount miningWalletAccount = null;

				this.miningTierOverride = null;

				if(tier != 0) {
					this.miningTierOverride = (Enums.MiningTiers) tier;
				}

				if(miningAccountId == default(AccountId)) {

					if(!await this.WalletProvider.IsDefaultAccountPublished(lockContext).ConfigureAwait(false)) {

						const string message = "Failed to mine. The mining account has not yet been fully published. Mining is not yet possible until the account is presented and confirmed on the blockchain.";
						this.CentralCoordinator.Log.Error(message);

						throw new ApplicationException(message);
					}

					miningAccountId = await this.WalletProvider.GetPublicAccountId(lockContext).ConfigureAwait(false);

					if(miningAccountId == default(AccountId)) {
						const string message = "Failed to mine. We could not load the default published account.";
						this.CentralCoordinator.Log.Error(message);

						throw new ApplicationException(message);
					}

					miningWalletAccount = await this.WalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
				} else {

					miningWalletAccount = await this.WalletProvider.GetWalletAccount(miningAccountId, lockContext).ConfigureAwait(false);

					if(miningWalletAccount == null) {
						const string message = "Failed to mine. Account does not exist.";
						this.CentralCoordinator.Log.Error(message);

						throw new ApplicationException(message);
					}

					if(!await this.WalletProvider.IsAccountPublished(miningWalletAccount.AccountCode, lockContext).ConfigureAwait(false)) {
						const string message = "Failed to mine. The mining account has not yet been fully published. Mining is not yet possible until the account is presented and confirmed on the blockchain.";
						this.CentralCoordinator.Log.Error(message);

						throw new ApplicationException(message);
					}
				}

				if(!this.WalletProvider.IsWalletLoaded) {
					const string message = "Failed to mine. A wallet must be loaded to mine.";
					this.CentralCoordinator.Log.Error(message);

					throw new ApplicationException(message);
				}

				if(!this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web) && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					const string message = "Failed to mine. Your must be connected to some peers to mine.";
					this.CentralCoordinator.Log.Error(message);

					throw new ApplicationException(message);
				}

				var requestedMiningTier = this.GetMiningTier(miningAccountId);

				if(MiningTierUtils.IsFirstOrSecondTier(requestedMiningTier) && !this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsConnectable) {
#if !TESTING
					string message2 = $"Mining could not be enabled because we want to mine in the {MiningTierUtils.GetOrdinalName(requestedMiningTier)} tier and our p2p port {this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.P2pPort} is not connectable.";
					this.CentralCoordinator.Log.Error(message2);

					throw new ApplicationException(message2);
#endif
				}

				async Task QueryKeyPassphrase() {
					// now, if the message key is encrypted, we need the passphrase
					if(miningWalletAccount.KeysEncrypted) {

						//ok, we must reqeset it
						await this.WalletProvider.EnsureWalletKeyIsReady(miningWalletAccount.AccountCode, GlobalsService.MESSAGE_KEY_ORDINAL_ID, lockContext).ConfigureAwait(false);
					}
				}

				if(!this.centralCoordinator.IsChainLikelySynchronized) {
					if(!await this.CheckSyncStatus(lockContext).ConfigureAwait(false)) {
						// chain is still not synced, we wither fail, or register for full sync to start the mininig automatically
						async Task Catcher(LockContext lc) {
							try {
								await this.EnableMining(miningAccountId, lc, this.miningTierOverride.HasValue ? (int) this.miningTierOverride.Value : 0, delegateAccountId).ConfigureAwait(false);
							} finally {
								this.miningPreloadRequested = false;

								if(this.miningPreloadCallback != null) {
									this.centralCoordinator.BlockchainSynced -= this.miningPreloadCallback;
									this.miningPreloadCallback = null;
								}
							}
						}

						if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableMiningPreload && !this.miningPreloadRequested) {

							await QueryKeyPassphrase().ConfigureAwait(false);

							this.miningPreloadRequested = true;

							this.miningPreloadCallback = Catcher;
							this.centralCoordinator.BlockchainSynced += this.miningPreloadCallback;

							this.CentralCoordinator.Log.Warning("Mining could not be enabled as the blockchain is not fully synced. A callback has been set and mining will be enabled automatically when the blockchain is fully synced.");

							return;
						}

						string message = "Mining could not be enabled as the blockchain is not yet fully synced.";
						this.CentralCoordinator.Log.Error(message);

						throw new ApplicationException(message);
					}
				}

				await QueryKeyPassphrase().ConfigureAwait(false);

				// start the validator server
				if(MiningTierUtils.IsFirstOrSecondTier(requestedMiningTier)) {

					List<(DateTime appointment, TimeSpan window, int requesterCount)> appointmentWindows = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentWindows().ConfigureAwait(false);

					this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterValidationServer(appointmentWindows, this.CreateAppointmentValidatorDelegate());
				}

				this.MiningAccountId = miningAccountId;
				this.miningEnabled = true;

				try {
					if(this.MiningEnabled) {

						this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.MiningStarted);
						this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningStatusChanged(this.MiningEnabled, Enums.MiningStatus.Mining));

						this.AddMiningHistoryEntry(new MiningHistoryEntry.MiningHistoryParameters {Message = BlockchainSystemEventTypes.Instance.MiningStarted, Level = ChainMiningProvider.MiningEventLevel.Level1});

						this.CentralCoordinator.Log.Information("Mining is now enabled.");

						// make sure we know about IP address changes
						this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IpAddressChanged += this.ChainNetworkingProviderBaseOnIpAddressChanged;

						// create the elector cache wallet file
						await this.WalletProvider.CreateElectionCacheWalletFile(miningWalletAccount, lockContext).ConfigureAwait(false);

						await this.WalletProvider.UpdateMiningStatistics(this.MiningAccountId, this.MiningTier, s => {
							s.BlockStarted = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
						}, t => {
							t.MiningSessions += 1;
						}, lockContext, true).ConfigureAwait(false);

						// register the chain in the network service, so we can answer the IP Validator

						if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.DisableMiningRegistration) {

							this.registrationParameters = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RegisterMiningRegistrationParameters();

							this.registrationParameters.AccountId = miningAccountId;
							this.registrationParameters.DelegateAccountId = delegateAccountId;
							this.registrationParameters.Password = Guid.Empty;
							this.registrationParameters.SecretCode = Guid.Empty;
							this.registrationParameters.Autograph.Entry = null;
							this.registrationParameters.ChainMiningStatusProvider = this;

							// here we can reuse our existing password if its not too old
							if(await this.ExistingMiningRegistrationValid(lockContext).ConfigureAwait(false)) {

								var miningCache = await this.WalletProvider.GetAccountMiningCache(this.MiningAccountId, lockContext).ConfigureAwait(false);

								this.registrationParameters.Password = miningCache.MiningPassword;
								this.registrationParameters.SecretCode = miningCache.MiningSecretCode;
								this.registrationParameters.Autograph.Entry = ByteArray.CreateClone(miningCache.MiningAutograph);

								if(this.registrationParameters.Password == Guid.Empty) {
									// clear it all
									await this.ClearCachedCredentials().ConfigureAwait(false);
								}
							}

							if(this.registrationParameters.Password == Guid.Empty) {
								// generate our random password
								this.registrationParameters.Autograph.Entry = null;

								this.registrationParameters.Password = GlobalRandom.GetNextGuid();
								this.registrationParameters.SecretCode = GlobalRandom.GetNextGuid();
							}

							// ok, now we must register for mining.  if we can, we will try the rest api first, its so much simpler. if we can't, we will publish a message on chain

							bool success = false;
							bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
							bool chain = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);

							if(web) {
								try {
									await this.PerformWebapiRegistration(lockContext).ConfigureAwait(false);

									// start our account update service
									this.StartAccountUpdateController(lockContext);
									this.StartMiningStatusUpdateCheck(lockContext);

									success = true;
								} catch(Exception ex) {

									if(chain) {
										// do not raise an exception, we will try to mine on chain
										this.CentralCoordinator.Log.Error(ex, "Failed to register for mining by webapi.");
									} else {
										throw new ApplicationException("Failed to register for mining by webapi.", ex);
									}
								}
							}

							if(chain && !success) {

								try {

									await this.PerformOnchainRegistration().ConfigureAwait(false);
									success = true;
								} catch(Exception ex) {
									throw new ApplicationException("Failed to register for mining by on chain message.", ex);
								}
							}

							if(success) {

								var miningCache = new WalletAccount.WalletAccountChainStateMiningCache();
								miningCache.MiningPassword = this.registrationParameters.Password;
								miningCache.MiningSecretCode = this.registrationParameters.SecretCode;
								miningCache.MiningAutograph = this.registrationParameters.Autograph?.ToExactByteArrayCopy();
								miningCache.LastMiningRegistrationUpdate = DateTimeEx.CurrentTime;

								await this.WalletProvider.UpdateAccountMiningCache(this.MiningAccountId, miningCache, lockContext).ConfigureAwait(false);
							}
						}
					} else {
						throw new ApplicationException();
					}
				} catch(Exception ex) {
					this.registrationParameters = null;
					this.CentralCoordinator.Log.Error(ex, "Mining is disabled. Impossible to enable mining.");
					await this.DisableMining(lockContext, Enums.MiningStatus.NotMining).ConfigureAwait(false);

					throw;
				}
			} catch(Exception ex) {

				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.Error(this.centralCoordinator.ChainId, ex.Message));
			}

		}

		public virtual async Task DisableMining(LockContext lockContext, Enums.MiningStatus status = Enums.MiningStatus.Unknown) {

			if(!this.MiningEnabled && !this.miningPreloadRequested && (this.miningPreloadCallback == null)) {
				return;
			}

			this.StopAccountUpdateController();

			this.miningEnabled = false;
			this.MiningRegistrationIpMode = IPMode.Unknown;
			this.miningTierOverride = null;

			this.currentCandidates.Clear();

			this.AddMiningHistoryEntry(new MiningHistoryEntry.MiningHistoryParameters {Message = BlockchainSystemEventTypes.Instance.MiningEnded, Level = ChainMiningProvider.MiningEventLevel.Level1, Parameters = new object[] {(byte) status}});

			try {
				// make sure we know about IP address changes
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IpAddressChanged -= this.ChainNetworkingProviderBaseOnIpAddressChanged;
			} catch {

			}

			try {
				await this.WalletProvider.StopSessionMiningStatistics(this.MiningAccountId, lockContext).ConfigureAwait(false);
			} catch(Exception ex) {
				// do nothing if this failed, its not very important
				this.CentralCoordinator.Log.Warning(ex, "Failed to stop mining statistics");
			}

			while(this.callbackQueue.TryDequeue(out Func<Task> callback)) {
				// do nothing, we are clearing.
			}

			if(this.miningPreloadRequested) {
				if(this.miningPreloadCallback != null) {
					this.centralCoordinator.BlockchainSynced += this.miningPreloadCallback;
					this.miningPreloadCallback = null;
				}

				this.miningPreloadRequested = false;
			}

			bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);

			if(web && (this.registrationParameters != null)) {
				try {
					// lets try to release our registration if we can
					await this.PerformWebapiRegistrationStop().ConfigureAwait(false);

				} catch(Exception ex) {
					// do nothing if this failed, its not very important
					this.CentralCoordinator.Log.Warning(ex, "Failed to stop mining through web registration");
				}
			}

			if(MiningTierUtils.IsFirstOrSecondTier(this.MiningTier) && !this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.IsConnectable) {

				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.UnregisterValidationServer();
			}

			this.registrationParameters = null;

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningEnded(status));
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningStatusChanged(this.MiningEnabled, status));

			this.AddMiningHistoryEntry(new MiningHistoryEntry.MiningHistoryParameters {Message = BlockchainSystemEventTypes.Instance.MiningEnded, Level = ChainMiningProvider.MiningEventLevel.Level1});

			this.CentralCoordinator.Log.Information($"Mining is now disabled. Status result: {status}");

			// delete the file from the wallet
			try {
				IWalletAccount miningAccount = await this.WalletProvider.GetWalletAccount(this.MiningAccountId, lockContext).ConfigureAwait(false);
				await this.WalletProvider.DeleteElectionCacheWalletFile(miningAccount, lockContext).ConfigureAwait(false);
			} catch {

			}

			try {
				// remove our network registration
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.UnRegisterMiningRegistrationParameters();
			} catch {

			}
		}

		public virtual List<MiningHistoryEntry> GetMiningHistory(int page, int pageSize, byte maxLevel) {
			List<MiningHistoryEntry> entries = null;

			var query = this.miningHistory.Where(h => (byte) h.Level <= maxLevel).OrderByDescending(h => h.Time);

			if(pageSize != 0) {
				lock(this.historyLocker) {
					return query.Skip(page * pageSize).Take(pageSize).ToList();
				}
			}

			lock(this.historyLocker) {
				return query.ToList();
			}
		}

		public virtual async Task<MiningStatisticSet> QueryMiningStatistics(AccountId miningAccountId) {
			LockContext lockContext = null;
			MiningStatisticSet miningStatisticSet = new MiningStatisticSet();

			if(miningAccountId == null) {
				miningAccountId = this.MiningAccountId;
			}

			if(miningAccountId == null) {
				miningAccountId = (await this.WalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false)).GetAccountId();
			}

			(miningStatisticSet.Session, miningStatisticSet.Aggregate) = await this.WalletProvider.QueryMiningStatistics(miningAccountId, this.MiningTier, lockContext).ConfigureAwait(false);

			return miningStatisticSet;
		}

		/// <summary>
		/// reset the cached credentials
		/// </summary>
		/// <returns></returns>
		public async Task<bool> ClearCachedCredentials() {

			if(this.MiningAccountId != null) {
				try {
					LockContext lockContext = null;
					await this.WalletProvider.UpdateAccountMiningCache(this.MiningAccountId, new WalletAccount.WalletAccountChainStateMiningCache(), lockContext).ConfigureAwait(false);

					return true;
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to clear cached credentials");
				}
			}

			return false;
		}

		public long QueryCurrentDifficulty() {

			//TODO: we need to review this. its not so simple to decide which to take considering the potential overlaps
			BlockElectionDistillate electionDistillate = null;
			List<BlockElectionDistillate> maturityEntries = null;

			var currentMaturityHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;

			if(this.electionBlockCache.ContainsKey(currentMaturityHeight)) {
				maturityEntries = this.electionBlockCache[currentMaturityHeight];

			} else if(this.electionBlockCache.Any()) {
				// lets take latest
				maturityEntries = this.electionBlockCache[this.electionBlockCache.Keys.Max()];
			}

			if(maturityEntries?.Any() ?? false) {
				// just take the highest
				var highest = maturityEntries.Max(e => e.electionBockId);
				electionDistillate = maturityEntries.SingleOrDefault(e => e.electionBockId == highest);
			}

			if(electionDistillate?.ElectionContext != null) {
				if(electionDistillate.ElectionContext.PrimariesBallotingMethod is HashTargetPrimariesBallotingMethod hashTargetPrimariesBallotingMethod) {
					if(hashTargetPrimariesBallotingMethod.MiningTierDifficulties.ContainsKey(this.MiningTier)) {
						return hashTargetPrimariesBallotingMethod.MiningTierDifficulties[this.MiningTier];
					}
				}
			}

			return 0;
		}

		/// <summary>
		///     This is called when our IP address has probably changed. we must update our registration when we do
		/// </summary>
		/// <exception cref="NotImplementedException"></exception>
		private async Task ChainNetworkingProviderBaseOnIpAddressChanged(LockContext lockContext) {

			if(this.miningEnabled) {

				bool web = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);

				if(web) {
					await this.UpdateWebapiAccountRegistration(lockContext).ConfigureAwait(false);
					this.ResetAccountUpdateController(lockContext);
				} else {

					//TODO: this could be made more efficient
					ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo();
					AccountId miningAccountId = electionsCandidateRegistrationInfo.AccountId;
					AccountId delegateAccountId = electionsCandidateRegistrationInfo.DelegateAccountId;

					// ok, we have to restart mining on gossip, by re-registering
					await this.DisableMining(lockContext).ConfigureAwait(false);
					await this.EnableMining(miningAccountId, lockContext, this.miningTierOverride.HasValue ? (int) this.miningTierOverride.Value : 0, delegateAccountId).ConfigureAwait(false);
				}
			}
		}

		protected void StartAccountUpdateController(LockContext lockContext) {

			if(this.updateMiningRegistrationTimer == null) {
				TimeSpan waitTime = GlobalsService.MinerSafeDelay;
				
				this.updateMiningRegistrationTimer = new ManagedTimer(state => this.UpdateWebapiAccountRegistration(lockContext), waitTime, waitTime);
				
				this.updateMiningRegistrationTimer.Start();
			}
		}

		protected void StartMiningStatusUpdateCheck(LockContext lockContext) {
			if((this.updateMiningStatusTimer == null) && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableMiningStatusChecks) {

				TimeSpan span = TimeSpan.FromMinutes(GlobalsService.UPDATE_MINING_STATUS_START_DELAY);
				TimeSpan span2 = TimeSpan.FromMinutes(GlobalsService.UPDATE_MINING_STATUS_DELAY);
				
				this.updateMiningStatusTimer = new ManagedTimer(state => this.CheckMiningStatus(lockContext), span, span2);
				this.updateMiningStatusTimer.Start();
			}

		}

		protected void ResetAccountUpdateController(LockContext lockContext) {
			if(this.StopAccountUpdateController()) {
				this.StartAccountUpdateController(lockContext);
				this.StartMiningStatusUpdateCheck(lockContext);
			}
		}

		protected bool StopAccountUpdateController() {
			bool wasRunning = this.updateMiningRegistrationTimer != null;

			try {
				this.updateMiningRegistrationTimer?.Dispose();
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, $"Failed to dispose {nameof(this.updateMiningRegistrationTimer)}");
			}

			this.updateMiningRegistrationTimer = null;

			try {
				this.updateMiningStatusTimer?.Dispose();
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, $"Failed to dispose {nameof(this.updateMiningStatusTimer)}");
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

			if(action.Value < DateTimeEx.CurrentTime) {
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

			bool addressSet = false;

			try {
				if(!string.IsNullOrWhiteSpace(this.ChainConfiguration.ServerMiningVerificationStaticIp)) {
					NodeAddressInfo nodeAddress = new NodeAddressInfo(this.ChainConfiguration.ServerMiningVerificationStaticIp, null);

					info.Ip = IPUtils.IPtoGuid(nodeAddress.AdjustedAddress);
					addressSet = true;
				}
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, $"Failed to set static ip from configuration. Value provided was {this.ChainConfiguration.ServerMiningVerificationStaticIp}.");
			}

			if(!addressSet && MiningTierUtils.IsFirstOrSecondTier(this.MiningTier)) {
				
				bool ipv4Set = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIpv4 != null;
				bool ipv6Set = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIpv6 != null;
				if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv4) {
					ipv6Set = false;
				} else if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv6) {
					ipv4Set = false;
				}
				
				if(ipv4Set) {
					info.Ip = IPUtils.IPtoGuid(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIpv4);
				} else if(ipv6Set) {
					info.Ip = IPUtils.IPtoGuid(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PublicIpv6);
				} else {
					throw new ApplicationException("our public IP is not set!");
				}
			}

			info.Port = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.P2pPort;
			info.ValidatorPort = GlobalSettings.ApplicationSettings.ValidatorPort;

			info.AccountId = this.registrationParameters.AccountId;
			info.DelegateAccountId = this.registrationParameters.DelegateAccountId;

			info.ChainType = this.centralCoordinator.ChainId;
			info.Password = this.registrationParameters.Password;
			info.SecretCode = this.registrationParameters.SecretCode;
			info.MiningTier = this.MiningTier;

			info.Timestamp = this.timeService.CurrentRealTime;

			return info;
		}

		public async Task UpdateAccountMiningCacheExpiration(LockContext lockContext) {
			WalletAccount.WalletAccountChainStateMiningCache miningCache = await this.WalletProvider.GetAccountMiningCache(this.MiningAccountId, lockContext).ConfigureAwait(false);

			if(miningCache != null) {
				await this.UpdateAccountMiningCacheExpiration(miningCache, lockContext).ConfigureAwait(false);
			}
		}

		protected Task UpdateAccountMiningCacheExpiration(WalletAccount.WalletAccountChainStateMiningCache miningCache, LockContext lockContext) {
			if(miningCache != null) {
				miningCache.LastMiningRegistrationUpdate = DateTimeEx.CurrentTime;

				return this.WalletProvider.UpdateAccountMiningCache(this.MiningAccountId, miningCache, lockContext);
			}

			return Task.CompletedTask;
		}

		/// <summary>
		///     upudate our registration to keep it active by sending our information again, with the very same password.
		///     Chain miners dont need to do this, since they will be contacted directly
		/// </summary>
		protected async Task UpdateWebapiAccountRegistration(LockContext lockContext) {

			if(!this.MiningEnabled || !this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web)) {
				return;
			}

			WalletAccount.WalletAccountChainStateMiningCache miningCache = await this.WalletProvider.GetAccountMiningCache(this.MiningAccountId, lockContext).ConfigureAwait(false);

			if(this.registrationParameters != null && miningCache != null && (miningCache.LastMiningRegistrationUpdate <= (DateTimeEx.CurrentTime - (GlobalsService.MinerSafeDelay - TimeSpan.FromMinutes(2))))) {

				if(miningCache.LastMiningRegistrationUpdate < (DateTimeEx.CurrentTime - GlobalsService.MinerTimeoutDelay)) {
					// have to clear it, we timed out
					await this.ClearCachedCredentials().ConfigureAwait(false);
				} else {
					try {
						await this.PerformWebapiRegistrationUpdate(lockContext).ConfigureAwait(false);

					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, "Failed to update mining registration by webapi.");

						try {
							await this.DisableMining(lockContext, Enums.MiningStatus.NotMining).ConfigureAwait(false);
						} catch(Exception ex2) {
							this.CentralCoordinator.Log.Error(ex2, "Failed to disable mining.");
						}
					}
				}
			}
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task PerformWebapiRegistrationUpdate(LockContext lockContext) {

			ElectionsCandidateRegistrationInfo registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == Guid.Empty) {
				throw new ApplicationException("Failed to update mining registration. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("accountId", registrationInfo.AccountId.ToLongRepresentation());

			if(registrationInfo.DelegateAccountId != default(AccountId)) {
				parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToLongRepresentation());
			}

			parameters.Add("password", registrationInfo.Password);
			parameters.Add("miningTier", (int) registrationInfo.MiningTier);
			
			if(MiningTierUtils.IsFirstOrSecondTier(registrationInfo.MiningTier)) {
				
				if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv4) {
					url = this.ChainConfiguration.WebIpv4ElectionsRegistrationUrl;
				}
				else if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv6) {
					url = this.ChainConfiguration.WebIpv6ElectionsRegistrationUrl;
				}
				
				parameters.Add("secret-code", registrationInfo.SecretCode);
				if(!string.IsNullOrWhiteSpace(this.ChainConfiguration.ServerMiningVerificationStaticIp)) {
					parameters.Add("address", registrationInfo.Ip);
				}
			}

			int wait = 3;

			DateTime absoluteTimeout = DateTime.Now.AddMinutes(5);
			// since this is an important process, we keep trying until we get it, or mining times out
			while(this.miningEnabled && await this.ExistingMiningRegistrationValid(lockContext).ConfigureAwait(false) && DateTime.Now < absoluteTimeout) {
				
				var restParameterSet = new RestUtility.RestParameterSet<IPMode>();
				restParameterSet.parameters = parameters;

				restParameterSet.transform = webResult => {
					if(byte.TryParse(webResult, out byte ipByte)) {
						return (IPMode) ipByte;
					}

					return IPMode.Unknown;
				};
					
				(bool sent, IPMode result) = await restUtility.PerformSecurePut(url, "elections/update-registration", restParameterSet).ConfigureAwait(false);

				if(sent && result != IPMode.Unknown) {
					this.MiningRegistrationIpMode = result;
					
					await this.UpdateAccountMiningCacheExpiration(lockContext).ConfigureAwait(false);

					// we just updated
					this.CentralCoordinator.Log.Verbose("Mining registration was successfully updated.");

					return;
				}

				Thread.Sleep(TimeSpan.FromSeconds(wait));
				wait = Math.Min(wait + 3, 20);
			}

			throw new ApplicationException("Could not update mining registration!");
		}

		/// <summary>
		///     try to unregister a stop through the public webapi interface
		/// </summary>
		protected async Task PerformWebapiRegistrationStop() {

			ElectionsCandidateRegistrationInfo registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == Guid.Empty) {
				throw new ApplicationException("Failed to stop mining registration. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("accountId", registrationInfo.AccountId.ToLongRepresentation());
			parameters.Add("password", registrationInfo.Password);
			
			var restParameterSet = new RestUtility.RestParameterSet<object>();
			restParameterSet.parameters = parameters;

			restParameterSet.transform = webResult => null;
					
			(bool sent, var _) = await restUtility.PerformSecurePost(url, "elections/stop", restParameterSet).ConfigureAwait(false);

			if(sent) {
				LockContext lockContext = null;
				await this.UpdateAccountMiningCacheExpiration(lockContext).ConfigureAwait(false);

				return;
			}

			throw new ApplicationException("Failed to stop mining through web");
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task PerformWebapiRegistration(LockContext lockContext) {

			ElectionsCandidateRegistrationInfo registrationInfo = this.PrepareRegistrationInfo();

			if(registrationInfo.Password == Guid.Empty) {
				throw new ApplicationException("Failed to register for mining. Password was not set.");
			}

			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			if(this.registrationParameters.Autograph.IsZero) {
				using IXmssWalletKey key = await this.WalletProvider.LoadKey<IXmssWalletKey>(GlobalsService.MESSAGE_KEY_NAME, lockContext).ConfigureAwait(false);

				// and sign the whole thing with our key
				SafeArrayHandle password = SafeArrayHandle.Create(16);
				TypeSerializer.Serialize(registrationInfo.Password, password.Span);
				this.registrationParameters.Autograph.Entry = (await this.WalletProvider.SignMessageXmss(password, key, lockContext).ConfigureAwait(false)).Entry;
				this.CentralCoordinator.Log.Verbose("Message successfully signed.");
			}

			registrationInfo.Autograph = this.registrationParameters.Autograph?.ToExactByteArrayCopy();

			if(registrationInfo.Autograph != null) {

				this.CentralCoordinator.Log.Verbose("Message autograph is correctly set.");

				string autograph64 = Convert.ToBase64String(registrationInfo.Autograph);
				string url = this.ChainConfiguration.WebElectionsRegistrationUrl;

				long longAccountId = registrationInfo.AccountId.ToLongRepresentation();

				await Repeater.RepeatAsync(async () => {

					Dictionary<string, object> parameters = new Dictionary<string, object>();
					parameters.Add("accountId", longAccountId);

					if(registrationInfo.DelegateAccountId != default(AccountId)) {
						parameters.Add("delegateAccountId", registrationInfo.DelegateAccountId.ToLongRepresentation());
					}

					parameters.Add("password", registrationInfo.Password);
					
					parameters.Add("autograph", autograph64);
					parameters.Add("miningTier", (int) registrationInfo.MiningTier);

					if(MiningTierUtils.IsFirstOrSecondTier(registrationInfo.MiningTier)) {
						
						if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv4) {
							url = this.ChainConfiguration.WebIpv4ElectionsRegistrationUrl;
						}
						else if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv6) {
							url = this.ChainConfiguration.WebIpv6ElectionsRegistrationUrl;
						}
						
						parameters.Add("secret-code", registrationInfo.SecretCode);
						parameters.Add("port", registrationInfo.Port);
						parameters.Add("validator-port", registrationInfo.ValidatorPort);

						if(!string.IsNullOrWhiteSpace(this.ChainConfiguration.ServerMiningVerificationStaticIp)) {
							parameters.Add("address", registrationInfo.Ip);
						}
					}

					IRestResponse result = await restUtility.Put(url, "elections/register", parameters).ConfigureAwait(false);

					// ok, check the result
					if(result.StatusCode == HttpStatusCode.OK) {

						if(byte.TryParse(result.Content, out byte ipByte)) {
							this.MiningRegistrationIpMode = (IPMode) ipByte;
						}

						await this.UpdateAccountMiningCacheExpiration(lockContext).ConfigureAwait(false);

						// ok, we are not registered. we can await a response from the IP Validator
						return;
					}

					throw new ApplicationException("Failed to register for mining through web");

				}).ConfigureAwait(false);

			} else {
				throw new ApplicationException("Failed to register for mining through web; autograph was null");
			}
		}

		protected async Task CheckMiningStatus(LockContext lockContext) {

			if(this.ChainConfiguration.EnableMiningStatusChecks && (this.registrationParameters != null)) {
				RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
				restUtility.Timeout = TimeSpan.FromSeconds(15);

				string url = this.ChainConfiguration.WebElectionsStatusUrl;
				
				ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo();

				if(electionsCandidateRegistrationInfo.Password == Guid.Empty) {
					throw new ApplicationException("Failed to check mining registration status. Password was not set.");
				}

				Dictionary<string, object> parameters = new Dictionary<string, object>();
				parameters.Add("accountId", electionsCandidateRegistrationInfo.AccountId.ToLongRepresentation());
				parameters.Add("password", electionsCandidateRegistrationInfo.Password);
				parameters.Add("miningTier", (int) electionsCandidateRegistrationInfo.MiningTier);
				
				if(MiningTierUtils.IsFirstOrSecondTier(electionsCandidateRegistrationInfo.MiningTier)) {
					if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv4) {
						url = this.ChainConfiguration.WebIpv4ElectionsStatusUrl;
					}
					else if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv6) {
						url = this.ChainConfiguration.WebIpv6ElectionsStatusUrl;
					}
					
					if(!string.IsNullOrWhiteSpace(this.ChainConfiguration.ServerMiningVerificationStaticIp)) {
						parameters.Add("address", electionsCandidateRegistrationInfo.Ip);
					}
				}
				
				Enums.MiningStatus status = Enums.MiningStatus.NotMining;
				bool sent = false;

				try {

					var restParameterSet = new RestUtility.RestParameterSet<Enums.MiningStatus>();
					restParameterSet.parameters = parameters;

					restParameterSet.transform = webResult => {
						if(int.TryParse(webResult, out int statusNumber)) {
							return (Enums.MiningStatus) statusNumber;
						}

						return Enums.MiningStatus.Unknown;
					};
					
					(sent, status) = await restUtility.PerformSecurePost(url, "elections-states/query-mining-status", restParameterSet).ConfigureAwait(false);

					if(sent) {
						if(status == Enums.MiningStatus.Mining) {
							this.CentralCoordinator.Log.Verbose($"A status check demonstrated that we are mining.");

							// all is fine, we are confirmed as mining.
							return;
						}

						if(status == Enums.MiningStatus.IpUsed) {

							this.CentralCoordinator.Log.Verbose($"A status check demonstrated that our Ip is currently in use.");

							// our IP is already used, nothing to do
							return;
						}

						this.CentralCoordinator.Log.Information($"A status check demonstrated that we are not mining. Status received {status}. We may check again");
					}
					else {
						this.CentralCoordinator.Log.Warning("We could not verify if we are registered for mining. We might be, but we could not verify it.  We may check again.");
					}

					if(status != Enums.MiningStatus.Mining) {
						// we are not mining, lets blow it up
						throw new ApplicationException();
					}
				} catch {
					await this.DisableMining(lockContext, status).ConfigureAwait(false);
				} 
			}
		}

		protected async Task PerformOnchainRegistration() {

			ElectionsCandidateRegistrationInfo registrationInfo = this.PrepareRegistrationInfo();

			// ok, well this is it, we will register for mining on chain
			var chainNetworkingProvider = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase;

			bool ipv4null = false;
			bool ipv6null = false;
			if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv4) {
				ipv4null = chainNetworkingProvider.PublicIpv4 == null;
			}
			else if(this.ChainConfiguration.ServerMiningVerificationIpProtocol == IPMode.IPv6) {
				ipv6null = chainNetworkingProvider.PublicIpv6 == null;
			}
			
			if((ipv4null && ipv6null) || (registrationInfo.Ip == Guid.Empty)) {
				throw new ApplicationException("Our public IP is still undefined. We can not register for mining on chain without an IP address to provide.");
			}

			ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> sendWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateSendElectionsCandidateRegistrationMessageWorkflow(this.MiningAccountId, this.MiningTier, registrationInfo, AppSettingsBase.ContactMethods.Gossip, new CorrelationContext());

			this.centralCoordinator.PostImmediateWorkflow(sendWorkflow);

			await sendWorkflow.Task.ConfigureAwait(false);

			if(!sendWorkflow.IsCompleted) {
				// its taking too much time
				throw new ApplicationException("Sending miner registration message workflow is taking too much time");
			}
		}

		protected void AddMiningHistoryEntry(MiningHistoryEntry.MiningHistoryParameters parameters) {

			try {
				MiningHistoryEntry entry = this.CreateMiningHistoryEntry();

				this.PrepareMiningHistoryEntry(entry, parameters);

				lock(this.historyLocker) {
					this.miningHistory.Enqueue(entry);

					while(this.miningHistory.Count > MAXIMUM_MINING_EVENT_COUNT) {
						this.miningHistory.Dequeue();
					}
				}
			} catch(Exception ex) {

				this.CentralCoordinator.Log.Verbose(ex, "Failed ot add mining history entry");
			}

		}

		protected virtual void PrepareMiningHistoryEntry(MiningHistoryEntry entry, MiningHistoryEntry.MiningHistoryParameters parameters) {

			entry.blockId = parameters.blockId;

			if((entry.blockId == null) && (parameters.blockElectionDistillate != null) && (parameters.finalElectionResultDistillate != null)) {
				entry.blockId = parameters.blockElectionDistillate.electionBockId - parameters.finalElectionResultDistillate.BlockOffset;
			}

			if(parameters.finalElectionResultDistillate != null) {
				entry.selectedTransactions.AddRange(parameters.finalElectionResultDistillate.TransactionIds.Select(t => new TransactionId(t)));
			}

			entry.Message = parameters.Message;
			entry.Level = parameters.Level;
			entry.Parameters = parameters.Parameters;
		}

		protected abstract MiningHistoryEntry CreateMiningHistoryEntry();

	#region Utilities

		public void ClearElectionBlockCache() {
			this.electionBlockCache.Clear();
		}

		protected void AddElectionBlock(BlockElectionDistillate blockElectionDistillate) {

			if(blockElectionDistillate.HasActiveElection) {
				long maturityId = blockElectionDistillate.electionBockId + blockElectionDistillate.ElectionContext.Maturity;

				if(!this.electionBlockCache.ContainsKey(maturityId)) {
					this.electionBlockCache.Add(maturityId, new List<BlockElectionDistillate>());
				}

				List<BlockElectionDistillate> blockEntry = this.electionBlockCache[maturityId];

				if(blockEntry.All(e => e.electionBockId != blockElectionDistillate.electionBockId)) {
					this.electionBlockCache[maturityId].Add(blockElectionDistillate);
				}

				long difficulty = 0;

				if(blockElectionDistillate.ElectionContext.PrimariesBallotingMethod is HashTargetPrimariesBallotingMethod hashTargetPrimariesBallotingMethod) {
					if(hashTargetPrimariesBallotingMethod.MiningTierDifficulties.ContainsKey(this.MiningTier)) {
						difficulty = hashTargetPrimariesBallotingMethod.MiningTierDifficulties[this.MiningTier];
					}
				}

				// alert that we have a new context inserted
				this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ElectionContextCached(this.centralCoordinator.ChainId, blockElectionDistillate.electionBockId, maturityId, difficulty));
			}

			// now clear any obsolete entries
			foreach(long entry in this.electionBlockCache.Keys.Where(k => k < blockElectionDistillate.electionBockId).ToList()) {
				this.electionBlockCache.Remove(entry);
			}
		}

		protected List<BlockElectionDistillate> ObtainMatureActiveElectionBlocks(long maturityBlockId, List<long> matureBlockIds = null) {

			List<BlockElectionDistillate> results = new List<BlockElectionDistillate>();

			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {
				// we only care about active ones
				results.AddRange(this.electionBlockCache[maturityBlockId].Where(b => (b.ElectionContext.ElectionMode == ElectionModes.Active) && (matureBlockIds?.Contains(b.electionBockId) ?? true)));
			}

			return results;
		}

		protected List<BlockElectionDistillate> ObtainMatureElectionBlocks(long maturityBlockId, List<long> matureBlockIds = null) {

			List<BlockElectionDistillate> results = new List<BlockElectionDistillate>();

			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {
				// we only care about active ones
				results.AddRange(this.electionBlockCache[maturityBlockId].Where(b => matureBlockIds?.Contains(b.electionBockId) ?? true));
			}

			return results;
		}

		protected async Task<BlockElectionDistillate> ObtainMatureElectionBlock(long maturityBlockId, long electionBlockId, LockContext lockContext) {
			if(this.electionBlockCache.ContainsKey(maturityBlockId)) {

				BlockElectionDistillate block = this.electionBlockCache[maturityBlockId].SingleOrDefault(b => b.electionBockId == electionBlockId);

				if(block != null) {
					return block;
				}
			}

			// ok we dont have it in cache, lets get it from disk
			return await this.PrepareBlockElectionContext(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock<IBlock>(electionBlockId), this.MiningAccountId, lockContext).ConfigureAwait(false);
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
		public virtual async Task<BlockElectionDistillate> PrepareBlockElectionContext(IBlock currentBlock, AccountId miningAccountId, LockContext lockContext) {

			if(miningAccountId == default(AccountId)) {
				throw new ApplicationException("Invalid mining account Id");
			}

			BlockElectionDistillate blockElectionDistillate = this.CreateBlockElectionContext();

			blockElectionDistillate.MiningAccountId = miningAccountId; 
			blockElectionDistillate.electionBockId = currentBlock.BlockId.Value;
			blockElectionDistillate.blockHash.Entry = currentBlock.Hash.Entry;
			blockElectionDistillate.blockType = currentBlock.Version;

			foreach(IIntermediaryElectionResults entry in currentBlock.IntermediaryElectionResults) {

				IntermediaryElectionContextDistillate intermediateElectionContext = blockElectionDistillate.CreateIntermediateElectionContext();
				intermediateElectionContext.BlockOffset = entry.BlockOffset;

				// now the questions

				//TODO: this needs major cleaning
				intermediateElectionContext.SecondTierQuestion = PrepareBlockQuestionDistillate(entry.SecondTierQuestion);
				intermediateElectionContext.DigestQuestion = PrepareDigestQuestionDistillate(entry.DigestQuestion);
				intermediateElectionContext.FirstTierQuestion = PrepareBlockQuestionDistillate(entry.FirstTierQuestion);

				if(entry is IPassiveIntermediaryElectionResults simplepassiveIntermediaryElectionResults) {

					if(simplepassiveIntermediaryElectionResults.ElectedCandidates.ContainsKey(miningAccountId)) {
						// thats us!!
						intermediateElectionContext.PassiveElectionContextDistillate = blockElectionDistillate.CreatePassiveElectionContext();

						await this.PreparePassiveElectionContext(currentBlock.BlockId.Value, miningAccountId, intermediateElectionContext.PassiveElectionContextDistillate, simplepassiveIntermediaryElectionResults, currentBlock, lockContext).ConfigureAwait(false);
					}
				}

				blockElectionDistillate.IntermediaryElectionResults.Add(intermediateElectionContext);
			}

			foreach(IFinalElectionResults entry in currentBlock.FinalElectionResults) {

				if(entry is IFinalElectionResults simpleFinalElectionResults) {

					if(simpleFinalElectionResults.ElectedCandidates.ContainsKey(miningAccountId)) {
						// thats us!!
						FinalElectionResultDistillate finalResultDistillateEntry = blockElectionDistillate.CreateFinalElectionResult();
						await this.PrepareFinalElectionContext(currentBlock.BlockId.Value, miningAccountId, finalResultDistillateEntry, simpleFinalElectionResults, currentBlock, lockContext).ConfigureAwait(false);

						blockElectionDistillate.FinalElectionResults.Add(finalResultDistillateEntry);
					}
				}
			}

			// we have them, so let's set them ehre
			blockElectionDistillate.BlockTransactionIds.AddRange(currentBlock.GetAllTransactions().Select(t => t.ToString()));

			if(currentBlock is IElectionBlock currentElection && (currentElection.ElectionContext != null)) {

				blockElectionDistillate.ElectionContext = currentElection.ElectionContext;
			}

			return blockElectionDistillate;
		}

		protected ElectionBlockQuestionDistillate PrepareBlockQuestionDistillate(IElectionBlockQuestion question) {
			if(question == null) {
				return null;
			}

			if(question is IBlockTransactionIdElectionQuestion blockTransactionIdElectionQuestion) {
				BlockTransactionSectionQuestionDistillate distillate = new BlockTransactionSectionQuestionDistillate();

				distillate.BlockId = blockTransactionIdElectionQuestion.BlockId;

				distillate.TransactionIndex = (int?) blockTransactionIdElectionQuestion.TransactionIndex?.Value;

				distillate.SelectedTransactionSection = (byte) blockTransactionIdElectionQuestion.SelectedTransactionSection;
				distillate.SelectedComponent = (byte) blockTransactionIdElectionQuestion.SelectedComponent;

				return distillate;
			}

			if(question is IBlockBytesetElectionQuestion bytesetElectionQuestion) {
				BlockBytesetQuestionDistillate distillate = new BlockBytesetQuestionDistillate();

				distillate.BlockId = bytesetElectionQuestion.BlockId;

				distillate.Offset = (int) bytesetElectionQuestion.Offset.Value;
				distillate.Length = bytesetElectionQuestion.Length;

				return distillate;
			}

			return null;
		}

		protected ElectionDigestQuestionDistillate PrepareDigestQuestionDistillate(IElectionDigestQuestion question) {
			if(question == null) {
				return null;
			}

			if(question is IDigestBytesetElectionQuestion digestBytesetElectionQuestion) {
				DigestBytesetQuestionDistillate distillate = new DigestBytesetQuestionDistillate();

				distillate.DigestID = (int) digestBytesetElectionQuestion.DigestId;

				distillate.Offset = (int) digestBytesetElectionQuestion.Offset.Value;
				distillate.Length = digestBytesetElectionQuestion.Length;

				return distillate;
			}

			return null;
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

				blockElectionDistillate.blockHash.Entry = ByteArray.FromBase58(blockElectionDistillate.blockHashSerialized);
			}

			if((blockElectionDistillate.blockType == (ComponentVersion<BlockType>) null) || blockElectionDistillate.blockType.IsNull) {
				if(string.IsNullOrWhiteSpace(blockElectionDistillate.blockTypeString)) {
					throw new ApplicationException("Invalid block type");
				}

				blockElectionDistillate.blockType = new ComponentVersion<BlockType>(blockElectionDistillate.blockTypeString);
			}

			blockElectionDistillate.MiningAccountId = miningAccountId;
			blockElectionDistillate.blockxxHash = BlockchainHashingUtils.BlockxxHash(blockElectionDistillate);

			this.RehydrateBlockElectionContext(blockElectionDistillate);
		}

		protected virtual async Task PreparePassiveElectionContext(long currentBlockId, AccountId miningAccountId, PassiveElectionContextDistillate passiveElectionContextDistillate, IPassiveIntermediaryElectionResults passiveIntermediaryElectionResults, IBlock currentBlock, LockContext lockContext) {

			passiveElectionContextDistillate.electionBlockId = currentBlockId - passiveIntermediaryElectionResults.BlockOffset;

			// let's see in which tier we have been placed. should correlate with our reported tier when we registered for mining
			passiveElectionContextDistillate.MiningTier = passiveIntermediaryElectionResults.ElectedCandidates[miningAccountId];

			long electionBlockId = currentBlockId - passiveIntermediaryElectionResults.BlockOffset;

			BlockElectionDistillate electionBlock = (await this.ObtainMatureElectionBlock(currentBlockId, electionBlockId, lockContext).ConfigureAwait(false));

		}

		protected virtual async Task PrepareFinalElectionContext(long currentBlockId, AccountId miningAccountId, FinalElectionResultDistillate finalResultDistillateEntry, IFinalElectionResults finalElectionResult, IBlock currentBlock, LockContext lockContext) {

			await this.SetFinalElectedContextStatistics(finalResultDistillateEntry, currentBlockId, lockContext).ConfigureAwait(false);

			finalResultDistillateEntry.BlockOffset = finalElectionResult.BlockOffset;

			Dictionary<AccountId, IElectedResults> allElectedCandidates = finalElectionResult.ElectedCandidates;

			if(allElectedCandidates.ContainsKey(miningAccountId)) {
				finalResultDistillateEntry.TransactionIds.AddRange(allElectedCandidates[miningAccountId].Transactions.Select(t => t.ToString()));
				finalResultDistillateEntry.DelegateAccountId = allElectedCandidates[miningAccountId].DelegateAccountId?.ToString();
			}

			long electionBlockId = currentBlockId - finalElectionResult.BlockOffset;

		}

		protected virtual async Task SetFinalElectedContextStatistics(FinalElectionResultDistillate finalResultDistillateEntry, long currentBlockId, LockContext lockContext) {

			await this.WalletProvider.UpdateMiningStatistics(this.MiningAccountId, this.MiningTier, s => {
				this.UpdateWalletSessionStatistics(s, finalResultDistillateEntry, currentBlockId, lockContext);
			}, t => {
				this.UpdateWalletTotalStatistics(t, finalResultDistillateEntry, currentBlockId, lockContext);
			}, lockContext).ConfigureAwait(false);

		}

		protected virtual void UpdateWalletSessionStatistics(WalletElectionsMiningSessionStatistics miningTotalStatistics, FinalElectionResultDistillate finalResultDistillateEntry, long currentBlockId, LockContext lockContext) {
			miningTotalStatistics.LastBlockElected = Math.Max(miningTotalStatistics.LastBlockElected, currentBlockId);
			miningTotalStatistics.BlocksElected += 1;
		}

		protected virtual void UpdateWalletTotalStatistics(WalletElectionsMiningAggregateStatistics miningAggregateStatistics, FinalElectionResultDistillate finalResultDistillateEntry, long currentBlockId, LockContext lockContext) {
			miningAggregateStatistics.LastBlockElected = Math.Max(miningAggregateStatistics.LastBlockElected, currentBlockId);
			miningAggregateStatistics.BlocksElected += 1;
		}

		protected abstract BlockElectionDistillate CreateBlockElectionContext();

		/// <summary>
		///     here we perform the entire election in a single shot since we have the block to read the transactions
		/// </summary>
		/// <param name="currentBlock"></param>
		/// <param name="chainEventPoolProvider"></param>
		/// <returns></returns>
		public virtual async Task PerformElection(IBlock currentBlock, Func<List<IElectionCandidacyMessage>, Task> electedCallback, LockContext lockContext) {

			async Task Callback() {

				// this check is important, in case we are running in deferred mode, we could be out of date, and thus moot.
				if(!await this.IsChainFullySynced(lockContext).ConfigureAwait(false) || (this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != currentBlock.BlockId)) {
					// this wont work anymore, we must stop
					return;
				}

				BlockElectionDistillate currentBlockElectionDistillate = await this.PrepareBlockElectionContext(currentBlock, this.MiningAccountId, lockContext).ConfigureAwait(false);

				if(!currentBlockElectionDistillate.HasActiveElection) {

					if(this.MiningEnabled) {
						// let's alert the user that mining wont work
						this.centralCoordinator.PostSystemEvent(SystemEventGenerator.RaiseMessage(ReportableMessageTypes.Instance.BLOCK_HAS_NO_ELECTION, $"Block {currentBlock.BlockId} has no election context and will hold no election"));
					}
					return;
				}
				List<ElectedCandidateResultDistillate> electionResults = await this.PerformElectionComputations(currentBlockElectionDistillate, lockContext).ConfigureAwait(false);

				if(electionResults == null || !electionResults.Any()) {
					return;
				}

				// select transactions
				foreach(ElectedCandidateResultDistillate result in electionResults) {

					// select transactions for this election
					result.SelectedTransactionIds.AddRange((await this.SelectTransactions(currentBlockElectionDistillate.electionBockId, result, lockContext).ConfigureAwait(false)).Select(t => t.ToString()));
				}

				List<IElectionCandidacyMessage> messages = await this.PrepareElectionCandidacyMessages(currentBlockElectionDistillate, electionResults, lockContext).ConfigureAwait(false);

				if(electedCallback != null && messages.Any()) {
					await electedCallback(messages).ConfigureAwait(false);
				}
			}

			if(await this.IsChainFullySynced(lockContext).ConfigureAwait(false)) {
				// we are all good, we can run it now
				await Callback().ConfigureAwait(false);
			} else {
				// store it for later and hope t will run in time
				this.callbackQueue.Enqueue(Callback);
				await this.centralCoordinator.RequestFullSync(lockContext, true).ConfigureAwait(false);
			}
		}

		public async Task<List<TransactionId>> SelectTransactions(BlockId currentBlockId, ElectedCandidateResultDistillate resultDistillate, LockContext lockContext) {

			BlockElectionDistillate matureElectionBlock = await this.ObtainMatureElectionBlock(currentBlockId.Value, resultDistillate.BlockId, lockContext).ConfigureAwait(false);

			if(matureElectionBlock != null) {
				IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(resultDistillate, this.centralCoordinator, this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider);

				// select transactions for this election
				return await matureElectionProcessor.SelectTransactions(currentBlockId.Value, matureElectionBlock, lockContext).ConfigureAwait(false);
			}

			return new List<TransactionId>();
		}

	#region sync status checking

		private async Task<bool> IsChainFullySynced(LockContext lockContext) {
			return this.IsChainSynced && await this.IsWalletSynced(lockContext).ConfigureAwait(false);
		}

		private bool IsChainSynced => this.centralCoordinator.IsChainLikelySynchronized;

		private Task<bool> IsWalletSynced(LockContext lockContext) {
			return this.centralCoordinator.IsWalletSynced(lockContext);
		}

		protected virtual async Task<bool> CheckSyncStatus(LockContext LockContext) {

			if(!await this.WaitForSync(lc => Task.FromResult(this.IsChainSynced), (service, lc) => service.SynchronizeBlockchain(false, lc), catcher => this.centralCoordinator.BlockchainSynced += catcher, catcher => this.centralCoordinator.BlockchainSynced -= catcher, "blockchain", LockContext).ConfigureAwait(false)) {
				return false;
			}

			return await this.WaitForSync(lc => this.IsWalletSynced(lc), (service, lc) => service.SynchronizeWallet(false, lc, true), catcher => this.centralCoordinator.WalletSynced += catcher, catcher => this.centralCoordinator.WalletSynced -= catcher, "wallet", LockContext).ConfigureAwait(false);
		}

		private async Task<bool> WaitForSync(Func<LockContext, Task<bool>> validityCheck, Action<IBlockchainManager, LockContext> syncAction, Action<Func<LockContext, Task>> register, Action<Func<LockContext, Task>> unregister, string name, LockContext lockContext) {

			if(await validityCheck(lockContext).ConfigureAwait(false)) {
				return true;
			}

			using AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);

			Task Catcher(LockContext lc) {
				resetEvent.Set();

				return Task.CompletedTask;
			}

			register(Catcher);

			try {
				BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
					syncAction(service, lc);
				});

				blockchainTask.Caller = null;
				await this.centralCoordinator.RouteTask(blockchainTask).ConfigureAwait(false);

				DateTime timeout = DateTime.Now.AddSeconds(CHAIN_SYNC_WAIT_TIMEOUT);

				while(DateTime.Now < timeout) {

					if(await resetEvent.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false) || await validityCheck(lockContext).ConfigureAwait(false)) {
						break;
					}
				}

				return await validityCheck(lockContext).ConfigureAwait(false);
			} finally {
				unregister(Catcher);
			}

		}

		/// <summary>
		///     this method is called when the blockchain has been synced. we can empty our callback queue when it is.
		/// </summary>
		/// <param name="lockContext"></param>
		/// <exception cref="NotImplementedException"></exception>
		protected virtual async Task OnSyncedEvent(LockContext lockContext) {

			if(!this.callbackQueue.Any() || !await this.IsChainFullySynced(lockContext).ConfigureAwait(false)) {
				return;
			}

			// if we had any deferred actions, let's run them now
			while(this.callbackQueue.TryDequeue(out Func<Task> callback)) {
				try {
					if(callback != null) {
						await callback().ConfigureAwait(false);
					}
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
		public virtual async Task<List<ElectedCandidateResultDistillate>> PerformElectionComputations(BlockElectionDistillate currentBlockElectionDistillate, LockContext lockContext) {
			if(!this.MiningEnabled || (this.factory == null)) {
				return null; // sorry, not happening
			}

			//If we are not in syncless mode, we needs to be fully synced before mining. Syncless mode is always considered fully synced for mining.
			if((!GlobalSettings.ApplicationSettings.SynclessMode && !await this.IsChainFullySynced(lockContext).ConfigureAwait(false)) || (this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != currentBlockElectionDistillate.electionBockId)) {
				return null; // sorry, not happening, we can not mine while we are not synced
			}

			List<ElectedCandidateResultDistillate> electionResults = new List<ElectedCandidateResultDistillate>();

			this.PrepareBlockElectionContext(currentBlockElectionDistillate, this.MiningAccountId);

			//ok, here is how we proceed
			//1. if we have an election block
			//	1.1 remove any confirmed transactions from our election cache
			//	1.2 if there are passive elections, see if we are part of them
			//2. if there are mature blocks, lets participate in the election

			// if it is not an election block, it will be null, which is fine

			if(currentBlockElectionDistillate.HasActiveElection) {
				// first thing, record this block in our cache so that we are ready to process it when it reaches maturity
				this.AddElectionBlock(currentBlockElectionDistillate);

				await this.WalletProvider.UpdateMiningStatistics(this.MiningAccountId, this.MiningTier, s => {
					s.BlocksProcessed += 1;
				}, t => {
					t.BlocksProcessed += 1;
				}, lockContext).ConfigureAwait(false);

				this.CentralCoordinator.Log.Information($"Block {currentBlockElectionDistillate.electionBockId} is an {currentBlockElectionDistillate.ElectionContext.ElectionMode} election block and will be mature at height {currentBlockElectionDistillate.electionBockId + currentBlockElectionDistillate.ElectionContext.Maturity} and published at height {currentBlockElectionDistillate.electionBockId + currentBlockElectionDistillate.ElectionContext.Maturity + currentBlockElectionDistillate.ElectionContext.Publication}");

				// ok, any transactions in this block must be removed from our election cache. First, sum all the transactions

				// and clear our election cache
				IWalletAccount account = await this.WalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
				await this.WalletProvider.RemoveBlockElectionTransactions(currentBlockElectionDistillate.electionBockId, currentBlockElectionDistillate.BlockTransactionIds.Select(t => new TransactionId(t)).ToList(), account, lockContext).ConfigureAwait(false);

			}

			// now, lets check if we are part of any confirmed election results
			if(currentBlockElectionDistillate.FinalElectionResults.Any()) {

				foreach(FinalElectionResultDistillate finalElectionResult in currentBlockElectionDistillate.FinalElectionResults) {

					await this.ConfirmedPrimeElected(currentBlockElectionDistillate, finalElectionResult).ConfigureAwait(false);
				}
			}

			// now the ones where we were candidates but did not become prime elected
			if(this.currentCandidates.ContainsKey(currentBlockElectionDistillate.electionBockId)) {
				List<long> electedEntries = currentBlockElectionDistillate.FinalElectionResults.Select(r => currentBlockElectionDistillate.electionBockId - r.BlockOffset).ToList();

				foreach(long missed in this.currentCandidates[currentBlockElectionDistillate.electionBockId].Where(b => !electedEntries.Contains(b))) {

					await this.PrimeElectedMissed(currentBlockElectionDistillate, missed).ConfigureAwait(false);
				}
			}

			// now, lets check if we are part of any passive elections
			if(currentBlockElectionDistillate.IntermediaryElectionResults.Any()) {

				foreach(IntermediaryElectionContextDistillate intermediaryElectionResult in currentBlockElectionDistillate.IntermediaryElectionResults) {

					if(intermediaryElectionResult.PassiveElectionContextDistillate != null) {

						// ok, get the cached context
						BlockElectionDistillate electionBlock = await this.ObtainMatureElectionBlock(currentBlockElectionDistillate.electionBockId, intermediaryElectionResult.PassiveElectionContextDistillate.electionBlockId, lockContext).ConfigureAwait(false);

						if(electionBlock != null) {

							// we are elected in this passive election block!
							ElectedCandidateResultDistillate electionResultDistillate = this.CreateElectedCandidateResult();

							electionResultDistillate.BlockId = electionBlock.electionBockId;
							electionResultDistillate.MaturityBlockId = currentBlockElectionDistillate.electionBockId;

							electionResultDistillate.ElectionMode = ElectionModes.Passive;

							electionResultDistillate.MaturityBlockHash = currentBlockElectionDistillate.blockxxHash;
							electionResultDistillate.MatureBlockType = electionBlock.blockType;
							electionResultDistillate.MatureElectionContextVersion = electionBlock.ElectionContext.Version;

							// let's use what we had reported, otherwise we risk getting culled for lieing.
							electionResultDistillate.MiningTier = intermediaryElectionResult.PassiveElectionContextDistillate.MiningTier;

							// answer the questions now
							this.AnswerQuestions(electionResultDistillate, intermediaryElectionResult);

							this.AddCurrentCandidateEntry(electionBlock);

							MiningHistoryEntry.MiningHistoryParameters parameters = new MiningHistoryEntry.MiningHistoryParameters();
							parameters.blockElectionDistillate = currentBlockElectionDistillate;
							parameters.blockId = electionBlock.electionBockId;
							parameters.Message = BlockchainSystemEventTypes.Instance.MiningElected;
							parameters.Level = ChainMiningProvider.MiningEventLevel.Level2;
							parameters.Parameters = new object[] {currentBlockElectionDistillate.electionBockId};

							this.AddMiningHistoryEntry(parameters);

							this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningElected(electionBlock.electionBockId));
							this.CentralCoordinator.Log.Information($"We are elected in Block {currentBlockElectionDistillate.electionBockId}");

							electionResults.Add(electionResultDistillate);
						}
					}
				}

				// remove elections that are now obsolete
				foreach(long obsolete in this.currentCandidates.Keys.Where(b => b <= currentBlockElectionDistillate.electionBockId)) {
					this.currentCandidates.Remove(obsolete);
				}
			}

			// now, see if we have any elections that are mature now
			List<BlockElectionDistillate> matureElectionBlocks = this.ObtainMatureActiveElectionBlocks(currentBlockElectionDistillate.electionBockId);

			// ok, lets run the elections that are due right now!
			foreach(BlockElectionDistillate matureElectionBlockDistillate in matureElectionBlocks) {

				Enums.MiningTiers miningTier = this.MiningTier;

				if(!matureElectionBlockDistillate.ElectionContext.MiningTiers.Contains(miningTier)) {
					this.CentralCoordinator.Log.Information($"We have a mature election block with original Id {matureElectionBlockDistillate.electionBockId} but we can not participate as our mining tier {miningTier} is not enabled.");

					continue;
				}

				this.CentralCoordinator.Log.Information($"We have a mature election block with original Id {matureElectionBlockDistillate.electionBockId}");
				IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(matureElectionBlockDistillate, this.centralCoordinator, this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider);

				// set the extra hash layer
				matureElectionProcessor.ExtraHashLayer = this.ExtraHashLayer;
				
				ElectedCandidateResultDistillate electionResultDistillate = matureElectionProcessor.PerformActiveElection(matureElectionBlockDistillate, currentBlockElectionDistillate, this.MiningAccountId, miningTier);

				if(electionResultDistillate != null) {

					// we are elected in this active election!

					electionResultDistillate.MatureBlockType = matureElectionBlockDistillate.blockType;
					electionResultDistillate.MatureElectionContextVersion = matureElectionBlockDistillate.ElectionContext.Version;

					// answer the questions now

					IntermediaryElectionContextDistillate intermediaryElectionResult = currentBlockElectionDistillate.IntermediaryElectionResults.SingleOrDefault(r => (electionResultDistillate.MaturityBlockId - r.BlockOffset) == electionResultDistillate.BlockId);
					this.AnswerQuestions(electionResultDistillate, intermediaryElectionResult);

					this.AddCurrentCandidateEntry(matureElectionBlockDistillate);

					MiningHistoryEntry.MiningHistoryParameters parameters = new MiningHistoryEntry.MiningHistoryParameters();
					parameters.blockElectionDistillate = currentBlockElectionDistillate;
					parameters.blockId = matureElectionBlockDistillate.electionBockId;
					parameters.Message = BlockchainSystemEventTypes.Instance.MiningElected;
					parameters.Level = ChainMiningProvider.MiningEventLevel.Level2;
					parameters.Parameters = new object[] {currentBlockElectionDistillate.electionBockId};

					this.AddMiningHistoryEntry(parameters);
					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningElected(matureElectionBlockDistillate.electionBockId));
					this.CentralCoordinator.Log.Information($"We are elected in Block {currentBlockElectionDistillate.electionBockId}");

					electionResults.Add(electionResultDistillate);
				}
			}

			// alert that we completed a mining cycle
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ElectionProcessingCompleted(this.centralCoordinator.ChainId, currentBlockElectionDistillate.electionBockId, electionResults.Count));

			return electionResults;
		}

		protected void AddCurrentCandidateEntry(BlockElectionDistillate matureElectionBlock) {

			long publicationBlockId = matureElectionBlock.electionBockId + matureElectionBlock.ElectionContext.Maturity + matureElectionBlock.ElectionContext.Publication;

			if(!this.currentCandidates.ContainsKey(publicationBlockId)) {
				this.currentCandidates.Add(publicationBlockId, new List<long>());
			}

			this.currentCandidates[publicationBlockId].Add(matureElectionBlock.electionBockId);
		}

		/// <summary>
		///     answer any election question we can answer
		/// </summary>
		/// <param name="electionResultDistillate"></param>
		/// <param name="intermediaryElectionResult"></param>
		protected void AnswerQuestions(ElectedCandidateResultDistillate electionResultDistillate, IntermediaryElectionContextDistillate intermediaryElectionResult) {
			if(intermediaryElectionResult == null) {
				return;
			}

			var results = this.AnswerQuestions(intermediaryElectionResult.SecondTierQuestion, intermediaryElectionResult.DigestQuestion, intermediaryElectionResult.FirstTierQuestion, electionResultDistillate.MiningTier);

			electionResultDistillate.secondTierAnswer = results.secondTierAnswer;
			electionResultDistillate.digestAnswer = results.digestAnswer;
			electionResultDistillate.firstTierAnswer = results.firstTierAnswer;
		}

		public (long? secondTierAnswer, long? digestAnswer, long? firstTierAnswer) AnswerQuestions(IElectionBlockQuestion secondTierQuestion, IElectionDigestQuestion digestQuestion, IElectionBlockQuestion firstTierQuestion, Enums.MiningTiers miningTier) {

			var secondTierQuestionDistillate = PrepareBlockQuestionDistillate(secondTierQuestion);
			var digestQuestionDistillate = PrepareDigestQuestionDistillate(digestQuestion);
			var firstTierQuestionDistillate = PrepareBlockQuestionDistillate(firstTierQuestion);

			return AnswerQuestions(secondTierQuestionDistillate, digestQuestionDistillate, firstTierQuestionDistillate, miningTier);
		}

		protected (long? secondTierAnswer, long? digestAnswer, long? firstTierAnswer) AnswerQuestions(ElectionBlockQuestionDistillate secondTierQuestion, ElectionDigestQuestionDistillate digestQuestion, ElectionBlockQuestionDistillate firstTierQuestion, Enums.MiningTiers miningTier) {

			long? secondTierAnswer = null;
			long? digestAnswer = null;
			long? firstTierAnswer = null;

			if(MiningTierUtils.IsSecondTier(miningTier)) {
				secondTierAnswer = this.AnswerQuestion(secondTierQuestion, false);
				digestAnswer = this.AnswerQuestion(digestQuestion, false);
			}

			if(MiningTierUtils.IsFirstTier(miningTier)) {
				firstTierAnswer = this.AnswerQuestion(firstTierQuestion, true);
			}

			return (secondTierAnswer, digestAnswer, firstTierAnswer);
		}

		public long? AnswerQuestion(ElectionQuestionDistillate question, bool hard) {

			if(GlobalSettings.ApplicationSettings.SynclessMode || (question == null)) {
				return null;
			}

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(!BlockchainUtilities.UsesBlocks(chainConfiguration.BlockSavingMode)) {
				return null;
			}

			if(hard && BlockchainUtilities.UsesPartialBlocks(chainConfiguration.BlockSavingMode) && (this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight > 0)) {
				return null;
			}

			long? answer = null;

			try {
				if(question is BlockTransactionSectionQuestionDistillate questionTransactionSectionDistillate) {
					//TODO: this needs much refining
					IBlock block = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlock(questionTransactionSectionDistillate.BlockId);

					if(block != null) {
						answer = 0;

						BlockTransactionIdElectionQuestion.QuestionTransactionSection selectedTransactionSection = (BlockTransactionIdElectionQuestion.QuestionTransactionSection) questionTransactionSectionDistillate.SelectedTransactionSection;
						BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents selectedComponent = (BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents) questionTransactionSectionDistillate.SelectedComponent;

						List<TransactionId> transactionIds = new List<TransactionId>();

						switch(selectedTransactionSection) {
							case BlockTransactionIdElectionQuestion.QuestionTransactionSection.Block:

								switch(selectedComponent) {
									case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Hash:

										TypeSerializer.Deserialize(block.Hash.Span.Slice(0, 8), out long result);

										answer = result;

										break;
									case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.BlockTimestamp:
										answer = block.Timestamp.Value;

										break;
								}

								break;
							case BlockTransactionIdElectionQuestion.QuestionTransactionSection.ConfirmedIndexedTransactions:
								transactionIds = block.ConfirmedIndexedTransactions.Select(e => e.TransactionId).ToList();

								break;
							case BlockTransactionIdElectionQuestion.QuestionTransactionSection.ConfirmedTransactions:
								transactionIds = block.ConfirmedTransactions.Select(e => e.TransactionId).ToList();

								break;
							case BlockTransactionIdElectionQuestion.QuestionTransactionSection.RejectedTransactions:
								transactionIds = block.RejectedTransactions.Select(e => e.TransactionId).ToList();

								break;
						}

						if(transactionIds?.Any() ?? false) {

							TransactionId transactionId = transactionIds[questionTransactionSectionDistillate.TransactionIndex.Value];

							switch(selectedComponent) {
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.AccountId:
									answer = transactionId.Account.SequenceId;

									break;
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Timestamp:
									answer = transactionId.Timestamp.Value;

									break;
								case BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents.Scope:
									answer = transactionId.Scope.Value;

									break;
							}
						}
					}
				}

				if(question is BlockBytesetQuestionDistillate bytesetDistillate) {
					//TODO: this needs much refining
					SafeArrayHandle bytes = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockPartialHighHeaderData(bytesetDistillate.BlockId, bytesetDistillate.Offset, bytesetDistillate.Length);

					if(bytes != null) {
						answer = HashingUtils.XxHash64(bytes);
					}
				}

				if(question is DigestBytesetQuestionDistillate digestBytesetDistillate) {
					//TODO: this needs much refining
					throw new NotImplementedException();

					//var bytes = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockPartialHighHeaderData(bytesetDistillate.BlockId, bytesetDistillate.Offset, bytesetDistillate.Length);

					// if(bytes != null) {
					// 	answer = HashingUtils.XxHash64(bytes);
					// }
				}
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to answer mining question");
			}

			return answer;
		}

		protected abstract ElectedCandidateResultDistillate CreateElectedCandidateResult();

		/// <summary>
		///     here we validate the election, and see if we are elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <returns></returns>
		public virtual async Task<List<IElectionCandidacyMessage>> PrepareElectionCandidacyMessages(BlockElectionDistillate currentBlockElectionDistillate, List<ElectedCandidateResultDistillate> electionResults, LockContext lockContext) {
			if(!this.MiningEnabled || (this.factory == null)) {
				return null; // sorry, not happening
			}

			List<IElectionCandidacyMessage> messages = new List<IElectionCandidacyMessage>();

			if(!electionResults.Any()) {
				return messages;
			}

			if((!GlobalSettings.ApplicationSettings.SynclessMode && !await this.IsChainFullySynced(lockContext).ConfigureAwait(false)) || (this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != currentBlockElectionDistillate.electionBockId)) {
				// this wont work anymore, we must stop
				return messages;
			}

			RestUtility restUtility = null;

			//ok, here is how we proceed
			//1. if we have an election block
			//	1.1 remove any confirmed transactions from our election cache
			//	1.2 if there are passive elections, see if we are part of them
			//2. if there are mature blocks, lets participate in the election

			bool useWeb = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useChain = this.ChainConfiguration.ElectionsRegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);

			// ok, lets run the elections that are due right now!

			foreach(ElectedCandidateResultDistillate electionResult in electionResults) {

				try {
					BlockElectionDistillate matureElectionBlock = await this.ObtainMatureElectionBlock(currentBlockElectionDistillate.electionBockId, electionResult.BlockId, lockContext).ConfigureAwait(false);

					if(matureElectionBlock != null) {

						if(useWeb && (restUtility == null)) {
							restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
						}

						this.CentralCoordinator.Log.Information($"We have a mature election block with Id {electionResult.BlockId}");
						IElectionProcessor matureElectionProcessor = this.factory.InstantiateProcessor(matureElectionBlock, this.centralCoordinator, this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.ChainEventPoolProvider);

						bool sent = false;

						if(useWeb && (this.registrationParameters != null)) {

							ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo = this.PrepareRegistrationInfo();

							try {

								string url = this.ChainConfiguration.WebElectionsRecordsUrl;

								Dictionary<string, object> parameters = null;
								string action = "";

								if(electionResult.ElectionMode == ElectionModes.Active) {
									parameters = matureElectionProcessor.PrepareActiveElectionWebConfirmation(currentBlockElectionDistillate, electionResult, electionsCandidateRegistrationInfo.Password);
									action = "election-records/record-active-election";
								} else if(electionResult.ElectionMode == ElectionModes.Passive) {
									parameters = matureElectionProcessor.PreparePassiveElectionWebConfirmation(currentBlockElectionDistillate, electionResult, electionsCandidateRegistrationInfo.Password);
									action = "election-records/record-passive-election";
								} else {
									throw new ApplicationException("Invalid election type");
								}
								
								(sent, _) = await restUtility.PerformSecurePut(url, action, new RestUtility.RestParameterSet<object>()).ConfigureAwait(false);

								if(sent) {
									await this.UpdateAccountMiningCacheExpiration(lockContext).ConfigureAwait(false);
								}
								
							} catch(Exception ex) {
								this.CentralCoordinator.Log.Error(ex, $"Failed to record election results through web for mining account {this.MiningAccountId}");

								// do nothing, we will sent it on chain
								sent = false;
							}
						}

						if(!sent && useChain) {
							// ok, we are going to send it on chain
							try {
								IElectionCandidacyMessage electionConfirmationMessage = null;

								if(electionResult.ElectionMode == ElectionModes.Active) {
									electionConfirmationMessage = matureElectionProcessor.PrepareActiveElectionConfirmationMessage(currentBlockElectionDistillate, electionResult);
								} else if(electionResult.ElectionMode == ElectionModes.Passive) {
									electionConfirmationMessage = matureElectionProcessor.PreparePassiveElectionConfirmationMessage(currentBlockElectionDistillate, electionResult);
								} else {
									throw new ApplicationException("Invalid election type");
								}

								if(electionConfirmationMessage != null) {

									messages.Add(electionConfirmationMessage);
									sent = true;
								}
							} catch(Exception ex) {
								this.CentralCoordinator.Log.Error(ex, "Failed to prepare gossip election message");

								sent = false;
							}
						}
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, $"Failed to record election for block {electionResult.BlockId}");
				}
			}

			// ok, thats it. lets see if we have any messages to send out
			return messages.Where(m => m != null).ToList();
		}

		protected virtual async Task ConfirmedPrimeElected(BlockElectionDistillate blockElectionDistillate, FinalElectionResultDistillate finalElectionResultDistillate) {

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MiningPrimeElected(blockElectionDistillate.electionBockId));

			MiningHistoryEntry.MiningHistoryParameters parameters = new MiningHistoryEntry.MiningHistoryParameters();
			parameters.blockElectionDistillate = blockElectionDistillate;
			parameters.finalElectionResultDistillate = finalElectionResultDistillate;
			parameters.Message = BlockchainSystemEventTypes.Instance.MiningPrimeElected;
			parameters.Level = ChainMiningProvider.MiningEventLevel.Level1;
			parameters.Parameters = new object[] {blockElectionDistillate.electionBockId};
			this.AddMiningHistoryEntry(parameters);

			this.CentralCoordinator.Log.Information($"We were officially announced as a prime elected in Block {blockElectionDistillate.electionBockId} for the election that was announced in block {blockElectionDistillate.electionBockId - finalElectionResultDistillate.BlockOffset}");
		}

		protected virtual async Task PrimeElectedMissed(BlockElectionDistillate blockElectionDistillate, BlockId originalElectionBlock) {

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.MininPrimeElectedMissed(blockElectionDistillate.electionBockId, originalElectionBlock));

			MiningHistoryEntry.MiningHistoryParameters parameters = new MiningHistoryEntry.MiningHistoryParameters();
			parameters.blockElectionDistillate = blockElectionDistillate;
			parameters.finalElectionResultDistillate = null;
			parameters.blockId = originalElectionBlock;
			parameters.Message = BlockchainSystemEventTypes.Instance.MiningPrimeElectedMissed;
			parameters.Level = ChainMiningProvider.MiningEventLevel.Level2;
			parameters.Parameters = new object[] {blockElectionDistillate.electionBockId, originalElectionBlock};
			this.AddMiningHistoryEntry(parameters);

			this.CentralCoordinator.Log.Information($"Although we were candidate for an election in block {originalElectionBlock}, we were never confirmed in block {blockElectionDistillate.electionBockId}.");
		}

		protected abstract IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GetElectionProcessorFactory();

	#endregion

	}
}