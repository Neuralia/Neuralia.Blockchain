using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	/// <summary>
	///     Wrapper class around a TcpConnection with various connection about our peer
	/// </summary>
	public class PeerConnection : IDisposableExtended {

		public enum ConnectionStates {
			Unvalidated,
			Connecting,
			DeferredConfirmed,
			FullyConfirmed,
			Rejected
		}

		public enum Directions {
			Unknown,
			Incoming,
			Outgoing,
			Any
		}

		public const byte CONNECTION_PEER = 1;

		public readonly Dictionary<BlockchainType, ChainSettings> ChainSettings = new Dictionary<BlockchainType, ChainSettings>();

		public readonly SoftwareVersion clientSoftwareVersion = new SoftwareVersion();

		public readonly ITcpConnection connection;
		public readonly Directions direction;

		/// <summary>
		///     here we store the peer's reported version for each blockchain, and if we consider them to be valid as per our
		///     settings
		/// </summary>
		public readonly Dictionary<BlockchainType, bool> ValidBlockchainVersions = new Dictionary<BlockchainType, bool>();

		public DateTime ConnectionTime;

		public byte ConnectionType = CONNECTION_PEER;
		private bool isDisposed;

		public NodeAddressInfoList PeerNodes = new NodeAddressInfoList();

		public PeerConnection(ITcpConnection connection, Directions direction) {
			this.connection = connection;

			this.connection.Disposing += this.Dispose;
			this.direction = direction;
			this.ConnectionTime = DateTimeEx.CurrentTime;
		}

		public GeneralSettings GeneralSettings { get; private set; }

		public Guid ClientUuid => this.connection.ReportedUuid;

		public ConnectionStates ConnectionState { get; set; } = ConnectionStates.Unvalidated;

		/// <summary>
		///     the IP and listening port of the client, since it is different than the one we see here. used to propagate the peer
		///     ip.
		/// </summary>
		public NodeActivityInfo NodeActivityInfo { get; set; }

		/// <summary>
		///     the IP and listening port of the client, since it is different than the one we see here. used to propagate the peer
		///     ip.
		/// </summary>
		public NodeAddressInfo NodeAddressInfo => this.NodeActivityInfo?.Node;

		public NodeInfo NodeInfo {
			get => this.NodeAddressInfo?.PeerInfo ?? new NodeInfo(Enums.GossipSupportTypes.None, Enums.PeerTypes.Unknown);
			set => this.NodeAddressInfo.PeerInfo = value;
		}

		public string ScopedIp => this.NodeAddressInfo?.ScopedIp;
		public string ScopedAdjustedIp => this.NodeAddressInfo?.ScopedAdjustedIp;

		public string Ip => this.NodeAddressInfo?.Ip;
		public string AdjustedIp => this.NodeAddressInfo?.AdjustedIp;

		/// <summary>
		///     tells is if this connection is there but supports no chain at all
		/// </summary>
		/// <returns></returns>
		public bool IsChainless => !this.ValidBlockchainVersions.Any();

		/// <summary>
		///     if we have this IP whitelisted, then we dont remove it when culling connections
		/// </summary>
		public bool Locked => GlobalSettings.ApplicationSettings.Whitelist.Any(w => w.Ip == this.Ip);

		public bool NoSupportedChains => !this.ValidBlockchainVersions.Any();
		public bool NoValidChainVersion => !this.ValidBlockchainVersions.Values.All(v => v);

		/// <summary>
		///     this connection is confirmed when we trust it, but we are not fully sure yet that the other side has flagged this
		///     connection as ready. we can't quite use it
		///     to trigger workflows jsut yet. the other side may not be ready yet
		/// </summary>
		public bool IsConfirmed => (this.ConnectionState == ConnectionStates.DeferredConfirmed) || this.IsFullyConfirmed;

		/// <summary>
		///     this connection is fully trusted and we know the other side is ready to use it too.
		/// </summary>
		public bool IsFullyConfirmed => this.ConnectionState == ConnectionStates.FullyConfirmed;

		public bool IsUnvalidated => this.ConnectionState == ConnectionStates.Unvalidated;

		public bool IsDisposed {
			get => this.isDisposed || this.connection.IsDisposed;
			private set => this.isDisposed = value;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsBlockchainVersionValid(BlockchainType blockchainType) {
			return this.ValidBlockchainVersions[blockchainType];
		}

		public bool Equals(PeerConnection other) {
			return this.ClientUuid == other.ClientUuid;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			return (obj.GetType() == this.GetType()) && this.Equals((PeerConnection) obj);
		}

		public override int GetHashCode() {
			return this.ClientUuid.GetHashCode();
		}

		public event Action<PeerConnection> Disposed;

		/// <summary>
		///     Trigger when something is wrong with the connection and we need to clean up
		/// </summary>
		public void TriggerDisposed() {
			if(this.Disposed != null) {
				this.Disposed(this);
			}
		}

		public void SetPeerNodes(NodeAddressInfoList peerNodes) {
			this.PeerNodes = new NodeAddressInfoList(peerNodes.Nodes);
		}

		public void SetGeneralSettings(GeneralSettings generalSettings) {
			this.GeneralSettings = generalSettings;
		}

		public void SetChainSettings(BlockchainType chainType, ChainSettings chainSettings) {
			if(!this.ChainSettings.ContainsKey(chainType)) {
				this.ChainSettings.Add(chainType, chainSettings);
			}

			this.ChainSettings[chainType] = chainSettings;
		}

		public void AddSupportedChain(BlockchainType chainType, bool isValid) {
			if(!this.ValidBlockchainVersions.ContainsKey(chainType)) {
				this.ValidBlockchainVersions.Add(chainType, isValid);
			}

			this.ValidBlockchainVersions[chainType] = isValid;
		}

		public bool SupportsChain(BlockchainType chainType) {
			return this.ValidBlockchainVersions.ContainsKey(chainType);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				// set this now, to prevent loopbacks
				this.IsDisposed = true;

				try {
					this.connection.Dispose();
				} finally {
					this.TriggerDisposed();
				}
			}

		}

		~PeerConnection() {
			this.Dispose(false);
		}
	}
}