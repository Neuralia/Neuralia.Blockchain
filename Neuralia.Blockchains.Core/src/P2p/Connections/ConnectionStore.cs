using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using MoreLinq;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionStore : IDisposable2 {
		ulong MyClientIdNonce { get; }
		Guid MyClientUuid { get; }

		List<NodeAddressInfo> AvailablePeerNodesCopy { get; }
		List<NodeAddressInfo> IgnorePeerNodesCopy { get; }

		List<PeerConnection> AllConnectionsList { get; }
		Dictionary<Guid, PeerConnection> AllConnections { get; }
		int AllConnectionsCount { get; }

		int SimpleConnectionsCount { get; }
		List<PeerConnection> SimpleConnectionsList { get; }
		Dictionary<Guid, PeerConnection> SimpleConnections { get; }

		int SyncingConnectionsCount { get; }
		List<PeerConnection> SyncingConnectionsList { get; }
		Dictionary<Guid, PeerConnection> SyncingConnections { get; }

		int BasicGossipConnectionsCount { get; }
		List<PeerConnection> BasicGossipConnectionsList { get; }
		Dictionary<Guid, PeerConnection> BasicGossipConnections { get; }

		int CompleteConnectionsCount { get; }
		List<PeerConnection> CompleteConnectionsList { get; }
		Dictionary<Guid, PeerConnection> CompleteConnections { get; }

		int FullGossipConnectionsCount { get; }
		List<PeerConnection> FullGossipConnectionsList { get; }
		Dictionary<Guid, PeerConnection> FullGossipConnections { get; }

		int ActiveConnectionsCount { get; }
		bool ConnectionsSaturated { get; }

		event Action<bool> IsConnectibleChange;

		bool IsConnecting { get; }

		ImmutableList<IPAddress> OurAddresses { get; }

		int LocalPort { get; }
		IPAddress PublicIp { get; }
		Func<PeerConnection, bool> FilterSyncingIp { get; set; }
		bool LoopbackCheckEnabled { get; set; }
		ImmutableList<NodeAddressInfo> HardcodedNodes { get; }

		bool PeerConnectionExists(ITcpConnection tcpConnection, PeerConnection.Directions direction);
		bool PeerConnectionExists(NetworkEndPoint endpoint, PeerConnection.Directions direction);

		bool PeerConnecting(ITcpConnection tcpConnection, PeerConnection.Directions direction);
		bool PeerConnected(ITcpConnection tcpConnection);

		ConnectionStore.ConnectionTieResults BreakingConnectionTie(ITcpConnection contendedConnection, PeerConnection.Directions direction);

		event Action<IByteArray, PeerConnection, IEnumerable<Type>> DataReceived;
		event Action<int> PeerConnectionsCountUpdated;
		PeerConnection GetNewConnection(IPAddress address, int port, IPMode mode);
		PeerConnection GetNewConnection(NetworkEndPoint endpoint);

		NodeAddressInfoList GetHubNodes();

		void AddLocalAddress(IPAddress address);

		NodeActivityInfo GetNodeActivityInfo(NodeAddressInfo nodeAddressInfo);

		void FreeSomeIgnorePeers();

		NodeActivityInfo RemoveAvailablePeerNode(NodeAddressInfo node);
		void AddAvailablePeerNodes(Dictionary<Enums.PeerTypes, NodeAddressInfoList> nodes, bool force);
		void AddAvailablePeerNode(NodeAddressInfo node, bool force);
		void AddAvailablePeerNode(NodeActivityInfo nodeActivityInfo, bool force);
		void AddIgnorePeerNodes(List<NodeAddressInfo> nodes);
		void AddIgnorePeerNode(NodeAddressInfo nodeAddressInfo);
		void AddIgnorePeerNode(NodeActivityInfo nodeActivityInfo);
		void CleanIgnoredPeers();

		/// <summary>
		///     A method to call when updating the peers of an existing nodeAddressInfo
		/// </summary>
		/// <param name="connection"></param>
		/// <param name="nodes"></param>
		void UpdatePeerNodes(PeerConnection connection, NodeAddressInfoList nodes);

		void UpdatePeerNodes(PeerConnection connection, Dictionary<Enums.PeerTypes, NodeAddressInfoList> nodes);

		/// <summary>
		///     return our list of peer nodes for network messages
		/// </summary>
		/// <param name="limit">the max amount of peers to return, in case we have too much</param>
		/// <returns></returns>
		Dictionary<Enums.PeerTypes, NodeAddressInfoList> GetPeerNodeList(ConnectionStore.PeerSelectionHeuristic selectionHeuristic, List<NodeAddressInfo> excludeAddresses, int? limit = null);

		List<NodeAddressInfo> GetAvailablePeerNodes(List<NodeAddressInfo> excludeAddresses, bool onlyShareable, bool excludeConnected, bool onlyConnectable, int? limit = null);

		PeerConnection AddNewIncomingConnection(ITcpConnection tcpConnection);
		PeerConnection AddNewOutgoingConnection(ITcpConnection tcpConnection);
		void RemoveConnection(PeerConnection connection);
		void ConfirmConnection(PeerConnection connection);

		void AddChainSettings(NodeAddressInfo nodeAddressInfo, Dictionary<BlockchainType, ChainSettings> chainSettings);

		bool IsOurAddress(NodeAddressInfo nodeAddressInfo);
		bool IsOurAddressAndPort(NodeAddressInfo nodeAddressInfo);
		void AddPeerReportedPublicIp(IPAddress publicIp, ConnectionStore.PublicIpSource source);
		void AddPeerReportedPublicIp(string publicIp, ConnectionStore.PublicIpSource source);
		void AddPeerReportedConnectible(bool connectible, ConnectionStore.PublicIpSource source);
		

		bool IsNeuraliumHub(PeerConnection peerConnection);
		bool IsNeuraliumHub(NodeAddressInfo nodes);
		void FullyConfirmConnection(PeerConnection connection);
		void LoadStaticStartNodes();

		void SetConnectionUuidExistsCheck(ITcpConnection tcpConnection, NodeAddressInfo nodeAddressInfoInfo);
	}

	public static class ConnectionStore {

		public enum ConnectionTieResults {
			Challenger,
			Existing
		}

		public enum PeerSelectionHeuristic {
			Any,
			Powers,
			Simple
		}

		public enum PublicIpSource {
			STUN,
			Hub,
			Peer
		}
	}

	/// <summary>
	///     A class to manage and coordinate tcp Connections with other peers
	/// </summary>
	public class ConnectionStore<R> : IConnectionStore
		where R : IRehydrationFactory {

		/// <summary>
		///     a list of peer nodes we maintain for our peers to which we are not yet connected to. we may need them in hunger
		///     times
		/// </summary>
		/// <returns></returns>
		protected readonly ConcurrentDictionary<string, NodeActivityInfo> availablePeerNodes = new ConcurrentDictionary<string, NodeActivityInfo>();

		private readonly Timer connectionPollingTimer;

		/// <summary>
		///     used to count errors by peer
		/// </summary>
		private readonly Dictionary<string, int> errorCounts = new Dictionary<string, int>();

		protected readonly IGlobalsService globalsService;

		/// <summary>
		///     a list of peer nodes we, for a reason or another decided to ignore from now on. we wont connect to them again in
		///     this session
		/// </summary>
		/// <returns></returns>
		/// //TODO: make this list more complete, like what time was a nodeAddressInfo transactioned, so we can eventually remove it
		private readonly ConcurrentDictionary<string, IgnoreNodeActivityInfo> ignorePeerNodes = new ConcurrentDictionary<string, IgnoreNodeActivityInfo>();

		protected readonly List<IPV4CIDRRange> localCIDRV4ranges = new List<IPV4CIDRRange>();
		protected readonly List<IPV6CIDRRange> localCIDRV6ranges = new List<IPV6CIDRRange>();

		private readonly object locker = new object();

		private readonly HashSet<IPAddress> ourAddresses = new HashSet<IPAddress>();

		/// <summary>
		///     ignored peer nodes that have been promoted back. we use this to track their history, and know whent o remove the
		///     completely
		/// </summary>
		private readonly HashSet<string> promotedIgnoredNodes = new HashSet<string>();

		private readonly HashSet<Guid> removingConnections = new HashSet<Guid>();

		private readonly Dictionary<ConnectionStore.PublicIpSource, HashSet<IPAddress>> reportedPublicIps = new Dictionary<ConnectionStore.PublicIpSource, HashSet<IPAddress>>();
		private readonly Dictionary<ConnectionStore.PublicIpSource, bool> reportedConnectible = new Dictionary<ConnectionStore.PublicIpSource, bool>();

		protected readonly ITimeService timeService;

		private List<NodeAddressInfo> hardcodedNodes;

		public bool IsConnectible { get; private set; }
		private bool isChainSettingConsensusDirty;

		public event Action<bool> IsConnectibleChange;
		
		private ImmutableList<IPAddress> localIps;

		private NodeAddressInfoList neuraliumHubAddresses;

		public ConnectionStore(ServiceSet<R> serviceSet) {
			this.globalsService = serviceSet.GlobalsService;
			this.timeService = serviceSet.TimeService;

			//generate our unique ID
			this.MyClientUuid = ProtocolFactory.PROTOCOL_UUID;

			Log.Verbose("Our network protocol UUID is {0}", this.MyClientUuid);

			this.MyClientIdNonce = GlobalRandom.GetNextULong();

			this.localCIDRV4ranges.AddRange(IPUtils.GetDefaultV4Ranges());
			this.localCIDRV6ranges.AddRange(IPUtils.GetDefaultV6Ranges());

			// lets obtain our Ip Addresses
			this.QueryLocalIPAddress();

			// and the hub IPs
			this.QueryHubIps();

			// here we prepare the connection poller that will periodically check connections to ensure they are still active

			TimeSpan waitTime = TimeSpan.FromSeconds(3);

			int counter = 0;

			this.connectionPollingTimer = new Timer(state => {

				List<KeyValuePair<Guid, PeerConnection>> connections = null;

				lock(this.locker) {
					connections = this.AllConnections.ToList();
				}

				foreach(var connection in connections) {
					connection.Value.connection.CheckConnected();
				}

				counter++;

				if(counter == 100) {
					this.CleanIgnoredPeers();
					counter = 0;
				}
			}, this, waitTime, waitTime);
		}

		/// <summary>
		///     this contains connections that are in the process of confirming their connection. they may be valid, or not. but
		///     they are in the process of negociating
		/// </summary>
		public ConcurrentDictionary<Guid, PeerConnection> ConnectingConnections { get; } = new ConcurrentDictionary<Guid, PeerConnection>();

		/// <summary>
		///     This contains confirmed and active connections
		/// </summary>
		protected ConcurrentDictionary<Guid, PeerConnection> Connections { get; } = new ConcurrentDictionary<Guid, PeerConnection>();

		/// <summary>
		///     if true, the connection will check to avoid loopbacks
		/// </summary>
		public bool LoopbackCheckEnabled { get; set; } = true;

		public bool IsDisposed { get; private set; }

		public ulong MyClientIdNonce { get; }

		public Guid MyClientUuid { get; }

		public List<NodeAddressInfo> AvailablePeerNodesCopy => this.availablePeerNodes.Values.ToArray().Where(n => n.Shareable).Select(n => n.Node).ToList();

		public List<NodeAddressInfo> IgnorePeerNodesCopy => this.ignorePeerNodes.Values.ToArray().Select(n => n.Node).ToList();

		public virtual List<PeerConnection> AllConnectionsList => this.Connections.Values.ToList();
		public virtual Dictionary<Guid, PeerConnection> AllConnections => this.Connections.ToDictionary(c => c.Key, c => c.Value);
		public virtual int AllConnectionsCount => this.Connections.Count;
		public int ActiveConnectionsCount => this.Connections.Count;

		public virtual int SimpleConnectionsCount => this.Connections.Count(c => Enums.SimplePeerTypes.Contains(c.Value.PeerType));
		public virtual List<PeerConnection> SimpleConnectionsList => this.Connections.Values.Where(c => Enums.SimplePeerTypes.Contains(c.PeerType)).ToList();
		public virtual Dictionary<Guid, PeerConnection> SimpleConnections => this.Connections.Where(c => Enums.SimplePeerTypes.Contains(c.Value.PeerType)).ToDictionary(c => c.Key, c => c.Value);

		public virtual int SyncingConnectionsCount => this.SyncingConnections.Count;
		public virtual List<PeerConnection> SyncingConnectionsList => this.SyncingConnections.Values.ToList();
		public virtual Dictionary<Guid, PeerConnection> SyncingConnections => this.Connections.Where(c => c.Value.IsFullyConfirmed && Enums.SyncingPeerTypes.Contains(c.Value.PeerType) && (this.FilterSyncingIp?.Invoke(c.Value) ?? true)).ToDictionary(c => c.Key, c => c.Value);

		public virtual int BasicGossipConnectionsCount => this.Connections.Count(c => Enums.BasicGossipPeerTypes.Contains(c.Value.PeerType));
		public virtual List<PeerConnection> BasicGossipConnectionsList => this.Connections.Values.Where(c => Enums.BasicGossipPeerTypes.Contains(c.PeerType)).ToList();
		public virtual Dictionary<Guid, PeerConnection> BasicGossipConnections => this.Connections.Where(c => Enums.BasicGossipPeerTypes.Contains(c.Value.PeerType)).ToDictionary(c => c.Key, c => c.Value);

		public virtual int FullGossipConnectionsCount => this.Connections.Count(c => Enums.FullGossipPeerTypes.Contains(c.Value.PeerType));
		public virtual List<PeerConnection> FullGossipConnectionsList => this.Connections.Values.Where(c => Enums.FullGossipPeerTypes.Contains(c.PeerType)).ToList();
		public virtual Dictionary<Guid, PeerConnection> FullGossipConnections => this.Connections.Where(c => Enums.FullGossipPeerTypes.Contains(c.Value.PeerType)).ToDictionary(c => c.Key, c => c.Value);

		public virtual int CompleteConnectionsCount => this.Connections.Count(c => Enums.CompletePeerTypes.Contains(c.Value.PeerType));
		public virtual List<PeerConnection> CompleteConnectionsList => this.Connections.Values.Where(c => Enums.CompletePeerTypes.Contains(c.PeerType)).ToList();
		public virtual Dictionary<Guid, PeerConnection> CompleteConnections => this.Connections.Where(c => Enums.CompletePeerTypes.Contains(c.Value.PeerType)).ToDictionary(c => c.Key, c => c.Value);

		public IPAddress PublicIp { get; private set; }

		/// <summary>
		///     a method others can use to further filter syncing nodes
		/// </summary>
		public Func<PeerConnection, bool> FilterSyncingIp { get; set; }

		public int LocalPort => GlobalSettings.ApplicationSettings.port;

		public bool IsConnecting => this.ConnectingConnections.Any();

		public ImmutableList<IPAddress> OurAddresses => this.ourAddresses.ToImmutableList();

		/// <summary>
		///     this method will tell us if our connection count is saturated as per how much we want
		/// </summary>
		/// <returns></returns>
		public bool ConnectionsSaturated => this.ActiveConnectionsCount >= GlobalSettings.ApplicationSettings.maxPeerCount;

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public event Action<IByteArray, PeerConnection, IEnumerable<Type>> DataReceived;
		public event Action<int> PeerConnectionsCountUpdated;

		public PeerConnection GetNewConnection(IPAddress address, int port, IPMode mode) {
			// lets check if we already have a connection to this peer
			NetworkEndPoint endpoint = new NetworkEndPoint(address, port, mode);

			return this.GetNewConnection(endpoint);
		}

		public PeerConnection GetNewConnection(NetworkEndPoint endpoint) {
			// lets check if we already have a connection to this peer

			bool connectionExists = this.PeerConnectionExists(endpoint, PeerConnection.Directions.Outgoing);

			if(connectionExists) {
				throw new ApplicationException("A connection to this peer already exists");
			}

			return this.AddNewOutgoingConnection(this.CreateTcpConnection(endpoint, (e, cn) => {
				// exception occured
				lock(this.locker) {

					// the ReportedUuid is still empty here. so we must use the scopped ip and an identifier
					NodeAddressInfo node = new NodeAddressInfo(GetEndpointIp(cn.EndPoint), Enums.PeerTypes.Unknown);

					if(!this.errorCounts.ContainsKey(node.ScoppedIp)) {
						this.errorCounts.Add(node.ScoppedIp, 0);
					}

					this.errorCounts[node.ScoppedIp] += 1;

					if(this.errorCounts[node.ScoppedIp] >= 3) {
						// that's it, its too often we boycott the peer

						int errorCount = this.errorCounts[node.ScoppedIp];
						this.AddIgnorePeerNode(node);
						this.errorCounts.Remove(node.ScoppedIp);

						Log.Error(e, $"An exception occured on the network connection for peer {node.ScoppedAdjustedIp}. Strike {errorCount} of {3}. we are now ignoring them");
					} else {
						Log.Error(e, $"An exception occured on the network connection for peer {node.ScoppedAdjustedIp}. Strike {this.errorCounts[node.ScoppedIp]} of {3}");
					}
				}

			}));
		}

		public NodeActivityInfo RemoveAvailablePeerNode(NodeAddressInfo node) {
			NodeActivityInfo nai = null;

			lock(this.locker) {
				if(this.availablePeerNodes.ContainsKey(node.ScoppedIp)) {
					// remove only nodes that are not locked
					if(!this.availablePeerNodes[node.ScoppedIp].Node.Locked) {
						nai = this.availablePeerNodes.RemoveSafe(node.ScoppedIp);

						Log.Verbose($"removing peer ip from available list: {node.ScoppedAdjustedIp}.");
					}
				}
			}

			return nai;
		}

		public virtual void AddAvailablePeerNode(NodeAddressInfo node, bool force) {
			lock(this.locker) {
				var isLocal = this.IsIPLocalNode(node);

				if(!this.availablePeerNodes.ContainsKey(node.ScoppedIp) && !this.ignorePeerNodes.ContainsKey(node.ScoppedIp) && (force || !(isLocal.HasValue && isLocal.Value))) {
					this.availablePeerNodes.AddSafe(node.ScoppedIp, new NodeActivityInfo(node, true));

					Log.Verbose($"accepting potential future peer ip: {node.ScoppedAdjustedIp}.");

					this.CleanAvailablePeerNodes();
				}
			}
		}

		public void AddAvailablePeerNode(NodeActivityInfo nodeActivityInfo, bool force) {

			this.AddAvailablePeerNode(nodeActivityInfo.Node, force);
		}

		public void AddAvailablePeerNodes(Dictionary<Enums.PeerTypes, NodeAddressInfoList> nodes, bool force) {
			// make sure none of the IPS are ours
			foreach(var entry in nodes) {
				foreach(NodeAddressInfo node in this.FilterIps(entry.Value.Nodes.ToList())) {

					this.AddAvailablePeerNode(node, force);
				}
			}
		}

		public void AddIgnorePeerNodes(List<NodeAddressInfo> nodes) {
			lock(this.locker) {
				// make sure none of the IPS are ours
				foreach(NodeAddressInfo node in this.FilterIps(nodes)) // remove it from our available list
				{
					this.AddIgnorePeerNode(node);
				}
			}
		}

		public void AddIgnorePeerNode(NodeActivityInfo nodeActivityInfo) {
			this.AddIgnorePeerNode(nodeActivityInfo.Node);
		}

		public virtual void AddIgnorePeerNode(NodeAddressInfo nodeAddressInfo) {

			// make sure none of the IPS are ours
			// remove it from our available list

			string nodeKey = nodeAddressInfo.ScoppedIp;

			NodeActivityInfo nai = this.RemoveAvailablePeerNode(nodeAddressInfo);

			if(nai == null) {
				nai = new NodeActivityInfo(nodeAddressInfo, true);
			}

			lock(this.locker) {
				if(!this.ignorePeerNodes.ContainsKey(nodeKey) && !this.promotedIgnoredNodes.Contains(nodeKey)) {
					this.ignorePeerNodes.AddSafe(nodeKey, new IgnoreNodeActivityInfo(nai));
					Log.Verbose($"setting peer {nodeAddressInfo.ScoppedAdjustedIp} to be ignored from now on.");
				} else {

					// thats it, we remove this IP completely, we let it go!
					if(this.ignorePeerNodes.ContainsKey(nodeKey)) {
						this.ignorePeerNodes.RemoveSafe(nodeKey);
					}

					if(this.promotedIgnoredNodes.Contains(nodeKey)) {
						this.promotedIgnoredNodes.Remove(nodeKey);
					}
				}
			}
		}

		public void CleanIgnoredPeers() {
			foreach(var peer in this.ignorePeerNodes.Where(p => p.Value.MaxReached).ToArray()) {
				this.ignorePeerNodes.RemoveSafe(peer.Key);
			}
		}

		/// <summary>
		///     here we can attempt to free some ignore peers, give them a second try
		/// </summary>
		public void FreeSomeIgnorePeers() {
			lock(this.locker) {
				if(this.ignorePeerNodes.Any()) {

					// take 10%
					int takeCount = (int) (this.ignorePeerNodes.Count * 0.10);

					//take the oldest ones
					var restoreNodes = this.ignorePeerNodes.OrderBy(n => n.Value.IgnoreTimestamp).Take(takeCount).ToArray();

					foreach(var entry in restoreNodes) {
						this.PromoteIgnoredPeerNode(entry.Value.Node);
					}
				}
			}
		}

		/// <summary>
		///     A method to call when updating the peers of an existing nodeAddressInfo
		/// </summary>
		/// <param name="connection"></param>
		/// <param name="nodes"></param>
		public void UpdatePeerNodes(PeerConnection connection, NodeAddressInfoList nodes) {
			lock(this.locker) {
				connection.PeerNodes[nodes.PeerType] = new NodeAddressInfoList(nodes.PeerType, nodes);

				//we got new peer nodes. lets do something with his/her peers. we collect them
				this.NewPeersReceived(connection);
			}
		}

		public void UpdatePeerNodes(PeerConnection connection, Dictionary<Enums.PeerTypes, NodeAddressInfoList> nodes) {
			lock(this.locker) {
				foreach(var entry in nodes) {
					connection.PeerNodes[entry.Key] = new NodeAddressInfoList(entry.Key, entry.Value);
				}

				this.NewPeersReceived(connection);
			}
		}

		/// <summary>
		///     return our list of peer nodes available for connection
		/// </summary>
		/// <param name="excludeConnected">if true, we will remove the connections we are alraedy conencted to, or connecting to</param>
		/// <param name="limit">the max amount of peers to return, in case we have too much</param>
		/// <returns></returns>
		public virtual List<NodeAddressInfo> GetAvailablePeerNodes(List<NodeAddressInfo> excludeAddresses, bool onlyShareable, bool excludeConnected, bool onlyConnectable, int? limit = null) {

			List<NodeAddressInfo> nodes = null;

			lock(this.locker) {
				nodes = this.availablePeerNodes.Values.ToArray().Where(n => (!onlyConnectable || n.Node.IsConnectable) && (!onlyShareable || n.Shareable)).Select(n => n.Node).ToList();
			}

			//first thing, we remove ourslelves and the hubs from the list
			// also make sure they are unique, we could have doubles here. Also exclude the ones we were asked to exlude
			nodes = nodes.Where(n => !this.IsOurAddressAndPort(n) && !this.IsNeuraliumHub(n)).Distinct().ToList();

			if(excludeAddresses != null) {
				nodes = nodes.Where(a => !excludeAddresses.Contains(a)).ToList();
			}

			if(excludeConnected) {

				// make sure we are not connected to this IP
				nodes = nodes.Where(n => !this.PeerConnectionExists(n.NetworkEndPoint, PeerConnection.Directions.Any)).ToList();
			}

			// shuffle them to reveal the less possible about who we are actually connected to
			nodes.Shuffle();

			if(limit.HasValue) {
				nodes = nodes.Take(limit.Value).ToList();
			}

			return nodes.ToList();
		}

		/// <summary>
		///     return our list of peer nodes for network messages
		/// </summary>
		/// <param name="limit">the max amount of peers to return, in case we have too much</param>
		/// <returns></returns>
		public Dictionary<Enums.PeerTypes, NodeAddressInfoList> GetPeerNodeList(ConnectionStore.PeerSelectionHeuristic selectionHeuristic, List<NodeAddressInfo> excludeAddresses, int? limit = null) {

			lock(this.locker) {
				var nodes = this.GetAvailablePeerNodes(excludeAddresses, true, false, false);

				// we may want a certain limited amount
				if(limit.HasValue) {

					int actualLimit = limit.Value;

					if(selectionHeuristic == ConnectionStore.PeerSelectionHeuristic.Any) {
						nodes = nodes.Take(limit.Value).ToList();
					} else {
						int powers = 0;
						int simples = 0;

						if(selectionHeuristic == ConnectionStore.PeerSelectionHeuristic.Powers) {
							// take more of the powerful nodes
							powers = (int) (actualLimit * 0.75);
							simples = (int) (actualLimit * 0.25);

						} else if(selectionHeuristic == ConnectionStore.PeerSelectionHeuristic.Simple) {
							// take mode of the simple nodes
							powers = (int) (actualLimit * 0.25);
							simples = (int) (actualLimit * 0.75);
						}

						var adjustedNodes = new List<NodeAddressInfo>();

						adjustedNodes.AddRange(nodes.Where(n => Enums.CompletePeerTypes.Contains(n.PeerType)).Take(powers));
						adjustedNodes.AddRange(nodes.Where(n => !Enums.CompletePeerTypes.Contains(n.PeerType)).Take(simples));

						nodes = adjustedNodes;

					}
				}

				return this.GetSortedNodeAddressInfoLists(nodes.Distinct().ToList());
			}
		}

		public PeerConnection AddNewIncomingConnection(ITcpConnection tcpConnection) {
			lock(this.locker) {
				return this.AddNewConnection(tcpConnection, PeerConnection.Directions.Incoming);
			}
		}

		public PeerConnection AddNewOutgoingConnection(ITcpConnection tcpConnection) {
			lock(this.locker) {
				return this.AddNewConnection(tcpConnection, PeerConnection.Directions.Outgoing);
			}
		}

		public void RemoveConnection(PeerConnection connection) {

			lock(this.locker) {

				if(this.removingConnections.Contains(connection.ClientUuid)) {
					return;
				}

				this.removingConnections.Add(connection.ClientUuid);

				if(connection.ClientUuid == Guid.Empty) {
					Log.Verbose("Removing connection for as of yet unidentified client");
				} else {
					Log.Verbose($"Removing connection for client {connection.ClientUuid}");
				}

				try {
					if(this.Connections.ContainsKey(connection.ClientUuid)) {
						PeerConnection localConnection = this.Connections[connection.ClientUuid];

						if(localConnection.connection.InternalUuid == connection.connection.InternalUuid) {
							// remove it only if it is the same connection. otherwise it amy be another to the same peer. we dont want to remove rthe original
							if(!this.Connections.TryRemove(connection.ClientUuid, out PeerConnection outPeerInfo)) {
								this.Connections.RemoveSafe(connection.ClientUuid);
							}

							// lets add this connection back into our list of known peers, we may connect to it later again
							this.AddAvailablePeerNode(connection.NodeAddressInfoInfo, false);
						}
					}

					this.RemoveConnectingConnection(connection.connection);

					connection.Disposed -= this.InvalidatedConnection;
				} finally {
					connection?.Dispose();

					// alert the world we lost a peer
					this.PeerConnectionsCountUpdated?.Invoke(this.ActiveConnectionsCount);

					if(this.removingConnections.Contains(connection.ClientUuid)) {
						this.removingConnections.Remove(connection.ClientUuid);
					}
				}
			}
		}

		public NodeActivityInfo GetNodeActivityInfo(NodeAddressInfo nodeAddressInfo) {

			lock(this.locker) {
				if(this.availablePeerNodes.ContainsKey(nodeAddressInfo.ScoppedIp)) {
					return this.availablePeerNodes[nodeAddressInfo.ScoppedIp];
				}

				if(this.ignorePeerNodes.ContainsKey(nodeAddressInfo.ScoppedIp)) {
					return this.ignorePeerNodes[nodeAddressInfo.ScoppedIp];
				}
			}

			return null;
		}

		public void ConfirmConnection(PeerConnection connection) {
			lock(this.locker) {
				if(connection.ClientUuid == Guid.Empty) {
					throw new ApplicationException("Peer uuid cannot be empty");
				}

				if(this.PeerConnected(connection.connection)) {
					//TODO: this needs further testing
					if(!this.Connections[connection.connection.ReportedUuid].connection.EndPoint.Equals(connection.connection.EndPoint)) {
						// its a different connection than the existing one, so we can close it
						connection.connection.Dispose();
					}

					throw new ApplicationException("We are already connected to this peer!");
				}

				if(this.AllConnections.ContainsKey(connection.ClientUuid)) {
					// clear the old connection
					this.RemoveConnection(this.AllConnections[connection.ClientUuid]);
				}

				this.RemoveConnectingConnection(connection.connection);

				NodeAddressInfo nodeSpecs = GetEndpointInfoNode(connection);
				connection.NodeAddressInfoInfo.Ip = nodeSpecs.Ip;
				connection.NodeAddressInfoInfo.RealPort = nodeSpecs.RealPort;

				if(GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.DebugNetworkMode) {
					// for testing purposes, we allow to connect to our same ip
					if(this.IsOurAddress(connection.NodeAddressInfoInfo) && (connection.NodeAddressInfoInfo.RealPort == this.LocalPort)) {
						throw new ApplicationException("We cannot connect to ourselves. connection refused.");
					}
				} else {
					if(this.IsOurAddress(connection.NodeAddressInfoInfo)) {
						throw new ApplicationException("We cannot connect to ourselves. connection refused.");
					}
				}

				this.Connections.AddSafe(connection.ClientUuid, connection);

				connection.ConnectionState = PeerConnection.ConnectionStates.DeferredConfirmed;

				// time we accepted this connection
				connection.ConnectionTime = this.timeService.CurrentRealTime;

				Log.Information($"accepting connection from peer {connection.ClientUuid} with scopped ip {connection.NodeAddressInfoInfo.ScoppedAdjustedIp}");

				NodeAddressInfo nodeInfo = GetEndpointInfoNode(connection);

				// if this IP was ignored, we can trust it again now
				this.PromoteIgnoredPeerNode(nodeInfo);

				// a new connection, lets make sure it will be in our list of known Connections
				this.AddAvailablePeerNode(nodeInfo, false);

				// alert the world we have a new peer!
				this.PeerConnectionsCountUpdated?.Invoke(this.ActiveConnectionsCount);

				//we got new peer nodes. lets do something with his/her peers. we collect them
				this.NewPeersReceived(connection);
			}
		}

		public void FullyConfirmConnection(PeerConnection connection) {
			connection.ConnectionState = PeerConnection.ConnectionStates.FullyConfirmed;
		}

		/// <summary>
		///     Here we determine if the peer is a defined neuralium hub (and thus trustworthy)
		/// </summary>
		/// <param name="peerConnection"></param>
		/// <returns></returns>
		public bool IsNeuraliumHub(PeerConnection peerConnection) {
			lock(this.locker) {
				return this.neuraliumHubAddresses?.Nodes.Any(n => n.EqualsIp(peerConnection.NodeAddressInfoInfo)) ?? false;
			}
		}

		public bool IsNeuraliumHub(NodeAddressInfo nodes) {
			lock(this.locker) {
				return this.neuraliumHubAddresses?.Nodes.Any(n => n.EqualsIp(nodes)) ?? false;
			}
		}

		/// <summary>
		///     here we determine if this is a local address
		/// </summary>
		/// <returns></returns>
		public bool IsOurAddress(NodeAddressInfo nodeAddressInfo) {
			lock(this.locker) {
				if(IPAddress.IsLoopback(nodeAddressInfo.AdjustedAddress) || this.ourAddresses.Any(a => a.Equals(nodeAddressInfo.Address))) {
					return this.LocalPort == nodeAddressInfo.RealPort;
				}

				return false;
			}
		}

		public bool IsOurAddressAndPort(NodeAddressInfo nodeAddressInfo) {
			return this.IsOurAddress(nodeAddressInfo) && (nodeAddressInfo.RealPort == this.LocalPort);
		}

		public void AddPeerReportedPublicIp(string publicIp, ConnectionStore.PublicIpSource source) {
			this.AddPeerReportedPublicIp(IPAddress.Parse(publicIp), source);
		}

		public void AddPeerReportedPublicIp(IPAddress publicIp, ConnectionStore.PublicIpSource source) {

			// first, ensure its not a local IP, otherwise we wont use it
			var result = this.IsIPLocalAddress(publicIp);

			if(result.HasValue && result.Value) {
				return;
			}

			lock(this.locker) {

				if(!this.reportedPublicIps.ContainsKey(source)) {
					this.reportedPublicIps.Add(source, new HashSet<IPAddress>());
				}

				if(!this.reportedPublicIps[source].Contains(publicIp)) {
					this.reportedPublicIps[source].Add(publicIp);
				}

				(IPAddress result, ConsensusUtilities.ConsensusType concensusType) results = ConsensusUtilities.GetConsensus(this.reportedPublicIps.SelectMany(s => s.Value));

				if((results.concensusType == ConsensusUtilities.ConsensusType.Undefined) || (results.concensusType == ConsensusUtilities.ConsensusType.Split)) {
					// not good, we can't trust what we just received. lets see if we have a trustworthy source
					if(this.reportedPublicIps.ContainsKey(ConnectionStore.PublicIpSource.Hub) && this.reportedPublicIps[ConnectionStore.PublicIpSource.Hub].Any()) {
						this.PublicIp = this.reportedPublicIps[ConnectionStore.PublicIpSource.Hub].Shuffle().First();
					} else if(this.reportedPublicIps.ContainsKey(ConnectionStore.PublicIpSource.STUN) && this.reportedPublicIps[ConnectionStore.PublicIpSource.STUN].Any()) {
						this.PublicIp = this.reportedPublicIps[ConnectionStore.PublicIpSource.STUN].Shuffle().First();
					} else {
						this.PublicIp = null;
					}
				} else {
					this.PublicIp = results.result;
				}

				this.ourAddresses.Clear();

				if(this.PublicIp != null) {
					NodeAddressInfo nodeAddressInfo = new NodeAddressInfo(this.PublicIp, Enums.PeerTypes.FullNode);
					this.OurAddresses.Add(nodeAddressInfo.Address);
				}

				foreach(IPAddress localIp in this.localIps) {
					NodeAddressInfo nodeAddressInfo = new NodeAddressInfo(localIp, Enums.PeerTypes.FullNode);

					if(!this.ourAddresses.Contains(nodeAddressInfo.Address)) {
						this.ourAddresses.Add(nodeAddressInfo.Address);
					}
				}
			}
		}

		public void AddPeerReportedConnectible(bool connectible, ConnectionStore.PublicIpSource source) {
			// first, ensure its not a local IP, otherwise we wont use it

			lock(this.locker) {

				if(!this.reportedConnectible.ContainsKey(source)) {
					this.reportedConnectible.Add(source, connectible);
				}

				this.reportedConnectible[source] = connectible;
				
				(bool result, ConsensusUtilities.ConsensusType concensusType) results = ConsensusUtilities.GetConsensus(this.reportedConnectible.Select(s => s.Value));

				if((results.concensusType == ConsensusUtilities.ConsensusType.Undefined) || (results.concensusType == ConsensusUtilities.ConsensusType.Split)) {

					results.result = false;
				} 

				if(this.IsConnectible != results.result) {
					this.IsConnectible = results.result;
					
					// alert that our connectible status has changed
					this.IsConnectibleChange?.Invoke(this.IsConnectible);
				}
			}
		}

		public ImmutableList<NodeAddressInfo> HardcodedNodes {
			get {
				lock(this.locker) {
					if(this.hardcodedNodes == null) {
						this.hardcodedNodes = new List<NodeAddressInfo>();

						foreach(string entry in this.globalsService.HardcodedNodes) {

							try {
								this.hardcodedNodes.Add(new NodeAddressInfo(Dns.GetHostAddresses(entry).First(), Enums.PeerTypes.FullNode));
							} catch(Exception ex) {
								// lets try it as text only
								try {
									this.hardcodedNodes.Add(new NodeAddressInfo(entry, Enums.PeerTypes.FullNode));
								} catch(Exception ex2) {
									// lets just die silently if it fails
								}
							}
						}
					}

					return this.hardcodedNodes.ToImmutableList();
				}
			}
		}

		/// <summary>
		///     Reload any peer ip we can salvage from our static sources.false the config file and the hardcoded ips
		/// </summary>
		public virtual void LoadStaticStartNodes() {
			lock(this.locker) {
				if((this.globalsService.HardcodedNodes.Count == 0) && (GlobalSettings.ApplicationSettings.Nodes.Count == 0)) {
					return;
				}

				// first thing we do it gather every possible list of peers we can find and add them to our peer list. we will need this to start
				var startNodes = new Dictionary<Enums.PeerTypes, NodeAddressInfoList>();

				//lets start with the hardcoded ones, best possible start
				startNodes[Enums.PeerTypes.FullNode] = new NodeAddressInfoList(Enums.PeerTypes.FullNode, this.HardcodedNodes.Distinct().Where(node => !this.IsOurAddress(node)));

				// lets collect the extra nodes provided by configuration
				startNodes[Enums.PeerTypes.Unknown] = new NodeAddressInfoList(Enums.PeerTypes.Unknown, GlobalSettings.ApplicationSettings.Nodes.Select(n => new NodeAddressInfo(n.ip, n.port, Enums.PeerTypes.Unknown)).Distinct().Where(node => !this.IsOurAddress(node)));

				// here we go. just make sure we oursevles are not in this list and lets go forward
				this.AddAvailablePeerNodes(startNodes, true);
			}
		}

		/// <summary>
		///     Get the address info of a neuralium Hub
		/// </summary>
		/// <returns></returns>
		public virtual NodeAddressInfoList GetHubNodes() {
			lock(this.locker) {
				this.QueryHubIps();

				return this.neuraliumHubAddresses;
			}
		}

		/// <summary>
		///     Add an IP that we discovered refers to ourselves.
		/// </summary>
		/// <param name="adress"></param>
		public void AddLocalAddress(IPAddress address) {
			lock(this.locker) {
				NodeAddressInfo nodeAddressInfo = new NodeAddressInfo(address, Enums.PeerTypes.FullNode);
				this.OurAddresses.Add(nodeAddressInfo.Address);
			}
		}

		/// <summary>
		///     If two ,achines connect to each other at the same time, we get a tie. The breaking rule is:
		///     lowest number of the ID, gets to be the outgoing connection
		/// </summary>
		/// <param name="contendedConnection"></param>
		/// <param name="direction"></param>
		/// <returns>the connection to be removed. either the challenger or our own existing</returns>
		public ConnectionStore.ConnectionTieResults BreakingConnectionTie(ITcpConnection contendedConnection, PeerConnection.Directions direction) {
			PeerConnection existingConnection = null;

			ConnectionStore.ConnectionTieResults RemoveChallenger() {
				// no need to dispose the connection as the other side will do it
				return ConnectionStore.ConnectionTieResults.Challenger;
			}

			ConnectionStore.ConnectionTieResults RemoveExisting() {
				this.RemoveConnectingConnection(existingConnection.connection);

				// no need to dispose the connection as the other side will do it
				return ConnectionStore.ConnectionTieResults.Existing;
			}

			if(this.ConnectingConnections.ContainsKey(contendedConnection.InternalUuid)) {
				existingConnection = this.ConnectingConnections[contendedConnection.InternalUuid];
			}

			// first, make sure the other side is connected or they lose
			if(!contendedConnection.CheckConnected()) {
				return RemoveChallenger();
			}

			// now see if we are connected or we lose
			if(!(existingConnection?.connection.CheckConnected() ?? false)) {
				return RemoveExisting();
			}

			// both are still connected. we will determine the lowest hash which will get to be the one connecting to the other
			BigInteger challengerId = new BigInteger(contendedConnection.ReportedUuid.ToByteArray().Concat(new byte[] {0}).ToArray());
			BigInteger localId = new BigInteger(this.MyClientUuid.ToByteArray().Concat(new byte[] {0}).ToArray());

			// ok, let's apply our tie breaker
			PeerConnection.Directions dominantDirection = localId < challengerId ? PeerConnection.Directions.Outgoing : PeerConnection.Directions.Incoming;

			if(existingConnection.direction == dominantDirection) {
				return RemoveChallenger();
			}

			return RemoveExisting();
		}

		/// <summary>
		///     register for a UUid exists event
		/// </summary>
		/// <param name="tcpConnection"></param>
		/// <param name="nodeAddressInfoInfo"></param>
		/// <exception cref="InvalidPeerException"></exception>
		public void SetConnectionUuidExistsCheck(ITcpConnection tcpConnection, NodeAddressInfo nodeAddressInfoInfo) {
			if(!tcpConnection.IsConnectedUuidProvidedSet) {
				tcpConnection.ConnectedUuidProvided += reportedUuid => {

					// nothing to do really, we will check later
				};
			}
		}

		/// <summary>
		///     Verify if the IP address is the same as ours. does not check for port
		/// </summary>
		/// <param name="publicIp"></param>
		/// <returns></returns>
		protected virtual bool? IsIPLocalAddress(IPAddress publicIp) {

			if(this.PublicIp != null) {
				NodeAddressInfo node = new NodeAddressInfo(publicIp, Enums.PeerTypes.Unknown);
				NodeAddressInfo ourNode = new NodeAddressInfo(this.PublicIp, Enums.PeerTypes.Unknown);

				if(ourNode == node) {
					return true;
				}
			}

			return false;
		}

		/// <summary>
		///     Verify if the IP address is the same as ours, port included
		/// </summary>
		/// <param name="publicIp"></param>
		/// <returns></returns>
		protected virtual bool? IsIPLocalNode(NodeAddressInfo node) {

			var addressCheck = this.IsIPLocalAddress(node.Address);

			return addressCheck.HasValue ? addressCheck.Value && (node.RealPort == this.LocalPort) : (bool?) null;
		}

		/// <summary>
		///     checks if we fall in the same local CIDR range.
		/// </summary>
		/// <param name="publicIp"></param>
		/// <returns></returns>
		protected virtual bool IsIPLocalNetwork(IPAddress publicIp) {

			NodeAddressInfo node = new NodeAddressInfo(publicIp, Enums.PeerTypes.Unknown);

			if(node.IsIpV4) {
				foreach(IPV4CIDRRange range in this.localCIDRV4ranges) {

					(IPAddress lower, IPAddress upper) ranges = IPUtils.GetIPV4CIDRRange(range);

					if(IPUtils.IsIPV4InCIDRRange(node.Address.MapToIPv4(), ranges)) {
						return true;
					}
				}
			} else {
				foreach(IPV6CIDRRange range in this.localCIDRV6ranges) {
					if(IPUtils.IsIPV6InCIDRRange(node.Address.MapToIPv6(), range)) {
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		///     enforce the cap on the amount of IPs we keep in memory
		/// </summary>
		protected void CleanAvailablePeerNodes() {
			if(this.availablePeerNodes.Count > GlobalSettings.ApplicationSettings.MaximumIpCacheCount) {
				// TODO: use the reliability index to weed out bad ips
				try {
					foreach(var removing in this.availablePeerNodes.Where(n => !n.Value.Node.Locked).OrderByDescending(n => n.Value.Timestamp).Skip(GlobalSettings.ApplicationSettings.MaximumIpCacheCount)) {
						this.availablePeerNodes.RemoveSafe(removing.Key);
					}
				} catch {
					// no ned to do anything, its not so important
				}
			}
		}

		/// <summary>
		///     take an ignored peer and return to our main list
		/// </summary>
		/// <param name="nodeAddressInfo"></param>
		public void PromoteIgnoredPeerNode(NodeAddressInfo nodeAddressInfo) {
			if(this.ignorePeerNodes.ContainsKey(nodeAddressInfo.ScoppedIp)) {
				IgnoreNodeActivityInfo inai = this.ignorePeerNodes.RemoveSafe(nodeAddressInfo.ScoppedIp);
				this.AddAvailablePeerNode(new NodeActivityInfo(inai), false);
				this.promotedIgnoredNodes.Add(nodeAddressInfo.ScoppedIp);
			}
		}

		public void RemoveConnectingConnection(ITcpConnection tcpConnection) {
			lock(this.locker) {
				if(this.ConnectingConnections.ContainsKey(tcpConnection.InternalUuid)) {
					this.ConnectingConnections.RemoveSafe(tcpConnection.InternalUuid);
				}
			}
		}

		protected void QueryHubIps() {
			lock(this.locker) {
				if(this.neuraliumHubAddresses == null) {
					this.neuraliumHubAddresses = new NodeAddressInfoList(Enums.PeerTypes.Hub);

					if(GlobalSettings.ApplicationSettings.EnableHubs) {

						Log.Verbose("Query hubs IPs");
						IPHostEntry hosts = null;

						try {
							string hubsAddress = GlobalSettings.ApplicationSettings.HubsAddress;

							hosts = Dns.GetHostEntry(hubsAddress);
						} catch(Exception ex) {
							Log.Error(ex, "Failed to query neuralium hubs");
						}

						if((hosts?.AddressList != null) && hosts.AddressList.Any()) {
							var ips = hosts.AddressList.Select(a => new NodeAddressInfo(a, Enums.PeerTypes.Hub)).ToList();

							foreach(NodeAddressInfo entry in ips) {
								Log.Verbose($"Adding hub IP address: {entry.ScoppedAdjustedIp}");
							}

							this.neuraliumHubAddresses.AddNodes(ips);
						} else {
							Log.Verbose("No hub IPs found");
						}
					}
				}
			}
		}

		/// <summary>
		///     Build the dictionary sorted structure from a list
		/// </summary>
		/// <param name="nodes"></param>
		/// <returns></returns>
		private Dictionary<Enums.PeerTypes, NodeAddressInfoList> GetSortedNodeAddressInfoLists(IEnumerable<NodeAddressInfo> nodes) {
			return nodes.GroupBy(n => n.PeerType).ToDictionary(g => g.Key, g => new NodeAddressInfoList(g.Key, g));
		}

		/// <summary>
		///     Here we determine all the IPS that define US
		/// </summary>
		public void QueryLocalIPAddress() {
			lock(this.locker) {
				var ipList = new List<IPAddress>();
				ipList.AddRange(new[] {IPAddress.Parse("0.0.0.0"), IPAddress.Parse("127.0.0.1"), IPAddress.Parse("::1"), IPAddress.Parse("::")});

				if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.localhostOnly && NetworkInterface.GetIsNetworkAvailable()) {
					if(GlobalSettings.ApplicationSettings.UseSTUNServer) {
						try {
							// a STUN server is the only way to get our address. otherwise, we will have to ask peers to tell us...
							STUNClient stunClient = new STUNClient();
							var task = stunClient.QueryAddressAsync();
							task.Wait();

							if(task.Result.SuccessResults != null) {
								this.AddPeerReportedPublicIp(task.Result.SuccessResults.PublicEndPoint.Address, ConnectionStore.PublicIpSource.STUN);
							}
						} catch(Exception ex) {

						}
					}

					try {
						// now add all our DNS entries
						foreach(NetworkInterface entry in NetworkInterface.GetAllNetworkInterfaces()) {

							IPInterfaceProperties props = entry.GetIPProperties();

							foreach(UnicastIPAddressInformation addressInfo in props.UnicastAddresses) {

								NodeAddressInfo node = new NodeAddressInfo(addressInfo.Address, Enums.PeerTypes.Unknown);

								if(node.IsIpV4) {
									IPV4CIDRRange range = IPUtils.GenerateCIDRV4Range(node.Address.MapToIPv4(), addressInfo.IPv4Mask);

									if(!this.localCIDRV4ranges.Contains(range)) {
										this.localCIDRV4ranges.Add(range);
									}
								}
							}

							ipList.AddRange(props.UnicastAddresses.Select(a => a.Address));
							ipList.AddRange(props.MulticastAddresses.Select(a => a.Address));
						}

						// as a fallback, lets try this technique (will most probably return localhost though, so its not perfect)
						IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
						ipList.AddRange(host.AddressList);
					} catch(Exception ex) {
						Log.Verbose(ex, "Failed to query network interfaces ");
					}

					if(!ipList.Any()) {

						// well, if we have nothing, lets check with a UDP test and get the interface IP
						foreach(string dnsip in new[] {"8.8.8.8", "8.8.4.4", "208.67.222.222", "208.67.222.220", "209.244.0.3", "208.244.0.4", "156.154.70.1", "156.154.71.1", "46.151.208.154", "128.199.248.105", "216.146.35.35", "216.146.36.36"}) {
							try {
								IPAddress address = IPAddress.Parse(dnsip);

								using(Socket socket = new Socket(address.AddressFamily, SocketType.Dgram, 0)) {
									socket.Connect(address, 65530);

									if(socket.LocalEndPoint is IPEndPoint endPoint) {
										ipList.Add(endPoint.Address);
									}

									break;
								}
							} catch(Exception ex) {

							}
						}
					}
				}

				this.localIps = ipList.Distinct().ToImmutableList();
			}
		}

		protected virtual IEnumerable<NodeAddressInfo> FilterIps(List<NodeAddressInfo> nodes) {
			lock(this.locker) {
				var ips = this.FilterLocalIp(nodes);

				if(this.neuraliumHubAddresses == null) {
					this.GetHubNodes();
				}

				// remove the neuralium hib nodes too. we dont want to bother them unless we explicitly need their help
				if((this.neuraliumHubAddresses != null) && this.neuraliumHubAddresses.Nodes.Any()) {
					ips = ips.Where(n => !this.neuraliumHubAddresses.Nodes.Contains(n));
				}

				return ips;
			}
		}

		/// <summary>
		///     remove our own IP from a list of nodes
		/// </summary>
		/// <param name="nodes"></param>
		/// <returns></returns>
		private IEnumerable<NodeAddressInfo> FilterLocalIp(List<NodeAddressInfo> nodes) {
			lock(this.locker) {
				return nodes.Where(n => {

					if(this.OurAddresses.Contains(n.Address)) {
						return n.RealPort != this.LocalPort;
					}

					return true;
				});
			}
		}

		protected void TriggerDataReceived(IByteArray data, PeerConnection peer, IEnumerable<Type> triggerTypes) {
			this.DataReceived?.Invoke(data, peer, triggerTypes);
		}

		protected virtual ITcpConnection CreateTcpConnection(NetworkEndPoint remoteEndPoint, TcpConnection.ExceptionOccured exceptionCallback, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) {
			return new TcpDuplexConnection(remoteEndPoint, exceptionCallback);
		}

		protected PeerConnection AddNewConnection(ITcpConnection tcpConnection, PeerConnection.Directions direction) {

			lock(this.locker) {

				PeerConnection peerConnection = this.WrapConnection(tcpConnection, direction);

				// add it to the connecting state
				this.ConnectingConnections.AddSafe(peerConnection.connection.InternalUuid, peerConnection);

				peerConnection.ConnectionState = PeerConnection.ConnectionStates.Connecting;

				return peerConnection;
			}
		}

		protected PeerConnection WrapConnection(ITcpConnection tcpConnection, PeerConnection.Directions direction) {
			// incoming client ID, we always assign them a unique ID in our lot

			lock(this.locker) {
				PeerConnection connection = new PeerConnection(tcpConnection, direction);

				// set various details about the connection
				NodeAddressInfo nodeSpecs = GetEndpointInfoNode(connection);

				NodeAddressInfo node = new NodeAddressInfo(nodeSpecs.Ip, nodeSpecs.RealPort, Enums.PeerTypes.Unknown);

				connection.NodeActivityInfo = this.GetNodeActivityInfo(node);

				if(connection.NodeActivityInfo == null) {
					connection.NodeActivityInfo = new NodeActivityInfo(node, true);
				}

				// called if something wrong happens to the connection and we must delete it
				connection.Disposed += this.InvalidatedConnection;

				var acceptedTriggerList = new[] {typeof(WorkflowTriggerMessage<R>)}.ToList();

				connection.connection.Connected += () => {
					if(direction == PeerConnection.Directions.Outgoing) {
						// we know it can connect
						node.IsConnectable = true;
						connection.NodeAddressInfoInfo.IsConnectable = true;
					}
				};
				
				connection.connection.DataReceived += buffer => {
					this.DataReceived?.Invoke(buffer, connection, acceptedTriggerList);
				};

				connection.connection.Disconnected += (sender, resultArgs) => {
					lock(this.locker) {
						this.RemoveConnection(connection);
					}
				};

				// validate the connection
				this.SetConnectionUuidExistsCheck(connection.connection, connection.NodeAddressInfoInfo);

				return connection;
			}
		}

		private void ConnectionDisconnectedHandler(object sender, DisconnectedEventArgs resultArgs) {

		}

		private void InvalidatedConnection(PeerConnection connection) {
			this.RemoveConnection(connection);
		}

		/// <summary>
		///     This method will ensure we add the new nodes to our various curated lists
		/// </summary>
		/// <param name="connection"></param>
		/// <param name="nodes"></param>
		protected virtual void NewPeersReceived(PeerConnection connection) {
			lock(this.locker) {
				var uniques = new Dictionary<Enums.PeerTypes, NodeAddressInfoList>();

				foreach(var entry in connection.PeerNodes) {
					if(!uniques.ContainsKey(entry.Key)) {
						uniques.Add(entry.Key, new NodeAddressInfoList(entry.Key, entry.Value.Nodes));
					}
				}

				// thats it, these are new IPs. lets store them, we may need them
				this.AddAvailablePeerNodes(uniques, false);
			}
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				lock(this.locker) {
					this.connectionPollingTimer?.Dispose();

					// release other disposable objects  
					foreach(PeerConnection connectionInfo in this.Connections.Values.ToArray()) {
						connectionInfo.Dispose();
					}

					this.IsDisposed = true;
				}
			}
		}

		~ConnectionStore() {
			this.Dispose(false);
		}

	#region Endpoint Utility Methods

		public static NetworkEndPoint GetEndpoint(PeerConnection peerConnection) {
			return peerConnection.connection.EndPoint;
		}

		/// <summary>
		///     Be careful with this method!! an incomming connection gets a very different port on us than they themselves use to
		///     listen for Connections.
		/// </summary>
		/// <param name="endpoint"></param>
		/// <returns></returns>
		public static int? GetEndpointPort(NetworkEndPoint endpoint) {
			IPEndPoint castedEndPoint = endpoint.EndPoint;

			// here we only return the port if it is different than our default port. otherwise we return null
			return castedEndPoint.Port == GlobalsService.DEFAULT_PORT ? null : (int?) castedEndPoint.Port;
		}

		public static IPAddress GetEndpointIp(NetworkEndPoint endpoint) {
			IPEndPoint castedEndPoint = endpoint.EndPoint;

			return castedEndPoint.Address;
		}

		public static (IPAddress IPAddress, int? port) GetEndpointInfo(PeerConnection peerConnection) {
			return (GetEndpointIp(GetEndpoint(peerConnection)), peerConnection.NodeAddressInfoInfo.RealPort);
		}

		/// <summary>
		///     Careful with this method!! an incomming connection gets a very different port on us than they themselves use to
		///     listen for Connections.
		/// </summary>
		/// <param name="endpoint"></param>
		/// <returns></returns>
		public static NodeAddressInfo GetEndpointInfoNode(NetworkEndPoint endpoint, Enums.PeerTypes peerType) {
			return new NodeAddressInfo(GetEndpointIp(endpoint), GetEndpointPort(endpoint), peerType);
		}

		public static NodeAddressInfo GetEndpointInfoNode(PeerConnection peerConnection) {
			NetworkEndPoint endpoint = GetEndpoint(peerConnection);

			int? port = null;

			if(peerConnection.NodeAddressInfoInfo != null) {
				port = peerConnection.NodeAddressInfoInfo.RealPort;
			} else {
				port = GetEndpointPort(endpoint);
			}

			return new NodeAddressInfo(GetEndpointIp(endpoint), port, peerConnection.PeerType);
		}

		public static NetworkEndPoint CreateEndpoint(PeerConnection peerConnection) {
			return CreateEndpoint(GetEndpointInfoNode(peerConnection));
		}

		public static NetworkEndPoint CreateEndpoint(NodeAddressInfo nodeAddressInfo) {
			return new NetworkEndPoint(nodeAddressInfo.Address, nodeAddressInfo.RealPort, IPMode.IPv6);
		}

	#endregion

	#region Peer chain settingsBase & consensus

		//TODO: revise all this!!!!
		/// <summary>
		///     Here store the peer chain support optionsBase. since we may receive different version from different peers, we
		///     store
		///     them per peer (key).
		///     we can later analysze them and figure out if there are any bad actors.
		/// </summary>
		/// <returns></returns>
		public readonly Dictionary<string, Dictionary<BlockchainType, ChainSettings>> chainSettings = new Dictionary<string, Dictionary<BlockchainType, ChainSettings>>();

		/// <summary>
		///     This is the consensus of all peers for the chain values
		/// </summary>
		private readonly Dictionary<BlockchainType, ChainSettings> chainSettingsConsensus = null;

		private readonly object chainSettingsLocker = new object();

		public void AddChainSettings(NodeAddressInfo nodeAddressInfo, Dictionary<BlockchainType, ChainSettings> chainSettings) {
			lock(this.chainSettingsLocker) {
				nodeAddressInfo.SetChainSettings(chainSettings);

				if(this.chainSettings.ContainsKey(nodeAddressInfo.ScoppedIp)) {
					this.chainSettings[nodeAddressInfo.ScoppedIp] = chainSettings;
				} else {
					this.chainSettings.Add(nodeAddressInfo.ScoppedIp, chainSettings);
				}

				this.isChainSettingConsensusDirty = true;
			}
		}

		/// <summary>
		///     This method will evaluate all data we got from all peers and establish the majority's version. If we have any bad
		///     actors, they only win if they get 51%+
		/// </summary>
		private void CalculateConsensus(BlockchainType chain) {

			var allSettings = this.chainSettings.Values.Where(d => d.ContainsKey(chain)).Select(d => d[chain]).ToList();

			ChainSettings consensusSettings = new ChainSettings();

			// ensure we get the concensus value
			consensusSettings.BlockSavingMode = this.GetPropertyConsensus(allSettings, c => c.BlockSavingMode);

			if(this.chainSettingsConsensus.ContainsKey(chain)) {
				this.chainSettingsConsensus[chain] = consensusSettings;
			} else {
				this.chainSettingsConsensus.Add(chain, consensusSettings);
			}
		}

		/// <summary>
		///     this method will take a property and evaluate all instance, and select the value that is 51%+. the majority
		///     concensus
		/// </summary>
		/// <param name="peerChainSettings"></param>
		/// <param name="selector"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		private T GetPropertyConsensus<T>(List<ChainSettings> peerChainSettings, Func<ChainSettings, T> selector)
			where T : struct {

			return ConsensusUtilities.GetConsensus(peerChainSettings, selector).result;
		}

	#endregion

	#region Connection ExistanceChecks

		/// <summary>
		///     check if any peer is in the process of connecting
		/// </summary>
		/// <param name="tcpConnection"></param>
		/// <returns></returns>
		public bool PeerConnecting(ITcpConnection tcpConnection, PeerConnection.Directions direction) {

			lock(this.locker) {
				// exclude the exact same connection, but return others with the same Uuid
				return this.ConnectingConnections.Any(c => (c.Key != tcpConnection.InternalUuid) && (c.Value.ClientUuid == tcpConnection.ReportedUuid));
			}
		}

		public bool PeerConnecting(NetworkEndPoint endpoint, PeerConnection.Directions direction) {
			if(GlobalSettings.ApplicationSettings.AllowMultipleConnectionsFromSameIp) {
				lock(this.locker) {
					return this.ConnectingConnections.Values.ToArray().Any(c => {
						NetworkEndPoint currentEndpoint = GetEndpoint(c);

						return currentEndpoint.EndPoint.Equals(endpoint.EndPoint) && currentEndpoint.IPMode.Equals(endpoint.IPMode);
					});
				}
			}

			return this.PeerConnecting(GetEndpointIp(endpoint), direction);
		}

		public bool PeerConnecting(IPAddress address, PeerConnection.Directions direction) {

			//TODO: here the port is usually not the same, and could be a different  peer. how should we treat this? is it even useful?
			lock(this.locker) {
				return this.ConnectingConnections.Values.ToArray().Any(c => {
					(IPAddress endpointAddress, var _) = GetEndpointInfo(c);

					return endpointAddress.Equals(address);
				});
			}
		}

		public bool PeerConnected(ITcpConnection tcpConnection) {
			lock(this.locker) {
				return this.Connections.Any(c => (c.Value.connection.InternalUuid != tcpConnection.InternalUuid) && (c.Key == tcpConnection.ReportedUuid));
			}

		}

		public bool PeerConnected(NetworkEndPoint endpoint) {

			if(GlobalSettings.ApplicationSettings.AllowMultipleConnectionsFromSameIp) {
				lock(this.locker) {
					return this.Connections.Values.ToArray().Any(c => {

						NetworkEndPoint currentEndpoint = GetEndpoint(c);

						return currentEndpoint.EndPoint.Equals(endpoint.EndPoint) && currentEndpoint.IPMode.Equals(endpoint.IPMode);
					});
				}
			}

			return this.PeerConnected(GetEndpointIp(endpoint));
		}

		public bool PeerConnected(IPAddress address) {

			lock(this.locker) {
				return this.Connections.Values.ToArray().Any(c => {

					(IPAddress endpointAddress, var _) = GetEndpointInfo(c);

					return endpointAddress.Equals(address);
				});
			}
		}

		public bool PeerConnectionExists(ITcpConnection tcpConnection, PeerConnection.Directions direction) {
			if(this.PeerConnected(tcpConnection)) {
				return true;
			}

			if(this.PeerConnecting(tcpConnection, direction)) {
				return true;
			}

			return false;
		}

		public bool PeerConnectionExists(NetworkEndPoint endpoint, PeerConnection.Directions direction) {
			if(this.PeerConnected(endpoint)) {
				return true;
			}

			if(this.PeerConnecting(endpoint, direction)) {
				return true;
			}

			return false;
		}

		public bool PeerConnectionExists(IPAddress address, PeerConnection.Directions direction) {
			if(this.PeerConnected(address)) {
				return true;
			}

			if(this.PeerConnecting(address, direction)) {
				return true;
			}

			return false;
		}

	#endregion

	}
}