using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	
	public interface IGossipMessageDispatcher {
		Task DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext);
	}

	
	public interface IChainNetworkingProvider : IGossipMessageDispatcher, IDisposableExtended, IChainProvider {

		event Func<int, LockContext, Task> PeerConnectionsCountUpdated;
		event Func<LockContext, Task> IpAddressChanged;
		
		bool IsPaused { get; }
		ulong MyClientIdNonce { get; }
		Guid MyclientUuid { get; }
		IPAddress PublicIp { get; }
		int CurrentPeerCount { get; }

		bool HasPeerConnections { get; }
		bool NoPeerConnections { get; }
		bool MinimumDispatchPeerCountAchieved { get; }

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
		
		Task DispatchLocalTransactionAsync(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext, LockContext lockContext);
		Task DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext, LockContext lockContext);

		Task<bool> DispatchElectionMessages(List<IElectionCandidacyMessage> messages, LockContext lockContext);
	}

	public interface IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainNetworkingProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
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

		public IBlockchainNetworkingService networkingService;
		public event Func<int, LockContext, Task> PeerConnectionsCountUpdated;
		public event Func<LockContext, Task> IpAddressChanged;

		public ChainNetworkingProvider(IBlockchainNetworkingService networkingService, CENTRAL_COORDINATOR centralCoordinator) {
			this.networkingService = networkingService;
			this.centralCoordinator = centralCoordinator;

			if(this.networkingService != null) {
				this.networkingService.IpAddressChanged += this.NetworkingServiceOnIpAddressChanged;
			}

			if(networkingService?.ConnectionStore != null) {
				this.networkingService.ConnectionStore.IsConnectableChange += (connectable) => {
				
					// alert that our connectable status has changed
					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ConnectableChanged(connectable));
				};

				this.networkingService.ConnectionStore.PeerConnectionsCountUpdated += async (c) => {

					if(this.PeerConnectionsCountUpdated != null) {
						LockContext lockContext = null;
						await PeerConnectionsCountUpdated(c, lockContext).ConfigureAwait(false);
					}
		
				};

			}
		}

		private async Task NetworkingServiceOnIpAddressChanged(LockContext lockContext) {
			if(this.IpAddressChanged != null) {
				await this.IpAddressChanged(lockContext).ConfigureAwait(false);
			}
		}

		protected BlockchainType ChainId => this.centralCoordinator.ChainId;

		private Func<PeerConnection, bool> FilterPeersPerChainVersion => p => p.IsBlockchainVersionValid(this.ChainId) && p.SupportsChain(this.ChainId);

		public void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet) {

			this.networkingService.PostNewGossipMessage(gossipMessageSet);
		}
		
		public bool IsPaused => this.networkingService?.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused;
		public ulong MyClientIdNonce => this.networkingService.ConnectionStore.MyClientIdNonce;
		public Guid MyclientUuid => this.networkingService.ConnectionStore.MyClientUuid;
		public IPAddress PublicIp => this.networkingService.ConnectionStore.PublicIp;
		
		public void PauseNetwork() {
			this.networkingService?.Pause(false);
		}
		
		public void RestoreNetwork() {
			this.networkingService?.Resume();
		}

		public void RemoveConnection(PeerConnection connection) {
			this.networkingService.ConnectionStore.RemoveConnection(connection);
		}

		public int CurrentPeerCount => this.networkingService.CurrentPeerCount;

		public bool HasPeerConnections => this.CurrentPeerCount != 0;
		public bool NoPeerConnections => !this.HasPeerConnections;
		
		/// <summary>
		/// this property tells us if we have the minimum number of peers to send transactions
		/// </summary>
		public bool MinimumDispatchPeerCountAchieved  {
			get {
				BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				return !(chainConfiguration.RegistrationMethod == AppSettingsBase.ContactMethods.Gossip && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount < chainConfiguration.MinimumDispatchPeerCount);
			}
		}

		public bool NetworkingStarted => this.networkingService?.IsStarted ?? false;
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
		
		
	#region Scopped connections

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
		public async Task DispatchLocalTransactionAsync(TransactionId transactionId, CorrelationContext correlationContext, LockContext lockContext) {

			IWalletTransactionCache results = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetLocalTransactionCacheEntry(transactionId, lockContext).ConfigureAwait(false);

			if(results == null) {
				throw new ApplicationException("Impossible to dispatch a transaction, failed to find cached entry.");
			}

			if(results.Status != (byte) WalletTransactionCache.TransactionStatuses.New) {
				throw new ApplicationException("Impossible to dispatch a transaction that has already been sent");
			}

			ITransactionEnvelope transactionEnvelope = null;
			try {
				transactionEnvelope = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ITransactionEnvelope>(results.Transaction);
			}
			catch(UnrecognizedElementException urex) {
				
				throw;
			}

			await this.DispatchLocalTransactionAsync(transactionEnvelope, correlationContext, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Publish an unpublished transaction on the network
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		public async Task DispatchLocalTransactionAsync(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext, LockContext lockContext) {

			IWalletTransactionCache entry = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetLocalTransactionCacheEntry(transactionEnvelope.Contents.Uuid, lockContext).ConfigureAwait(false);

			if(entry.Status != (byte) WalletTransactionCache.TransactionStatuses.New) {
				throw new ApplicationException("Impossible to dispatch a transaction that has already been sent");
			}

			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;

			if(!useWeb && !chainConfiguration.AllowGossipPresentations && (transactionEnvelope.IsPresentation || transactionEnvelope.Contents.RehydratedTransaction is IPresentation) && !this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.AllowGossipPresentations) {
				
				// seems we have no choice but to use the webreg for presentation transactions.
				useWeb = true;
			}

			if(useWeb) {
				try {
					await this.PerformWebTransactionRegistration(transactionEnvelope, correlationContext, lockContext).ConfigureAwait(false);
					sent = true;
				} catch(Exception ex) {
					Log.Error(ex, "Failed to register transaction through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip) {
				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount >= chainConfiguration.MinimumDispatchPeerCount) {

					IBlockchainGossipMessageSet gossipMessageSet = this.PrepareGossipTransactionMessageSet(transactionEnvelope);

					await Repeater.RepeatAsync(async () => {
						await  this.SendGossipTransaction(gossipMessageSet).ConfigureAwait(false);
						sent = true;
					}).ConfigureAwait(false);

					if(!sent) {
						throw new ApplicationException("Failed to send transaction");
					}

					await this.ConfirmTransactionSent(transactionEnvelope, correlationContext, gossipMessageSet.BaseHeader.Hash, lockContext).ConfigureAwait(false);

				} else {
					throw new ApplicationException("Failed to send transaction. Not enough peers available to send a gossip transactions.");
				}
			}

			if(!sent) {
				throw new ApplicationException("Failed to send transaction");
			}
		}

		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task PerformWebTransactionRegistration(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext, LockContext lockContext) {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			try {
				await Repeater.RepeatAsync(async () => {

					Dictionary<string, object> parameters = new Dictionary<string, object>();

					var bytes = transactionEnvelope.DehydrateEnvelope();
					parameters.Add("transactionEnvelope", bytes.Entry.ToBase64());
					bytes.Return();

					string url = transactionEnvelope.IsPresentation?chainConfiguration.WebPresentationRegistrationUrl:chainConfiguration.WebTransactionRegistrationUrl;
					string action = transactionEnvelope.IsPresentation?"presentation/register":"transactions/register";

					var result = await restUtility.Put(url, action, parameters).ConfigureAwait(false);
					
					// ok, check the result
					if(result.StatusCode == HttpStatusCode.OK) {
						// ok, all good

						return;
					}
					
					throw new ApplicationException("Failed to register transaction through web");
				}).ConfigureAwait(false);

				await this.ConfirmTransactionSent(transactionEnvelope, correlationContext, 0, lockContext).ConfigureAwait(false);

			} catch {
				throw;
			}
		}

		protected Task ConfirmTransactionSent(ITransactionEnvelope transactionEnvelope, CorrelationContext correlationContext, long messageHash, LockContext lockContext) {
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionSent(transactionEnvelope.Contents.Uuid), correlationContext);

			return IndependentActionRunner.RunAsync(lockContext, (lc) => {
				return Repeater.RepeatAsync(() => centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionCacheEntry(transactionEnvelope.Contents.Uuid, WalletTransactionCache.TransactionStatuses.Dispatched, messageHash, lc));
			}, (lc) => {
				ITransaction transaction = transactionEnvelope.Contents.RehydratedTransaction;

				return Repeater.RepeatAsync(async () => centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transactionEnvelope.Contents.Uuid, WalletTransactionHistory.TransactionStatuses.Dispatched, lc));
			});
		}
		
		/// <summary>
		///     try to register through the public webapi interface
		/// </summary>
		protected async Task PerformWebMessageRegistration(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext) {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			try {
				await Repeater.RepeatAsync(async () => {

					Dictionary<string, object> parameters = new Dictionary<string, object>();

					var bytes = messageEnvelope.DehydrateEnvelope();
					parameters.Add("transactionEnvelope", bytes.Entry.ToBase64());
					bytes.Return();

					string url = chainConfiguration.WebMessageRegistrationUrl;
					string action = "messages/register";

					var result = await restUtility.Put(url, action, parameters).ConfigureAwait(false);

					// ok, check the result
					if(result.StatusCode == HttpStatusCode.OK) {
						// ok, all good

						return;
					}
					
					throw new ApplicationException("Failed to register message through web");
				}).ConfigureAwait(false);

			} catch {
				throw;
			}
		}
		
		protected IBlockchainGossipMessageSet PrepareGossipTransactionMessageSet(ITransactionEnvelope transactionEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateTransactionCreatedGossipMessageSet(transactionEnvelope);

			this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected IBlockchainGossipMessageSet PrepareGossipBlockchainMessageMessageSet(IMessageEnvelope messageEnvelope) {

			// lets prepare our message first
			IBlockchainGossipMessageSet gossipMessageSet = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.CreateBlockchainMessageCreatedGossipMessageSet(messageEnvelope);

			this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.HashGossipMessage(gossipMessageSet);

			return gossipMessageSet;
		}

		protected async Task SendGossipTransaction(IBlockchainGossipMessageSet gossipMessageSet) {
			if(GlobalSettings.ApplicationSettings.P2PEnabled) {

				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoPeerConnections) {
					Log.Warning("No peers available. Gossip message announcing new transaction is not sent");
				} else {
					// ok, we are ready. lets send it out to the world!!  :)
					this.centralCoordinator.PostNewGossipMessage(gossipMessageSet);
				}

			} else {
				Log.Warning("p2p is not enabled. Gossip message announcing new transaction is not sent");
			}
		}
		
		public async Task DispatchNewMessage(IMessageEnvelope messageEnvelope, CorrelationContext correlationContext) {

			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			bool useWeb = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
			bool useGossip = chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
			bool sent = false;

			if(useWeb) {
				try {

					await this.PerformWebMessageRegistration(messageEnvelope, correlationContext).ConfigureAwait(false);
					sent = true;
				} catch(Exception ex) {
					Log.Error(ex, "Failed to register message through web");

					// do nothing, we will sent it on chain
					sent = false;
				}
			}

			if(!sent && useGossip) {
				if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.HasPeerConnections && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount >= chainConfiguration.MinimumDispatchPeerCount) {

					IBlockchainGossipMessageSet gossipMessageSet = this.PrepareGossipBlockchainMessageMessageSet(messageEnvelope);

					await Repeater.RepeatAsync(async () => {
						// ok, we are ready. lets send it out to the world!!  :)
						await this.SendGossipTransaction(gossipMessageSet).ConfigureAwait(false);
						sent = true;
					}).ConfigureAwait(false);

					if(!sent) {
						throw new ApplicationException("Failed to send message");
					}

				} else {
					throw new ApplicationException("Failed to send message. Not enough peers available to send a gossip message.");
				}
			}

			if(!sent) {
				throw new ApplicationException("Failed to send message");
			}
		}
		
		public async Task<bool> DispatchElectionMessages(List<IElectionCandidacyMessage> messages, LockContext lockContext) {
			if(messages != null && messages.Any()) {

				// well, seems we have messages!  lets send them out. first, prepare the envelopes

				var results = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.PrepareElectionMessageEnvelopes(messages, lockContext).ConfigureAwait(false);

				var sentMessages = new HashSet<Guid>();

				await Repeater.RepeatAsync(async () => {
					foreach(IMessageEnvelope envelope in results) {
						// if we repeat, lets not send the successful ones more than once.
						if(!sentMessages.Contains(envelope.ID)) {
							await this.DispatchNewMessage(envelope, new CorrelationContext()).ConfigureAwait(false);
							sentMessages.Add(envelope.ID);
						}
					}
				}).ConfigureAwait(false);
			}

			return true;
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