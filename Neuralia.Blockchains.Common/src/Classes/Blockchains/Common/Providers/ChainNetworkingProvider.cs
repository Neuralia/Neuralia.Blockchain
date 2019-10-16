using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
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
using Neuralia.Blockchains.Core.Workflows.Tasks;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainNetworkingProvider {

		event Action<int> PeerConnectionsCountUpdated;
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

		void Test();

		void RegisterChain(INetworkRouter transactionchainNetworkRouting);

		BlockchainNetworkingService.MiningRegistrationParameters RegisterMiningRegistrationParameters();
		void UnRegisterMiningRegistrationParameters();

		void ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection);

		void ReceiveConnectionsManagerTask(ISimpleTask task);
		void ReceiveConnectionsManagerTask(IColoredTask task);

		void PauseNetwork();
		void RestoreNetwork();

		
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
	public abstract class ChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public IBlockchainNetworkingService networkingService;
		public event Action<int> PeerConnectionsCountUpdated;
		
		public ChainNetworkingProvider(IBlockchainNetworkingService networkingService, CENTRAL_COORDINATOR centralCoordinator) {
			this.networkingService = networkingService;
			this.centralCoordinator = centralCoordinator;

			if(networkingService?.ConnectionStore != null) {
				this.networkingService.ConnectionStore.IsConnectableChange += (connectable) => {
				
					// alert that our connectable status has changed
					this.centralCoordinator.PostSystemEvent(SystemEventGenerator.ConnecableChanged(connectable));
				};

				this.networkingService.ConnectionStore.PeerConnectionsCountUpdated += (c) => this.PeerConnectionsCountUpdated?.Invoke(c);
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

		public void Test() {
			this.networkingService.ConnectionStore.AddAvailablePeerNode(new NodeAddressInfo("192.168.1.118", 33888, Enums.PeerTypes.Unknown), false);
		}

		public void PauseNetwork() {
			if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Active) {
				this.networkingService.NetworkingStatus = NetworkingService.NetworkingStatuses.Paused;
			}
		}
		
		public void RestoreNetwork() {
			if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
				this.networkingService.NetworkingStatus = NetworkingService.NetworkingStatuses.Active;
			}
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

				return !(chainConfiguration.RegistrationMethod == ChainConfigurations.RegistrationMethods.Gossip && this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.CurrentPeerCount < chainConfiguration.MinimumDispatchPeerCount);
			}
		}

		public bool NetworkingStarted => this.networkingService?.IsStarted ?? false;
		public bool NoNetworking => !this.NetworkingStarted;

		public void RegisterChain(INetworkRouter transactionchainNetworkRouting) {

			// validate the chain versions
			Func<SoftwareVersion, bool> versionValidationCallback = version => {

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
			};

			this.networkingService.RegisterChain(this.ChainId, this.PrepareChainSettings(), transactionchainNetworkRouting, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase, versionValidationCallback);
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

		public void ForwardValidGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection) {
			this.networkingService.ForwardValidGossipMessage(gossipMessageSet, connection);
		}

		public void ReceiveConnectionsManagerTask(ISimpleTask task) {
			this.networkingService.ConnectionsManager.ReceiveTask(task);
		}

		public void ReceiveConnectionsManagerTask(IColoredTask task) {
			this.networkingService.ConnectionsManager.ReceiveTask(task);
		}

		/// <summary>
		///     Prepare the publicly available chain settingsBase that we use
		/// </summary>
		/// <returns></returns>
		protected virtual ChainSettings PrepareChainSettings() {
			ChainSettings settings = new ChainSettings();

			// set the public chain settingsBase
			settings.BlockSavingMode = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().BlockSavingMode;

			return settings;
		}

	#region Scopped connections

		public List<string> AllIPCache => this.networkingService.ConnectionStore.AvailablePeerNodesCopy.Select(n => n.AdjustedIp).ToList();

		public List<PeerConnection> AllConnectionsList => this.networkingService.ConnectionStore.AllConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();

		public List<PeerConnection> SyncingConnectionsList => this.networkingService?.ConnectionStore?.SyncingConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();
		public int SyncingConnectionsCount => this.SyncingConnectionsList?.Count ?? 0;

		public List<PeerConnection> FullGossipConnectionsList => this.networkingService?.ConnectionStore?.FullGossipConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();
		public int FullGossipConnectionsCount => this.FullGossipConnectionsList?.Count ?? 0;

		public List<PeerConnection> BasicGossipConnectionsList => this.networkingService?.ConnectionStore?.BasicGossipConnectionsList.Where(this.FilterPeersPerChainVersion).ToList();
		public int BasicGossipConnectionsCount => this.BasicGossipConnectionsList?.Count ?? 0;

	#endregion

	}

}