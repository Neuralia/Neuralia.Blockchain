using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.Metadata;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IGossipMessageDispatcher {
		
		Task<ChainNetworkingProvider.DispatchedMethods> DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext, ChainNetworkingProvider.MessageDispatchTypes messageDispatchType = ChainNetworkingProvider.MessageDispatchTypes.GeneralMessage, bool enableBackup = true);
		Task<bool> DispatchNewGossipMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext, AppSettingsBase.ContactMethods method);
		Task<bool> DispatchNewGossipMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext);

	}

	public interface IChainNetworkingProvider : IGossipMessageDispatcher, IDisposableExtended, IChainProvider
	{
		IIPCrawler.PeerStatistics QueryStats(NodeAddressInfo nai, bool onlyConnected = true);
		void HandleSyncError(NodeAddressInfo nai, DateTime timestamp);
		void HandleInputSliceSync(NodeAddressInfo node, DateTime timestamp);
			
		bool InAppointmentWindow{ get; }
		bool InAppointmentWindowProximity { get; }
		bool IsInAppointmentWindow(DateTime appointment);
		bool IsPaused { get; }
		ulong MyClientIdNonce { get; }
		Guid MyClientUuid { get; }
		IPAddress PublicIpv4 { get; }
		IPAddress PublicIpv6 { get; }
		int CurrentPeerCount { get; }
		bool IsConnectable { get; }
		int P2pPort { get; }

		bool HasPeerConnections { get; }
		bool NoPeerConnections { get; }
		bool MinimumDispatchPeerCountAchieved(AppSettingsBase.ContactMethods method);
		bool MinimumDispatchPeerCountAchieved();
		
		bool NetworkingStarted { get; }
		bool NoNetworking { get; }
		BlockchainNetworkingService.MiningRegistrationParameters MiningRegistrationParameters { get; }

		List<string> AllIPCache { get; }

		List<PeerConnection> AllConnectionsList { get; }
		int SyncingConnectionsCount { get; }
		List<PeerConnection> SyncingConnectionsList { get; }

		int FullGossipConnectionsCount { get; }
		List<PeerConnection> FullGossipConnectionsList { get; }

		int BasicGossipConnectionsCount { get; }
		List<PeerConnection> BasicGossipConnectionsList { get; }

		event Func<int, LockContext, Task> PeerConnectionsCountUpdated;
		event Func<LockContext, Task> IpAddressChanged;

		void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet);

		void RegisterChain(INetworkRouter transactionchainNetworkRouting);

		BlockchainNetworkingService.MiningRegistrationParameters RegisterMiningRegistrationParameters();
		void UnRegisterMiningRegistrationParameters();
		Task ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection);

		void ReceiveConnectionsManagerTask(ISimpleTask task);
		void ReceiveConnectionsManagerTask(IColoredTask task);

		void PauseNetwork();
		void RestoreNetwork();

		void RemoveConnection(PeerConnection connection);

		Task<ChainNetworkingProvider.DispatchedMethods> DispatchLocalTransactionAsync(ITransactionEnvelope signedTransactionEnvelope, CorrelationContext correlationContext, LockContext lockContext, bool enableBackup = true);
		Task<ChainNetworkingProvider.DispatchedMethods> DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext, LockContext lockContext, bool enableBackup = true);

		Task<ChainNetworkingProvider.DispatchedMethods> DispatchElectionMessages(List<IElectionCandidacyMessage> messages, LockContext lockContext);

		Task<List<WebTransactionPoolResult>> QueryWebTransactionPool(LockContext lockContext);

		void RegisterValidationServer(List<(DateTime appointment, TimeSpan window, int requesterCount)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate);
		void UnregisterValidationServer();
		void AddAppointmentWindow(DateTime appointment, TimeSpan window, int requesterCount);

		Task<(bool success, CheckAppointmentRequestConfirmedResult result)> PerformAppointmentRequestUpdateCheck(Guid requesterId, LockContext lockContext, bool enableBackup = true);
		Task<(bool success, CheckAppointmentVerificationConfirmedResult result)> PerformAppointmentCompletedUpdateCheck(Guid requesterId, Guid secretAppointmentId, LockContext lockContext, bool enableBackup = true);
		Task<(bool success, CheckAppointmentContextResult2 result)> PerformAppointmentContextUpdateCheck(Guid requesterId, int requesterIndex, DateTime appointment, LockContext lockContext, bool enableBackup = true);
		Task<(bool success, string triggerKey)> PerformAppointmentTriggerUpdateCheck(DateTime appointment, LockContext lockContext, bool enableBackup = true);
		
		Task<(bool success, CheckAppointmentsResult result)> QueryAvailableAppointments(LockContext lockContext);
		Task<(bool success, QueryValidatorAppointmentSessionsResult result)> QueryValidatorAppointmentSessions(AccountId miningAccountId, List<DateTime> appointments, List<Guid> hashes, LockContext lockContext);
		void UrgentClearConnections();
		
	}

	public interface IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainNetworkingProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class ChainNetworkingProvider {

		public enum MessageDispatchTypes {
			GeneralMessage,
			AppointmentInitiationRequest,
			AppointmentRequest,
			AppointmentValidatorResults,
			Elections
		}
		
		public enum DispatchedMethods : byte {
			Failed = 0,
			Web = 1,
			Gossip = 2,
			Any = 3
		}
	}

	/// <summary>
	///     A provider that offers the chain state parameters from the DB
	/// </summary>
	/// <typeparam name="CHAIN_STATE_DAL"></typeparam>
	/// <typeparam name="CHAIN_STATE_CONTEXT"></typeparam>
	/// <typeparam name="CHAIN_STATE_ENTRY"></typeparam>
	public abstract class ChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		protected CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		public IBlockchainNetworkingService networkingService;
		private IClientAppointmentRequestWorkflow appointmentRequestWorkflow;
		
		public ChainNetworkingProvider(IBlockchainNetworkingService networkingService, CENTRAL_COORDINATOR centralCoordinator) {
			this.networkingService = networkingService;
			this.centralCoordinator = centralCoordinator;

			if(this.networkingService != null) {
				this.networkingService.IpAddressChanged += this.NetworkingServiceOnIpAddressChanged;
			}

			if(networkingService?.ConnectionStore != null) {
				this.networkingService.ConnectionStore.IsConnectableChange += connectable => {

					// alert that our connectable status has changed
					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ConnectableChanged(connectable));
				};

				this.networkingService.ConnectionStore.PeerConnectionsCountUpdated += async c => {

					if(this.PeerConnectionsCountUpdated != null) {
						LockContext lockContext = null;
						await this.PeerConnectionsCountUpdated(c, lockContext).ConfigureAwait(false);
					}

				};

			}
		}

		protected BlockchainType ChainId => this.centralCoordinator.ChainId;

		private Func<PeerConnection, bool> FilterPeersPerChainVersion => p => p.IsBlockchainVersionValid(this.ChainId) && p.SupportsChain(this.ChainId);
		public event Func<int, LockContext, Task> PeerConnectionsCountUpdated;
		public event Func<LockContext, Task> IpAddressChanged;

		public IIPCrawler.PeerStatistics QueryStats(NodeAddressInfo nai, bool onlyConnected = true) {
			return this.networkingService.IPCrawler.QueryStats(nai, onlyConnected);
		}
		public void HandleSyncError(NodeAddressInfo nai, DateTime timestamp) {
			this.networkingService.IPCrawler.HandleSyncError(nai, timestamp);
		}

		public void HandleInputSliceSync(NodeAddressInfo nai, DateTime timestamp)
		{
			this.networkingService.IPCrawler.HandleInputSliceSync(nai, timestamp);
		}
		public void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet) {

			this.networkingService.PostNewGossipMessage(gossipMessageSet);
		}

		public bool IsPaused => this.networkingService?.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused;
		public ulong MyClientIdNonce => this.networkingService.ConnectionStore.MyClientIdNonce;
		public Guid MyClientUuid => this.networkingService.ConnectionStore.MyClientUuid;
		public IPAddress PublicIpv4 => this.networkingService.ConnectionStore.PublicIpv4;
		public IPAddress PublicIpv6 => this.networkingService.ConnectionStore.PublicIpv6;

		public void PauseNetwork() {
			this.networkingService?.Pause();
		}

		public void RestoreNetwork() {
			this.networkingService?.Resume();
		}

		public void RemoveConnection(PeerConnection connection) {
			this.networkingService.ConnectionStore.RemoveConnection(connection);
		}

		/// <summary>
		/// to be called in urgency only, to clear some ram
		/// </summary>
		public void UrgentClearConnections() {

			this.networkingService.UrgentClearConnections();
		}
		
		public int CurrentPeerCount => this.networkingService.CurrentPeerCount;
		public bool IsConnectable => this.networkingService.ConnectionStore.IsConnectable;
		public int P2pPort => this.networkingService.LocalPort;

		public bool HasPeerConnections => this.CurrentPeerCount != 0;
		public bool NoPeerConnections => !this.HasPeerConnections;

		/// <summary>
		///     this property tells us if we have the minimum number of peers to send transactions
		/// </summary>
		public virtual bool MinimumDispatchPeerCountAchieved(AppSettingsBase.ContactMethods method) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			return method.HasFlag(AppSettingsBase.ContactMethods.Gossip) && (this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount >= chainConfiguration.MinimumDispatchPeerCount);
		}

		public bool MinimumDispatchPeerCountAchieved() {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			return this.MinimumDispatchPeerCountAchieved(chainConfiguration.RegistrationMethod);
		}
		
		public bool NetworkingStarted => this.networkingService.IsNetworkAvailable && (this.networkingService?.IsStarted ?? false);
		public bool NoNetworking => !this.NetworkingStarted;

		public void RegisterChain(INetworkRouter transactionchainNetworkRouting) {

			// validate the chain versions
			bool VersionValidationCallback(SoftwareVersion version) {

				IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;

				if(version > new SoftwareVersion(chainStateProvider.MaximumVersionAllowed)) {
					return false;
				}

				if(version < new SoftwareVersion(chainStateProvider.MinimumVersionAllowed)) {
					return false;
				}

				if(version < new SoftwareVersion(chainStateProvider.MinimumWarningVersionAllowed)) {
					//TODO: what to do here?
				}

				return true;
			}

			this.networkingService.RegisterChain(this.ChainId, GlobalSettings.Instance.NodeInfo.GetChainSettings()[this.ChainId], transactionchainNetworkRouting, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase, VersionValidationCallback);
		}

		public BlockchainNetworkingService.MiningRegistrationParameters RegisterMiningRegistrationParameters() {
			if(!this.networkingService.ChainMiningRegistrationParameters.ContainsKey(this.ChainId)) {
				this.networkingService.ChainMiningRegistrationParameters.Add(this.ChainId, new BlockchainNetworkingService.MiningRegistrationParameters());
			}

			return this.MiningRegistrationParameters;
		}

		public void UnRegisterMiningRegistrationParameters() {
			if(this.networkingService.ChainMiningRegistrationParameters.ContainsKey(this.ChainId)) {
				this.networkingService.ChainMiningRegistrationParameters.Remove(this.ChainId);
			}
		}

		public BlockchainNetworkingService.MiningRegistrationParameters MiningRegistrationParameters => this.networkingService.ChainMiningRegistrationParameters.ContainsKey(this.ChainId) ? this.networkingService.ChainMiningRegistrationParameters[this.ChainId] : null;

		public Task ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection) {
			return this.networkingService.ForwardValidGossipMessage(gossipMessageSet, connection);
		}

		public void ReceiveConnectionsManagerTask(ISimpleTask task) {
			this.networkingService.ConnectionsManager.ReceiveTask(task);
		}

		public void ReceiveConnectionsManagerTask(IColoredTask task) {
			this.networkingService.ConnectionsManager.ReceiveTask(task);
		}

		private async Task NetworkingServiceOnIpAddressChanged(LockContext lockContext) {
			if(this.IpAddressChanged != null) {
				await this.IpAddressChanged(lockContext).ConfigureAwait(false);
			}
		}

	#region Scoped connections

		public List<string> AllIPCache => this.networkingService.ConnectionStore.AvailablePeerNodesCopy.Select(n => n.AdjustedIp).ToList();

		public List<PeerConnection> AllConnectionsList => this.networkingService.ConnectionStore.AllConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();

		public List<PeerConnection> SyncingConnectionsList => this.networkingService?.ConnectionStore?.SyncingConnectionsList(this.centralCoordinator.ChainId).Where(this.FilterPeersPerChainVersion).ToList();
		public int SyncingConnectionsCount => this.SyncingConnectionsList?.Count ?? 0;

		public List<PeerConnection> FullGossipConnectionsList => this.networkingService?.ConnectionStore?.FullGossipConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();
		public int FullGossipConnectionsCount => this.FullGossipConnectionsList?.Count ?? 0;

		public List<PeerConnection> BasicGossipConnectionsList => this.networkingService?.ConnectionStore?.BasicGossipConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();
		public int BasicGossipConnectionsCount => this.BasicGossipConnectionsList?.Count ?? 0;

	#endregion

	#region Dispatching

		/// <summary>
		///     Publish an unpublished transaction on the network
		/// </summary>
		public async Task<ChainNetworkingProvider.DispatchedMethods> DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext, LockContext lockContext, bool enableBackup = true) {

			IWalletGenerationCache results = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetGenerationCacheEntry(transactionId, lockContext).ConfigureAwait(false);

			if(results == null) {
				throw new EventDispatchException("Impossible to dispatch a transaction, failed to find cached entry.");
			}

			// if(results.Status != (byte) WalletGenerationCache.GenerationCacheStatuses.New) {
			// 	throw new EventDispatchException("Impossible to dispatch a transaction that has already been sent");
			// }

			ITransactionEnvelope signedTransactionEnvelope = null;
			signedTransactionEnvelope = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ITransactionEnvelope>(results.Event);

			return await this.DispatchLocalTransactionAsync(signedTransactionEnvelope, correlationContext, lockContext, enableBackup).ConfigureAwait(false);
		}

		protected bool CanSendGossip(AppSettingsBase.ContactMethods method) {
			if(this.NoNetworking) {
				throw new ApplicationException("We are not connected to the p2p network nor have internet access.");
			}

			return this.MinimumDispatchPeerCountAchieved(method);
		}
		/// <summary>
		///     Publish an unpublished transaction on the network
		/// </summary>
		/// <param name="signedTransactionEnvelope"></param>
		public async Task<ChainNetworkingProvider.DispatchedMethods> DispatchLocalTransactionAsync(ITransactionEnvelope signedTransactionEnvelope, CorrelationContext correlationContext, LockContext lockContext, bool enableBackup = true) {

			ChainNetworkingProvider.DispatchedMethods dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Failed;
			if(signedTransactionEnvelope.Contents.RehydratedEvent is IGenesisAccountPresentationTransaction) {
				throw new ApplicationException("Genesis transactions can not be added this way");
			}
			
			IWalletGenerationCache entry = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetGenerationCacheEntry(signedTransactionEnvelope.Contents.Uuid, lockContext).ConfigureAwait(false);

			if(entry.Signed == false) {
				throw new EventDispatchException("Impossible to dispatch a transaction that is not signed");
			}
			if(entry.Dispatched) {
				throw new EventDispatchException("Impossible to dispatch a transaction that has already been sent");
			}

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;

			if(useWeb) {
				try {
					sent = await this.PerformWebTransactionRegistration(signedTransactionEnvelope, correlationContext, lockContext).ConfigureAwait(false);

					if(!sent) {
						throw new ApplicationException();
					}
					dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Web;
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register transaction through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {

				if(this.CanSendGossip(chainConfiguration.RegistrationMethod)) {

					IBlockchainGossipMessageSet gossipMessageSet = this.PrepareTransactionGossipMessageSet(signedTransactionEnvelope);

					await Repeater.RepeatAsync(async () => {
						await this.SendGossipTransaction(gossipMessageSet).ConfigureAwait(false);
						sent = true;
						dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Gossip;
					}).ConfigureAwait(false);

					if(!sent) {
						throw new ApplicationException("Failed to send transaction");
					}
					
				} else {
					throw new EventDispatchException("Failed to send transaction. Not enough peers available to send a gossip transactions.");
				}
			}

			if(!sent) {
				throw new EventDispatchException("Failed to send transaction");
			}
			
			// send the confirmation
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionSent(signedTransactionEnvelope.Contents.Uuid), correlationContext);

			return dispatchedMethod;
		}

		public async Task<List<WebTransactionPoolResult>> QueryWebTransactionPool(LockContext lockContext) {
			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.UseWebTransactionPool) {

				try {
					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					return await Repeater.RepeatAsync(async () => {
						string url = chainConfiguration.WebTransactionPoolUrl;

						Dictionary<string, object> parameters = new Dictionary<string, object>();

						IRestResponse result = await restUtility.Post(url, "pool/query", parameters).ConfigureAwait(false);

						// ok, check the result
						if(result.StatusCode == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(result.Content)) {
							// ok, we are not registered. we can await a response from the IP Validator
							var serializerSettings = new JsonSerializerOptions();
							serializerSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
							
							return JsonSerializer.Deserialize<List<WebTransactionPoolResult>>(result.Content, serializerSettings);
						}

						throw new ApplicationException();
					}).ConfigureAwait(false);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to query web transaction pool");
				}
			}

			return new List<WebTransactionPoolResult>();
		}

		public void RegisterValidationServer(List<(DateTime appointment, TimeSpan window, int requesterCount)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate) {
			this.networkingService.RegisterValidationServer(this.centralCoordinator.ChainId, appointmentWindows, appointmentValidatorDelegate);
		}

		public void UnregisterValidationServer() {
			this.networkingService.UnregisterValidationServer(this.centralCoordinator.ChainId);
		}

		public void AddAppointmentWindow(DateTime appointment, TimeSpan window, int requesterCount) {
			this.networkingService.AddAppointmentWindow(appointment, window, requesterCount);
		}
		
		protected IIPCrawler IPCrawler => this.networkingService.IPCrawler;

		public bool InAppointmentWindow => this.networkingService.InAppointmentWindow;
		public bool InAppointmentWindowProximity => this.networkingService.InAppointmentWindowProximity;
		

		public bool IsInAppointmentWindow(DateTime appointment) {
			return this.networkingService.IsInAppointmentWindow(appointment);
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task<bool> PerformWebTransactionRegistration(ITransactionEnvelope signedTransactionEnvelope, CorrelationContext correlationContext, LockContext lockContext) {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool sent = false;

			try {
				sent = await Repeater.RepeatAsync(async () => {

					Dictionary<string, object> parameters = new Dictionary<string, object>();

					SafeArrayHandle bytes = signedTransactionEnvelope.DehydrateEnvelope();
					parameters.Add("transactionEnvelope", bytes.Entry.ToBase64());
					bytes.Return();

					string url = chainConfiguration.WebTransactionRegistrationUrl;
					string action = "transactions/register";

					if(signedTransactionEnvelope is IPresentationTransactionEnvelope) {
						url = chainConfiguration.WebPresentationRegistrationUrl;
						action = "presentation/register";
					}

					IRestResponse result = await restUtility.Put(url, action, parameters).ConfigureAwait(false);

					// ok, check the result
					if(result.StatusCode == HttpStatusCode.OK) {
						// ok, all good

						return true;
					}

					throw new ApplicationException("Failed to register transaction through web");
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "");
			}

			return sent;
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task<bool> PerformWebMessageRegistration(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext, ChainNetworkingProvider.MessageDispatchTypes messageDispatchType) {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			Dictionary<string, object> parameters = new Dictionary<string, object>();

			using SafeArrayHandle bytes = messageEnvelope.DehydrateEnvelope();
			parameters.Add("messageEnvelope", bytes.Entry.ToBase64());
			bool sent = false;
			try {
				sent = await Repeater.RepeatAsync(async () => {

					string url = chainConfiguration.WebMessageRegistrationUrl;
					string action = "messages/register";

					if(messageDispatchType == ChainNetworkingProvider.MessageDispatchTypes.GeneralMessage) {

					} else if(messageDispatchType == ChainNetworkingProvider.MessageDispatchTypes.AppointmentInitiationRequest) {
						url = chainConfiguration.WebAppointmentsRegistrationUrl;
						action = "appointments/initiation";
					} else if(messageDispatchType == ChainNetworkingProvider.MessageDispatchTypes.AppointmentRequest) {
						url = chainConfiguration.WebAppointmentsRegistrationUrl;
						action = "appointments/appointment";
					} else if(messageDispatchType == ChainNetworkingProvider.MessageDispatchTypes.AppointmentValidatorResults) {
						url = chainConfiguration.WebAppointmentsRegistrationUrl;
						action = "appointments/validator";

						if(messageEnvelope is ISignedMessageEnvelope signedEnvelope && signedEnvelope.Contents.RehydratedEvent is IAppointmentVerificationResultsMessage appointmentVerificationResultsMessage) {

						} else {
							throw new ApplicationException("Invalid appointment initiation envelope type");
						}
					}

					IRestResponse result = await restUtility.Put(url, action, parameters).ConfigureAwait(false);

					// ok, check the result
					if(result.StatusCode == HttpStatusCode.OK) {
						// ok, all good

						return true;
					}

					throw new ApplicationException($"Failed to register message through web. Error code: {result.StatusCode}");
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "");
			}

			return sent;
		}

		protected IBlockchainGossipMessageSet PrepareTransactionGossipMessageSet(ITransactionEnvelope signedTransactionEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateTransactionCreatedGossipMessageSet(signedTransactionEnvelope);

			bool setMetadata = false;
			bool presentation = false;

			if(signedTransactionEnvelope is IPresentationTransactionEnvelope) {
				setMetadata = true;
				presentation = true;
			}

			if(setMetadata) {
				gossipMessageSet.MessageMetadata = new GossipMessageMetadata(new TransactionGossipMessageMetadataDetails(presentation), this.centralCoordinator.ChainId);
			}

			this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected IBlockchainGossipMessageSet PrepareBlockchainMessageGossipMessageSet(IMessageEnvelope messageEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateBlockchainMessageCreatedGossipMessageSet(messageEnvelope);
			//gossipMessageSet.MessageMetadata = new GossipMessageMetadata(new BlockchainMessageGossipMessageMetadataDetails(), this.centralCoordinator.ChainId);
			this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected Task SendGossipTransaction(IBlockchainGossipMessageSet gossipMessageSet) {
			if(GlobalSettings.ApplicationSettings.P2PEnabled) {

				
				if(this.CanSendGossip(AppSettingsBase.ContactMethods.Gossip)) {
					// ok, we are ready. lets send it out to the world!!  :)
					this.centralCoordinator.PostNewGossipMessage(gossipMessageSet);
				} else {
					this.CentralCoordinator.Log.Warning("No peers available. Gossip message announcing new transaction is not sent");
				}

			} else {
				this.CentralCoordinator.Log.Warning("p2p is not enabled. Gossip message announcing new transaction is not sent");
			}

			return Task.CompletedTask;
		}

		public async Task<(bool success, CheckAppointmentRequestConfirmedResult result)> PerformAppointmentRequestUpdateCheck(Guid requesterId, LockContext lockContext, bool enableBackup = true) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;
			CheckAppointmentRequestConfirmedResult result = null;
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					(sent, result) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();

						parameters.Add("requesterId", requesterId);
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-appointment-request-confirmed";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							var serializerSettings = new JsonSerializerOptions();
							serializerSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
							
							return (true, JsonSerializer.Deserialize<CheckAppointmentRequestConfirmedResult>(webResult.Content, serializerSettings));
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && result != null) {
						return (sent, result);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {
				if(this.CanSendGossip(chainConfiguration.RegistrationMethod)) {

					sent = await SendAppointmentRequest(requesterId, null, null, Enums.AppointmentRequestModes.RequestConfirmation, lockContext).ConfigureAwait(false);

					if(!sent) {
						throw new ApplicationException("Failed to send message");
					}
				} else {
					this.CentralCoordinator.Log.Error("Failed to send message. Not enough peers available to send a gossip message.");
				}
			}

			return (sent, null);
		}

		public async Task<(bool success, CheckAppointmentVerificationConfirmedResult result)> PerformAppointmentCompletedUpdateCheck(Guid requesterId, Guid secretAppointmentId, LockContext lockContext, bool enableBackup = true) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;
			CheckAppointmentVerificationConfirmedResult result = null;
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					(sent, result) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();

						parameters.Add("requesterId", requesterId);
						parameters.Add("secretAppointmentId", secretAppointmentId);
						
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-appointment-verification-confirmed";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							var serializerSettings = new JsonSerializerOptions();
							serializerSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
							
							return (true, JsonSerializer.Deserialize<CheckAppointmentVerificationConfirmedResult>(webResult.Content, serializerSettings));
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && result != null) {
						return (sent, result);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {
				if(this.CanSendGossip(chainConfiguration.RegistrationMethod)) {

					sent = await SendAppointmentRequest(requesterId, null, null, Enums.AppointmentRequestModes.VerificationConfirmation, lockContext).ConfigureAwait(false);
					
				} else {
					throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
				}
			}

			return (sent, null);
		}

		public async Task<(bool success, CheckAppointmentContextResult2 result)> PerformAppointmentContextUpdateCheck(Guid requesterId, int requesterIndex, DateTime appointment, LockContext lockContext, bool enableBackup = true) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;
			CheckAppointmentContextResult2 result = null;
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					(sent, result) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();

						parameters.Add("requesterId", requesterId);
						parameters.Add("appointment", appointment.Ticks);
						
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-appointment-context2";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							var serializerSettings = new JsonSerializerOptions();
							serializerSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							return (true, JsonSerializer.Deserialize<CheckAppointmentContextResult2>(webResult.Content, serializerSettings));
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && result != null) {
						return (sent, result);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {
				if(this.CanSendGossip(chainConfiguration.RegistrationMethod)) {
					
					sent = await SendAppointmentRequest(requesterId, requesterIndex, appointment, Enums.AppointmentRequestModes.Context, lockContext).ConfigureAwait(false);
				} else {
					throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
				}
			}

			return (sent, null);
		}

		public async Task<(bool success, string triggerKey)> PerformAppointmentTriggerUpdateCheck(DateTime appointment, LockContext lockContext, bool enableBackup = true) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;
			string triggerKey = null;
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					(sent, triggerKey) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();

						parameters.Add("appointment", appointment.Ticks);
						
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-appointment-trigger2";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							return (true, webResult.Content);
						}
						else if(webResult.StatusCode == HttpStatusCode.NoContent) {
							return (true, null);
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && !string.IsNullOrWhiteSpace(triggerKey)) {
						return (sent, triggerKey);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {
				if(this.CanSendGossip(chainConfiguration.RegistrationMethod)) {

					sent = await SendAppointmentRequest(null, null, appointment, Enums.AppointmentRequestModes.Trigger, lockContext).ConfigureAwait(false);
				} else {
					throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
				}
			}
			
			return (sent, null);
		}
		
		public async Task<(bool success, CheckAppointmentsResult result)> QueryAvailableAppointments(LockContext lockContext) {
			
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					bool sent = false;
					CheckAppointmentsResult result = null;

					(sent, result) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();
						
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-appointments";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							var serializerSettings = new JsonSerializerOptions();

							return (true, JsonSerializer.Deserialize<CheckAppointmentsResult>(webResult.Content, serializerSettings));
						}
						else if(webResult.StatusCode == HttpStatusCode.NoContent) {
							return (true, null);
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && result != null) {
						return (true, result);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");
					
				}
			}
			
			return (false, null);
		}

		public async Task<(bool success, QueryValidatorAppointmentSessionsResult result)> QueryValidatorAppointmentSessions(AccountId miningAccountId, List<DateTime> appointments, List<Guid> hashes, LockContext lockContext) {
			
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			
			if(useWeb) {
				try {

					RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

					bool sent = false;
					QueryValidatorAppointmentSessionsResult result = null;

					(sent, result) = await Repeater.RepeatAsync(async () => {

						Dictionary<string, object> parameters = new Dictionary<string, object>();
						
						parameters.Add("miningAccountId", miningAccountId.ToLongRepresentation());
						
						parameters.Add("appointments", string.Join(";", appointments.Select(h => h.Ticks)));
						parameters.Add("hashes", string.Join(";", hashes.Select(h => h.ToString())));
						
						string url = chainConfiguration.WebAppointmentsRegistrationUrl;
						string action = "appointments/check-validator-appointment-sessions";

						IRestResponse webResult = await restUtility.Post(url, action, parameters).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {
							
							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, null);
							}
							// ok, all good
							var serializerSettings = new JsonSerializerOptions();
							serializerSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
							
							return (true, JsonSerializer.Deserialize<QueryValidatorAppointmentSessionsResult>(webResult.Content, serializerSettings));
						}
						else if(webResult.StatusCode == HttpStatusCode.NoContent) {
							return (true, null);
						}

						throw new ApplicationException("Failed to register message through web");
					}).ConfigureAwait(false);

					if(sent && result != null) {
						return (true, result);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");
					
				}
			}
			
			return (false, null);
		}
		
		private async Task<bool> SendAppointmentRequest(Guid? requesterId, int? requesterIndex, DateTime? appointment, Enums.AppointmentRequestModes mode, LockContext lockContext) {

			bool sent = false;
			if(this.appointmentRequestWorkflow == null) {
				try {
					this.appointmentRequestWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ClientWorkflowFactoryBase.CreateAppointmentRequestWorkflow(requesterId, requesterIndex, appointment, mode);

					this.appointmentRequestWorkflow.Completed2 += (b, workflow) => {
						return Task.CompletedTask;
					};

					this.centralCoordinator.PostWorkflow(this.appointmentRequestWorkflow);

					await appointmentRequestWorkflow.Wait(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

					if(this.appointmentRequestWorkflow != null && appointmentRequestWorkflow.Task.IsCompletedSuccessfully) {
						sent = true;
						if(appointmentRequestWorkflow.Result != null && !appointmentRequestWorkflow.Result.IsZero) {

							ISignedMessageEnvelope messageEnvelope = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ISignedMessageEnvelope>(appointmentRequestWorkflow.Result);

							ValidationResult valid = new ValidationResult();

							await this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(messageEnvelope, true, result => {
								valid = result;
							}, lockContext).ConfigureAwait(false);

							if(valid.Valid) {

								await this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.HandleBlockchainMessage(messageEnvelope.Contents.RehydratedEvent, messageEnvelope, lockContext).ConfigureAwait(false);
								
							}
						}
					}
				} finally {
					this.appointmentRequestWorkflow = null;
				}
			}

			return sent;
		}
		

		public async Task<ChainNetworkingProvider.DispatchedMethods> DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext, ChainNetworkingProvider.MessageDispatchTypes messageDispatchType = ChainNetworkingProvider.MessageDispatchTypes.GeneralMessage, bool enableBackup = true) {

			ChainNetworkingProvider.DispatchedMethods dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Failed;
			
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			var method = chainConfiguration.RegistrationMethod;

			if(messageDispatchType == ChainNetworkingProvider.MessageDispatchTypes.Elections) {
				method = chainConfiguration.ElectionsRegistrationMethod;
			}
			bool useWeb = method.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = method.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;

			if(useWeb) {
				try {

					sent = await this.PerformWebMessageRegistration(messageEnvelope, correlationContext, messageDispatchType).ConfigureAwait(false);

					if(sent) {
						dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Web;
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip && (useWeb?enableBackup:true)) {
				sent = await DispatchNewGossipMessage(messageEnvelope, correlationContext, method).ConfigureAwait(false);

				if(sent) {
					dispatchedMethod = ChainNetworkingProvider.DispatchedMethods.Gossip;
				}
			}
			
			return dispatchedMethod;
		}

		public Task<bool> DispatchNewGossipMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext) {

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			return this.DispatchNewGossipMessage(messageEnvelope, correlationContext, chainConfiguration.RegistrationMethod);
		}

		public virtual async Task<bool> DispatchNewGossipMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext, AppSettingsBase.ContactMethods method) {


			bool sent = false;
			if(this.CanSendGossip(method)) {

				IBlockchainGossipMessageSet gossipMessageSet = this.PrepareBlockchainMessageGossipMessageSet(messageEnvelope);

				await Repeater.RepeatAsync(gossipMessageSet, async (messageSet, count) => {
					// ok, we are ready. lets send it out to the world!!  :)
					await this.SendGossipTransaction(messageSet).ConfigureAwait(false);
					sent = true;
				}).ConfigureAwait(false);

				if(!sent) {
					throw new ApplicationException("Failed to send gossip message");
				}

			} else {
				throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
			}

			return sent;
		}

		public async Task<ChainNetworkingProvider.DispatchedMethods> DispatchElectionMessages(List<IElectionCandidacyMessage> messages, LockContext lockContext) {
			if((messages != null) && messages.Any()) {

				// well, seems we have messages!  lets send them out. first, prepare the envelopes

				List<ISignedMessageEnvelope> results = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PrepareElectionMessageEnvelopes(messages, lockContext).ConfigureAwait(false);

				HashSet<Guid> sentMessages = new HashSet<Guid>();
				
				foreach(ISignedMessageEnvelope envelope in results) {
					try {
						await Repeater.RepeatAsync(async () => {
							// if we repeat, lets not send the successful ones more than once.
							if(!sentMessages.Contains(envelope.ID)) {
								var method = await this.DispatchNewMessage(envelope, new CorrelationContext()).ConfigureAwait(false);

								if(method == ChainNetworkingProvider.DispatchedMethods.Failed) {
									throw new ApplicationException("Failed to send election message");
								}

								sentMessages.Add(envelope.ID);
							}
						}).ConfigureAwait(false);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, "election message dispatch failed");
					}
				}
			}

			return ChainNetworkingProvider.DispatchedMethods.Any;
		}

	#endregion

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				if(this.networkingService != null) {
					try {
						this.networkingService.IpAddressChanged -= this.NetworkingServiceOnIpAddressChanged;
					} catch {

					}
				}
			}

			this.IsDisposed = true;
		}

		~ChainNetworkingProvider() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}

}