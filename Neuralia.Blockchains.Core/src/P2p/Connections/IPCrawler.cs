using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionsProvider {
		void RequestHubIPs();
		void RequestPeerIPs(NodeAddressInfo peerIP);
		void RequestConnect(NodeAddressInfo node);
		void RequestDisconnect(NodeAddressInfo node);
		bool IsPublicAndConnectable();
		Dictionary<BlockchainType, ChainSettings> ChainSettings { get; }
	}

	public interface IIPCrawler
	{
		public class PeerStatistics
		{
			public double Metric { get; set; }
			public double InputKBps { get; set; }
			public double OutputKBps { get; set; }
			public double InputMB { get; set; }
			public double OutputMB { get; set; }
			public double Latency { get; set; }
			public double Logins { get; set; }
			public double Logouts { get; set; }
		}
		
		public enum DynamicPeerOperations
		{
			ConnectAndAddToPool = 0,
			ConnectAndWhitelist = 1,
			ConnectAndAddToLocalNodes = 2,
			DisconnectAndBlacklistTemporarily = 3,
			DisconnectAndBlackListPermanently = 4,
		}

		bool PerformDynamicPeerOperation(IConnectionsProvider provider, NodeAddressInfo node, DynamicPeerOperations operations);
		PeerStatistics QueryStats(NodeAddressInfo nai, bool onlyConnected = true);
		List<NodeAddressInfo> LocalNodes { get; }
		void QueueDynamicBlacklist(List<NodeAddressInfo> value);
		bool CanAcceptNewConnection(NodeAddressInfo node);
		void HandleHubIPs(List<NodeAddressInfo> nodes, DateTime timestamp);
		void HandlePeerIPs(NodeAddressInfo node, List<NodeAddressInfo> nodes, DateTime timestamp);
		int SyncConnections(List<PeerConnection> connected, DateTime timestamp);
		bool CombineIPs(List<NodeAddressInfo> nodes);
		void SyncFilteredNodes(List<NodeAddressInfo> nodes, IConnectionsProvider provider);
		void DisconnectAndBlacklist(NodeAddressInfo node, IConnectionsProvider provider, DateTime expiry);
		bool ForgetIPsAtNextDisconnect(NodeAddressInfo node);
		bool ForgetIPsAtNextDisconnect(List<NodeAddressInfo> nodes);
		void HandleLogin(NodeAddressInfo node, DateTime timestamp, double latency, double latencyStd);
		void HandleLogout(NodeAddressInfo node, DateTime timestamp);
		void HandleTimeout(NodeAddressInfo node, DateTime timestamp);
		void HandlePeerListRequestError(NodeAddressInfo node, DateTime timestamp);
		void HandleInputSliceSync(NodeAddressInfo node, DateTime timestamp);
		void HandleSyncComplete(NodeAddressInfo node, DateTime timestamp);
		void HandleSyncError(NodeAddressInfo node, DateTime timestamp);
		void HandleInput(NodeAddressInfo node, DateTime timestamp, uint nBytes, double latency, double latencyStd);
		void HandleOutput(NodeAddressInfo node, DateTime timestamp, uint nBytes, double latency, double latencyStd);
		double SumOfConnectedPeerMetrics(IConnectionsProvider provider);
		double SumOfConnectedPeerLatencies();
		Task Crawl(IConnectionsProvider provider, DateTime now);
	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class IPCrawler : WithLockHelpers, IIPCrawler
	{

		public const string TAG = "[" + nameof(IPCrawler) + "]";
		private readonly int averagePeerCount;

		private readonly double hubIPsRequestPeriod;
		private readonly double peerIPsRequestPeriod;
		private readonly double peerReconnectionPeriod;
		private readonly double dynamicBlacklistPeriod;
		private readonly double ipCrawlerPrintNConnectionsPeriod;
		private readonly int maxConnectionRequestPerCrawl;
		private readonly int maxPendingConnectionRequests;
		private readonly double maxLatency;
		private readonly int localNodesMode;
		private int autoLocalNodesMinConnections = 2;
		private bool p2pVerified;
		private bool isConnectable;
		private bool isSlave;
		
		private readonly List<int> LocalNodesDiscoveryPorts;
		private Queue<NodeAddressInfo> localNodesCandidates = new();
		private readonly double localNodesDiscoveryTimeout;
		
		private DateTime lastHubIpsRequested;
		private DateTime lastHubIpsReceived;
		private DateTime lastDiscoverLocalNodesAction;
		private DateTime lastIPCrawlerPrintNConnectionsPeriod;
		
		private readonly ConnectionsDict connections = new();
		private readonly ConnectionsDict nonConnectableConnections = new();
		private readonly ConnectionsDict mobileConnections = new ();
		private readonly ConnectionsDict localConnections = new ();

		
		
		private readonly object countsLock = new object();
		
		private readonly List<NodeAddressInfo> dynamicBlacklist = new ();
		private readonly List<NodeAddressInfo> filteredNodes = new ();
		private IIPCrawler iipCrawlerImplementation;


		public IPCrawler(int averagePeerCount, int maxPeerCount, int maxMobilePeerCount, int maxNonConnectablePeerCount = -1, List<NodeAddressInfo> localNodes = null
			, double hubIPsRequestPeriod = 1800.0, double peerIPsRequestPeriod = 600.0
			, double peerReconnectionPeriod = 60.0, double dynamicBlacklistPeriod = 24 * 60 * 60, int maxConnectionRequestPerCrawl = 20
			, List<int> LocalNodesDiscoveryPorts = null, double localNodesDiscoveryTimeout = 0.1, int localNodesMode = 0, double maxLatency = 2.0, double ipCrawlerPrintNConnectionsPeriod = 0.0, int maxPendingConnectionRequests = 100) 
			: base(TimeSpan.FromSeconds(30)) {
			
			this.connections.maxConnectedReference = this.connections.maxConnected = maxPeerCount;
			this.averagePeerCount = averagePeerCount;
			this.mobileConnections.maxConnectedReference = this.mobileConnections.maxConnected = maxMobilePeerCount;
			this.nonConnectableConnections.maxConnectedReference = this.nonConnectableConnections.maxConnected = maxNonConnectablePeerCount < 0 ? maxPeerCount : maxNonConnectablePeerCount;
			this.LocalNodesDiscoveryPorts = LocalNodesDiscoveryPorts??new List<int>();
			this.localNodesDiscoveryTimeout = localNodesDiscoveryTimeout;
			this.localNodesMode = localNodesMode;
			if (localNodes != null)
				foreach (var node in localNodes)
				{
					if (!this.localConnections.store.TryAdd(node, new ConnectionMetrics()))
						throw new ApplicationException($"Fatal: could not add {node} to local connections");
				}
			this.lastIPCrawlerPrintNConnectionsPeriod = this.lastDiscoverLocalNodesAction = this.lastHubIpsReceived = this.lastHubIpsRequested = DateTimeEx.MinValue;
			this.peerIPsRequestPeriod = peerIPsRequestPeriod;
			this.hubIPsRequestPeriod = hubIPsRequestPeriod;
			this.peerReconnectionPeriod = peerReconnectionPeriod;
			this.dynamicBlacklistPeriod = dynamicBlacklistPeriod;
			this.maxConnectionRequestPerCrawl = maxConnectionRequestPerCrawl;
			this.maxLatency = maxLatency;
			this.ipCrawlerPrintNConnectionsPeriod = ipCrawlerPrintNConnectionsPeriod;
			this.maxPendingConnectionRequests = maxPendingConnectionRequests;
		}

		public List<NodeAddressInfo> LocalNodes => this.localConnections.store.Keys.ToList();
		private class ConnectionsDict: WithLockHelpers
		{
			public ConnectionsDict() : base(TimeSpan.FromSeconds(30))
			{
			}

			public readonly ConcurrentDictionary<NodeAddressInfo, ConnectionMetrics> store = new ();

			public int nConnected = 0;
			public int maxConnected = 0;
			public int maxConnectedReference = 0;
			public readonly FixedConcurrentQueue<int> peerCounts = new(10);
			public bool IsSaturated => nConnected >= maxConnected;

			public void UpdateMetrics(IConnectionsProvider provider)
			{
				foreach (var connection in this.store)
				{
					LockRead(connection.Value, _ => connection.Value.UpdateMetric(provider, connection.Key));
				}
			}

			public bool IsConnected(NodeAddressInfo node)
			{
				if (store.TryGetValue(node, out var metrics))
				{
					return metrics.Status == ConnectionMetrics.ConnectionStatus.Connected ||
					       metrics.Status == ConnectionMetrics.ConnectionStatus.Pending;
				}

				return false;
			}

			public List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> ConnectedNodes(bool sortByMetric = false)
			{
				var nodes = store.Where(pair => LockRead(pair.Value, _ => pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected));
				
				if (sortByMetric)
					return nodes.OrderBy(pair => LockRead(pair.Value, _ => pair.Value.LastMetric)).ToList();
				
				return nodes.ToList();
			}

		}

		public bool PerformDynamicPeerOperation(IConnectionsProvider provider, NodeAddressInfo node, IIPCrawler.DynamicPeerOperations operations)
		{
			if (provider == null || node == null)
				throw new ArgumentNullException($"{TAG}{nameof(PerformDynamicPeerOperation)}() for node {node}");

			if (!node.IsConnectable)
			{
				NLog.IPCrawler.Warning($"{TAG}{nameof(PerformDynamicPeerOperation)}() {node} is not connectable, aborting...");
				return false;
			}
			
			bool IsAlreadyConnected(NodeAddressInfo node2)
			{
				return this.connections.IsConnected(node2) || this.localConnections.IsConnected(node2) || this.mobileConnections.IsConnected(node2) || this.nonConnectableConnections.IsConnected(node2);
			}
			
			
			switch (operations)
			{
				case IIPCrawler.DynamicPeerOperations.ConnectAndAddToPool:
					if (IsAlreadyConnected(node))
						return false;
					provider.RequestConnect(node);
					break;
				case IIPCrawler.DynamicPeerOperations.ConnectAndWhitelist:
					IPMarshall.Instance.MoveToWhiteList(node.AdjustedAddress);
					if (IsAlreadyConnected(node))
						return false;
					provider.RequestConnect(node);
					break;
				case IIPCrawler.DynamicPeerOperations.ConnectAndAddToLocalNodes:
					this.localConnections.store.TryAdd(node, new ConnectionMetrics());
					if (IsAlreadyConnected(node))
						return false;
					provider.RequestConnect(node);
					break;
				case IIPCrawler.DynamicPeerOperations.DisconnectAndBlacklistTemporarily:
					this.DisconnectAndBlacklist(node, provider, DateTimeEx.CurrentTime.AddMinutes(10));
					break;
				case IIPCrawler.DynamicPeerOperations.DisconnectAndBlackListPermanently:
					this.DisconnectAndBlacklist(node, provider, DateTimeEx.MaxValue);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(operations), operations, null);
			}

			return true;
		}

		public IIPCrawler.PeerStatistics QueryStats(NodeAddressInfo nai, bool onlyConnected = true)
		{
			IIPCrawler.PeerStatistics TryGetStats(ConnectionsDict connections,
				NodeAddressInfo nai2)
			{
				if (connections.store.TryGetValue(nai2, out var metric))
				{
					return LockRead(metric, _ =>
					{
						if (!onlyConnected || metric.Status == ConnectionMetrics.ConnectionStatus.Connected)
						{
							return new IIPCrawler.PeerStatistics
							{
								InputKBps = metric.Stats[ConnectionMetrics.BYTES_IN] / 1e3,
								OutputKBps = metric.Stats[ConnectionMetrics.BYTES_OUT] / 1e3,
								InputMB = metric.Stats[ConnectionMetrics.TOTAL_BYTES_IN] / 1e6,
								OutputMB = metric.Stats[ConnectionMetrics.TOTAL_BYTES_OUT] / 1e6,
								Logins = metric.Stats[ConnectionEvent.Type.Login.ToString()],
								Logouts = metric.Stats[ConnectionEvent.Type.Logout.ToString()],
								Metric = metric.LastMetric,
								Latency = metric.Latency
							};
						}
						return null;
					});
				}

				return null;
			}

			return TryGetStats(connections, nai) ?? TryGetStats(mobileConnections, nai) ?? TryGetStats(nonConnectableConnections, nai) ?? TryGetStats(localConnections, nai);
		}
		public void QueueDynamicBlacklist(List<NodeAddressInfo> value)
		{
			if (value == null)
				return;

			this.dynamicBlacklist.AddRange(value);
		}

		public bool CanAcceptNewConnection(NodeAddressInfo node)
		{
			if (IPMarshall.Instance.IsWhiteList(node.Address, out var acceptanceType))
			{
				switch (acceptanceType)
				{
					case AppSettingsBase.WhitelistedNode.AcceptanceTypes.Always:
						NLog.IPCrawler.Verbose($"CanAcceptNewConnection() asked for a whitelisted node ({node}), accepting...");
						return true;
					case AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots:
						NLog.IPCrawler.Information($"{TAG} whitelisted node's acceptanceType is '{AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots}', no special treatment.");
						break;
					default:
						NLog.IPCrawler.Error($"{TAG} Undefined whitelisted node's acceptanceType: '{acceptanceType}', no special treatment.");
						break;
				}
			}

			if (this.localConnections.store.TryGetValue(node, out var metrics))
			{
				if(metrics.Status == ConnectionMetrics.ConnectionStatus.Connected
					|| metrics.Status == ConnectionMetrics.ConnectionStatus.Pending)
					NLog.IPCrawler.Warning($"CanAcceptNewConnection() asked for a local node ({node}) with status {metrics.Status}, accepting anyway");
				
				NLog.IPCrawler.Verbose($"CanAcceptNewConnection() asked for a local node ({node}) with status {metrics.Status}, accepting...");
				
				return true;
			}

			return LockRead(this.countsLock, _ =>
			{
				bool mobileOk = !this.mobileConnections.IsSaturated;
				bool fullNodesOk = !this.connections.IsSaturated;
				bool nonConnectableOk = !this.nonConnectableConnections.IsSaturated;


				if (node.PeerInfo.PeerType == Enums.PeerTypes.Mobile)
				{
					if (!mobileOk)
						NLog.IPCrawler.Verbose(
							$"CanAcceptNewConnection: mobile connections saturated ({this.mobileConnections.maxConnected}), refusing {node}.");
					return mobileOk;
				}

				if (node.PeerInfo.PeerType == Enums.PeerTypes.FullNode && !node.IsConnectable)
				{
					if (!nonConnectableOk)
						NLog.IPCrawler.Verbose(
							$"CanAcceptNewConnection: non-connectable connections saturated ({this.nonConnectableConnections.maxConnected}), refusing {node}.");
					return nonConnectableOk;
				}

				if (node.PeerInfo.PeerType == Enums.PeerTypes.Unknown)
				{
					if (!(mobileOk || fullNodesOk || nonConnectableOk))
						NLog.IPCrawler.Verbose(
							$"CanAcceptNewConnection: all connection types saturated (c:{this.connections.maxConnected}, n-c: {this.nonConnectableConnections.maxConnected}, m: {this.mobileConnections.maxConnected}), refusing {node}.");

					return mobileOk || fullNodesOk || nonConnectableOk;
				}

				if (!fullNodesOk)
					NLog.IPCrawler.Verbose(
						$"CanAcceptNewConnection: connectable connections saturated ({this.connections.maxConnected}), refusing {node}.");

				return fullNodesOk;
			});
		}

		private ConnectionMetrics GocConnectionMetrics(NodeAddressInfo node)
		{
			if (this.localConnections.store.TryGetValue(node, out var connectionMetric))
				return connectionMetric;
			
			var store = node.PeerInfo.PeerType == Enums.PeerTypes.Mobile ? this.mobileConnections.store : (node.IsConnectable ? this.connections.store : this.nonConnectableConnections.store);

			if(!store.TryGetValue(node, out var connectionMetrics)) {
				connectionMetrics = store[node] = new ConnectionMetrics();
			}

			return connectionMetrics;
		}
		
		public void HandleHubIPs(List<NodeAddressInfo> nodes, DateTime timestamp) {
			this.lastHubIpsReceived = timestamp;
			this.CombineIPs(nodes);
		}

		public void HandlePeerIPs(NodeAddressInfo node, List<NodeAddressInfo> nodes, DateTime timestamp) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			LockRead(metric, _ =>
			{
				metric.LastPeerIpsReceived = timestamp;
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Input, timestamp,
					Convert.ToUInt32(nodes.Count *
					                 4U))); //FIXME nBytes is a bad approximation, not too important anyway
			});

			this.CombineIPs(nodes);
		}

		
		public int SyncConnections(List<PeerConnection> connected, DateTime timestamp)
		{
			
			//FIXME: this method should not be necessary and the proper hooks should be added to e.g. ConnectionStore
			// to make certain we are always notified of new connections/disconnections
			// Please note that this is the only method that uses PeerConnection type, and this method should be removed

			List<PeerConnection> connectedLocal =
				connected.Where(c => this.localConnections.store.ContainsKey(c.NodeAddressInfo)).ToList();
			
			List<PeerConnection> connectedMobile =
				connected.Where(c => !this.localConnections.store.ContainsKey(c.NodeAddressInfo) && c.NodeAddressInfo.PeerInfo.PeerType == Enums.PeerTypes.Mobile).ToList();
			
			List<PeerConnection> connectedOther =
				connected.Where(c => !this.localConnections.store.ContainsKey(c.NodeAddressInfo) && c.NodeAddressInfo.PeerInfo.PeerType != Enums.PeerTypes.Mobile && c.NodeAddressInfo.IsConnectable).ToList();	
			
			List<PeerConnection> connectedNonConnectable =
				connected.Where(c => !this.localConnections.store.ContainsKey(c.NodeAddressInfo) && c.NodeAddressInfo.PeerInfo.PeerType != Enums.PeerTypes.Mobile && !c.NodeAddressInfo.IsConnectable).ToList();
				
			int correctionsMade = 0;

			void MissingLogins(ConnectionsDict dict, IEnumerable<PeerConnection> peerConnections)
			{
				foreach (var peerConnection in peerConnections)
				{
					if (dict.store.TryGetValue(peerConnection.NodeAddressInfo, out var metrics))
					{
						if (LockRead(metrics, l =>
						{
							switch (metrics.Status)
							{
								case ConnectionMetrics.ConnectionStatus.Connected:
									if (Math.Abs(peerConnection.connection.ConnectionLatency - metrics.Latency) > Double.Epsilon ||
									    Math.Abs(peerConnection.connection.ConnectionLatencyStd - metrics.LatencyStd) > Double.Epsilon)
									{
										l.UpgradeToWriterLock(this.mutexTimeout);
										metrics.Latency = peerConnection.connection.ConnectionLatency;
										metrics.LatencyStd = peerConnection.connection.ConnectionLatencyStd;
									}
									return true;
								case ConnectionMetrics.ConnectionStatus.Pending:
								case ConnectionMetrics.ConnectionStatus.Filtered:
								case ConnectionMetrics.ConnectionStatus.Lost:
								case ConnectionMetrics.ConnectionStatus.NotConnected:
									NLog.IPCrawler.Verbose(
										$"{TAG} SyncConnections: found a wrong status for {peerConnection.NodeAddressInfo.PeerInfo.PeerType} node {peerConnection.NodeAddressInfo}, status was {metrics.Status} ");

									break;
								default:
									NLog.IPCrawler.Error(
										$"{TAG} SyncConnections: bad status for {peerConnection.NodeAddressInfo.PeerInfo.PeerType} node {peerConnection.NodeAddressInfo}, status was {metrics.Status} ");
									break;
							}

							return false;
						}))
						{
							continue;
						}
					}
					else
					{
						NLog.IPCrawler.Verbose(
							$"{TAG} SyncConnections: missing {peerConnection.NodeAddressInfo.PeerInfo.PeerType} node detected {peerConnection.NodeAddressInfo}");
					}

					HandleLogin(peerConnection.NodeAddressInfo, peerConnection.ConnectionTime, peerConnection.connection.ConnectionLatency, peerConnection.connection.ConnectionLatencyStd);
					correctionsMade++;
				}
			}

			MissingLogins(this.localConnections, connectedLocal);
			MissingLogins(this.connections, connectedOther);
			MissingLogins(this.mobileConnections, connectedMobile);
			MissingLogins(this.nonConnectableConnections, connectedNonConnectable);
			


			void MissingLogouts(ConnectionsDict store, IEnumerable<PeerConnection> connections)
			{
				
				var toDisconnect = store.store.Where(pair => LockRead(pair.Value, _ =>
					{
						return pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected &&
						       !connections.Any(c => c.NodeAddressInfo.Equals(pair.Key));
					})).ToList();

				foreach (var pair in toDisconnect)
				{
					LockRead(pair.Value,
						_ => NLog.IPCrawler.Verbose(
							$"{TAG} SyncConnections: found a missing HandleLogout for {pair.Key.PeerInfo.PeerType} node {pair.Key}, status was {pair.Value.Status}... "));
					
					HandleLogout(pair.Key, timestamp);
					correctionsMade++;
				}
			}
			
			MissingLogouts(this.localConnections, connectedLocal);
			MissingLogouts(this.connections, connectedOther);
			MissingLogouts(this.mobileConnections, connectedMobile);
			MissingLogouts(this.nonConnectableConnections, connectedNonConnectable);

			return correctionsMade;

		}

		public bool ForgetIPsAtNextDisconnect(NodeAddressInfo node) {
			return ForgetIPsAtNextDisconnect(new []{node}.ToList());
		}

		public bool ForgetIPsAtNextDisconnect(List<NodeAddressInfo> nodes)
		{
			bool result = false;
			foreach (var node in nodes)
			{
				if (this.connections.store.TryGetValue(node, out var metrics))
				{
					LockRead(metrics, l =>
					{
						if (metrics.Status == ConnectionMetrics.ConnectionStatus.Connected ||
						    metrics.Status == ConnectionMetrics.ConnectionStatus.Pending)
						{
							l.UpgradeToWriterLock(this.mutexTimeout);
							
							metrics.ForgetOnNextDisconnection = true;
							NLog.IPCrawler.Verbose(
								$"{TAG} connected node {node} is set to be forgotten at next disconnection event, it is YOUR job to request disconnection.");
							result = true;
						}
						else
						{
							NLog.IPCrawler.Verbose(
								$"{TAG} connected node {node} is set to be forgotten and it is already disconnected, removing immediately.");
							if (this.connections.store.TryRemove(node, out var _))
								result = true;
							else
								NLog.IPCrawler.Warning(
									$"{nameof(ForgetIPsAtNextDisconnect)}  with {node} is ask to be forgotten, but was not found in the connectable node's connection pool.");
						}
					});
				}
			}
			return result;
		}

		public bool CombineIPs(List<NodeAddressInfo> nodes)
		{
			
			List<NodeAddressInfo> newIPs = nodes.Where(node =>
			{
				if (this.localConnections.store.ContainsKey(node))
				{
					// One might think that we (or some other peer) probably sent it in a PeerList request, but in fact,
					// every ClientHandshakeWorkflow ends by calling AddValidConnection(node), which, in turn, will call
					// AddAvailablePeerNode(node) which will end up calling CombineIPs 
					return false;
				}

				bool canConnect = (NodeAddressInfo.IsAddressIpV6(node.NetworkEndPoint) && TcpConnection.IPv6Supported &&
				              GlobalSettings.ApplicationSettings.IPProtocolClient.HasFlag(IPMode.IPv6)) ||
				             (NodeAddressInfo.IsAddressIpV4Analog(node.NetworkEndPoint) &&
				              GlobalSettings.ApplicationSettings.IPProtocolClient.HasFlag(IPMode.IPv4));
				
				return node.PeerInfo.PeerType != Enums.PeerTypes.Mobile
				       && node.IsConnectable
				       && canConnect
				       && !this.connections.store.ContainsKey(node);
			}).ToList();
			
			foreach (NodeAddressInfo newIP in newIPs)
			{
				NLog.IPCrawler.Verbose($"{TAG} new node detected: {newIP}, registering...");
				ConnectionMetrics metric = new ConnectionMetrics();
				if(!this.connections.store.TryAdd(newIP, metric))
					throw new ApplicationException($"{TAG} could not add metric for node {newIP}, this is fatal");
			}

			return newIPs.Any();
		}

		public void SyncFilteredNodes(List<NodeAddressInfo> nodes, IConnectionsProvider provider)
		{
			List<NodeAddressInfo> unfilter = this.filteredNodes.Where(node => !nodes.Contains(node)).ToList();
			
			foreach(NodeAddressInfo node in unfilter) {
				if (this.connections.store.TryGetValue(node, out var connectionMetrics))
				{
					LockRead(connectionMetrics, l =>
					{
						if (connectionMetrics.Status == ConnectionMetrics.ConnectionStatus.Filtered)
						{
							l.UpgradeToWriterLock(this.mutexTimeout);
							connectionMetrics.Status = ConnectionMetrics.ConnectionStatus.NotConnected;
							connectionMetrics.NextConnectionAttempt = DateTimeEx.MinValue;

						}
					});
				} //else nothing to do
			}

			foreach (var node in nodes)
			{
				if (this.connections.store.TryGetValue(node, out var metric))
				{
					LockRead(metric, l =>
					{
						switch (metric.Status)
						{
							case ConnectionMetrics.ConnectionStatus.Filtered:
								break;
							case ConnectionMetrics.ConnectionStatus.Connected:
							case ConnectionMetrics.ConnectionStatus.Pending:
								provider.RequestDisconnect(node);
								l.UpgradeToWriterLock(this.mutexTimeout);
								metric.FilterOnNextEvent = true;
								break;
							case ConnectionMetrics.ConnectionStatus.DynamicBlacklist:
								NLog.IPCrawler.Error(
									$"{TAG} Filtering requested on a dynamic blacklist node, this is illegal, updating status to Filtered.");
								l.UpgradeToWriterLock(this.mutexTimeout);
								metric.Status = ConnectionMetrics.ConnectionStatus.Filtered;
								break;
							case ConnectionMetrics.ConnectionStatus.NotConnected:
							case ConnectionMetrics.ConnectionStatus.Lost:
								l.UpgradeToWriterLock(this.mutexTimeout);
								metric.Status = ConnectionMetrics.ConnectionStatus.Filtered;
								break;
							default:
								throw new NotImplementedException($"{TAG} unimplemented case");

						}
					});
				}
				else
				{
					metric = new ConnectionMetrics
					{
						Status = ConnectionMetrics.ConnectionStatus.Filtered,
					};

					if(!this.connections.store.TryAdd(node, metric))
						throw new ApplicationException($"{TAG} could not add metric for node {node}, this is fatal");
				}
				
				metric.NextConnectionAttempt = DateTimeEx.MaxValue;
				
			}

			this.filteredNodes.Clear();
			this.filteredNodes.AddRange(nodes);

		}
		
		public Queue<NodeAddressInfo> LocalNodesCandidates(List<int> ports)
	    {
        
		    Queue<NodeAddressInfo> candidates = new();
			    

		    NLog.IPCrawler.Information($"{TAG} Launching local nodes discovery, this might take a while");
		    
		    
	        foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
	        {
	            //if the current interface doesn't have an IP, skip it
	            if (! (netInterface.GetIPProperties().GatewayAddresses.Count > 0))
	                continue;

	            foreach (var gateway in netInterface.GetIPProperties()?.GatewayAddresses)
	            {
		            var gatewayIp = netInterface.GetIPProperties()?.GatewayAddresses.FirstOrDefault().Address;
		            //get current IP Address(es)
		            UnicastIPAddressInformation uniIpInfo =
			            netInterface.GetIPProperties()?.UnicastAddresses.FirstOrDefault();
		            
	            
		            var localIp = uniIpInfo.Address;

		            NLog.IPCrawler.Information($"{TAG} {nameof(LocalNodesCandidates)} Gateway is {gatewayIp}, our local ip is {localIp}");
					
	                //get the subnet mask and the IP address as bytes
	                byte[] subnetMaskBytes = uniIpInfo.IPv4Mask.GetAddressBytes();
	                byte[] localIpBytes = localIp.GetAddressBytes();
	                
	                // we reverse the byte-array if we are dealing with littl endian.
	                if (BitConverter.IsLittleEndian)
	                {
	                    Array.Reverse(subnetMaskBytes);
	                    Array.Reverse(localIpBytes);
	                }
	        
	                //we negate the subnet to determine the maximum number of host possible in this subnet
	                uint validHostsEndingMax = ~BitConverter.ToUInt32(subnetMaskBytes, 0);
	                //we convert the start of the ip addres as uint (the part that is fixed wrt the subnet mask - from here we calculate each new address by incrementing with 1 and converting to byte[] afterwards 
	                uint validHostsStart = BitConverter.ToUInt32(localIpBytes, 0) & BitConverter.ToUInt32(subnetMaskBytes, 0);
	        
	                //we increment the startIp to the number of maximum valid hosts in this subnet and for each we check the intended port (refactoring needed)
	                for (uint i = 1; i < validHostsEndingMax; i++)
	                {
		                uint host = validHostsStart + i;
		                byte[] hostBytes = BitConverter.GetBytes(host);
		                if (BitConverter.IsLittleEndian)
			                Array.Reverse(hostBytes);

		                var ip = new IPAddress(hostBytes);

		                if (ip.Equals(localIp) || ip.Equals(gatewayIp))
			                continue;
		                
		                ports.ForEach(port =>
		                {
			                var node = new NodeAddressInfo(ip, port, NodeInfo.Full);
			                if(!this.localConnections.store.ContainsKey(node))
								candidates.Enqueue(node);
		                });
	                }
	            }
	        }

	        return candidates;
	    }

		private bool TryConnectLocalNode(NodeAddressInfo node, double waitTimeForResponse)
		{
			try
			{
				//try to connect
				Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
					ProtocolType.Tcp);

				IAsyncResult result = socket.BeginConnect(node.AdjustedAddress, node.Port??0, null, null);

				bool success = result.AsyncWaitHandle.WaitOne(Convert.ToInt32(waitTimeForResponse * 1000), true);

				if (socket.Connected) // if succesful => something is listening on this port
				{
					socket.EndConnect(result);
					
					NLog.IPCrawler.Verbose(
						$"{TAG} {nameof(TryConnectLocalNode)} found a local peer at: {node} ");
					socket.Close();
					return true;
				}
				socket.Close();
			}
			catch (SocketException ex)
			{
				NLog.IPCrawler.Verbose(ex,
					$"{TAG} caught a socket exception during {nameof(TryConnectLocalNode)} with {node}");
			}

			return false;
		}

		public void DisconnectAndBlacklist(NodeAddressInfo node, IConnectionsProvider provider, DateTime expiry)
		{
			if (this.connections.store.TryGetValue(node, out var metric))
			{
				LockRead(metric, l =>
				{
					if (IPMarshall.Instance.IsWhiteList(node.Address, out var acceptanceType))
					{
						NLog.IPCrawler.Error($"{TAG} Trying to blacklist whitelisted node {node} with acceptance type {acceptanceType}, aborting...");
						return;
					}

					IPMarshall.Instance.Quarantine(node.Address, IPMarshall.QuarantineReason.DynamicBlacklist, expiry);

					if (metric.Status == ConnectionMetrics.ConnectionStatus.Connected ||
					    metric.Status == ConnectionMetrics.ConnectionStatus.Pending)
					{
						provider.RequestDisconnect(node);
					}

					l.UpgradeToWriterLock(this.mutexTimeout);
					metric.NextConnectionAttempt = expiry;
					metric.DynamicBlacklistOnNextEvent = true;
				});
			}
		}

		private void UpdateConnectionStatus(NodeAddressInfo node, ConnectionMetrics metric, ConnectionMetrics.ConnectionStatus newStatus, ReaderWriterLock metricLock)
		{
			if (metric.Status == newStatus)
				return;

			bool wasDisconnected = false;
			LockRead(this.countsLock, l =>
			{

				void UpdateCounter(int delta)
				{
					l.UpgradeToWriterLock(this.mutexTimeout);
					
					if (this.localConnections.store.ContainsKey(node))
						this.localConnections.nConnected += delta;
					else if (node.PeerInfo.PeerType == Enums.PeerTypes.Mobile)
						this.mobileConnections.nConnected += delta;
					else if (node.IsConnectable)
						this.connections.nConnected += delta;
					else
						this.nonConnectableConnections.nConnected += delta;
				}

				if (metric.Status != ConnectionMetrics.ConnectionStatus.Connected &&
				    newStatus == ConnectionMetrics.ConnectionStatus.Connected)
					UpdateCounter(+1); // Any -> Connected
				else if (metric.Status == ConnectionMetrics.ConnectionStatus.Connected &&
				         newStatus != ConnectionMetrics.ConnectionStatus.Connected)
				{
					UpdateCounter(-1); // Connected -> Any
					wasDisconnected = true;
				}
			});

			metricLock.UpgradeToWriterLock(this.mutexTimeout);
			
			metric.Status = newStatus;

			if (metric.DynamicBlacklistOnNextEvent)
			{
				metric.Status = ConnectionMetrics.ConnectionStatus.DynamicBlacklist;
				metric.DynamicBlacklistOnNextEvent = false;
			}

			if (metric.FilterOnNextEvent)
			{
				metric.Status = ConnectionMetrics.ConnectionStatus.Filtered;
				metric.FilterOnNextEvent = false;
			}
			
			if (wasDisconnected && metric.ForgetOnNextDisconnection)
			{
				if(this.connections.store.TryRemove(node, out metric))
					NLog.IPCrawler.Verbose($"{nameof(HandleLogout)}  with {node} is now forgotten.");
				else
					NLog.IPCrawler.Warning($"{nameof(HandleLogout)}  with {node} was set to be forgotten, but was not found in the connectable node's connection pool.");

				metric.ForgetOnNextDisconnection = false; //it will be disposed anyway

			}
		}
		public void HandleLogin(NodeAddressInfo node, DateTime timestamp, double latency, double latencyStd)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			NLog.IPCrawler.Verbose($"{nameof(HandleLogin)}  with {node}, status was {metric.Status}.");
			LockWrite(metric, l =>
			{
				if (metric.Status == ConnectionMetrics.ConnectionStatus.Filtered)
				{
					NLog.IPCrawler.Error(
						$"{TAG} {nameof(this.HandleLogin)} this node is supposed to be filtered, a login is not supposed to happen");
					metric.FilterOnNextEvent = true;
				}

				metric.Latency = latency;
				metric.LatencyStd = latencyStd;

				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Login, timestamp, 0));

				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected, l);
			});
		}

		public void HandleLogout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			NLog.IPCrawler.Verbose($"{nameof(HandleLogout)}  with {node}, status was {metric.Status}.");
			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Logout, timestamp, 0));

				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.NotConnected, l);
			});
		}

		public void HandleTimeout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			NLog.IPCrawler.Verbose($"{nameof(HandleTimeout)} with {node}, status was {metric.Status}");
			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Timeout, timestamp, 0));

				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Lost, l);
			});

		}
		
		public void HandlePeerListRequestError(NodeAddressInfo node, DateTime timestamp)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.PeerListRequestError, timestamp, 0));
			});

		}

		public void HandleInputSliceSync(NodeAddressInfo node, DateTime timestamp)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			LockWrite(metric, _ =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.InputSliceSync, timestamp, 0));
			});
		}

		public void HandleSyncError(NodeAddressInfo node, DateTime timestamp)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.SyncError, timestamp, 0));
			});

		}
		
		public void HandleSyncComplete(NodeAddressInfo node, DateTime timestamp)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			NLog.IPCrawler.Verbose($"{TAG} HandleSyncComplete received, {node} is being rewarded generously!");
			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.SyncComplete, timestamp, 0));
			});

		}
		
		public void HandleInput(NodeAddressInfo node, DateTime timestamp, uint nBytes, double latency, double latencyStd) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Input, timestamp, nBytes));
				metric.Latency = latency;
				metric.LatencyStd = latencyStd;
				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected, l);
			});

		}

		public void HandleOutput(NodeAddressInfo node, DateTime timestamp, uint nBytes, double latency, double latencyStd) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);
			LockWrite(metric, l =>
			{
				metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Output, timestamp, nBytes));
				metric.Latency = latency;
				metric.LatencyStd = latencyStd;
				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected, l);
			});

		}

		public double SumOfConnectedPeerMetrics(IConnectionsProvider provider)
		{
			var connectedNodes = this.connections.ConnectedNodes();

			double sum = 0.0;

			foreach (var node in connectedNodes)
				LockRead(node.Value, _ => sum += node.Value.Metric(provider, node.Key));

			return sum;
		}
		public double SumOfConnectedPeerLatencies()
		{
			var connectedNodes = this.connections.ConnectedNodes();

			double sum = 0.0;

			foreach (var node in connectedNodes)
				LockRead(node.Value, _ => sum += node.Value.Latency);

			return sum;
		}

		
		public Task Crawl(IConnectionsProvider provider, DateTime now) {

			var connectedConnectableNodes = connections.ConnectedNodes(true);
			var connectedNonConnectableNodes = nonConnectableConnections.ConnectedNodes(true);
			var connectedMobileNodes = mobileConnections.ConnectedNodes(true);


			var connectedNodes = (connectedConnectableNodes.Concat(connectedNonConnectableNodes).Concat(connectedMobileNodes)).ToList();
			
			int nPending = connections.store.Count(pair => LockRead(pair.Value, _ => pair.Value.Status == ConnectionMetrics.ConnectionStatus.Pending));
			int nConnectable = connectedConnectableNodes.Count;
			int nNonConnectable = connectedNonConnectableNodes.Count;
			connections.UpdateMetrics(provider);
			mobileConnections.UpdateMetrics(provider);
			nonConnectableConnections.UpdateMetrics(provider);
			
			List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectionCandidates = connections.store.Where(pair =>
				LockRead(pair.Value, _ => (pair.Value.Status == ConnectionMetrics.ConnectionStatus.NotConnected) ||
				                          (pair.Value.Status == ConnectionMetrics.ConnectionStatus.Lost) &&
				                          (pair.Value.NextConnectionAttempt < now) &&
				                          !IPMarshall.Instance.IsQuarantined(pair.Key.Address))
			).OrderByDescending(pair => LockRead(pair.Value, _ => pair.Value.LastMetric) //we just updated all metrics a few lines above
			).ToList();
			
			int nConnectableInTimeout = connections.store.Count - connections.nConnected - connectionCandidates.Count;
			
			if (this.dynamicBlacklist.Count > 0)
			{
				foreach (var node in dynamicBlacklist)
					DisconnectAndBlacklist(node, provider, DateTimeEx.CurrentTime.AddSeconds(dynamicBlacklistPeriod));
				dynamicBlacklist.Clear();
			}
			
			TryToDiscoverLocalNodes(provider, now, connectedNodes);

			foreach (var (node, metric) in localConnections.store.Where(n => 
				n.Value.Status != ConnectionMetrics.ConnectionStatus.Connected 
			    && n.Value.Status != ConnectionMetrics.ConnectionStatus.Pending ))
			{
				NLog.IPCrawler.Information($"[LocalNodes] request connection to {node}, current status is {metric.Status}.");
				// actively try connecting local nodes
				provider.RequestConnect(node);
			}
			
			//remove extra connections
			void RemoveIfNotWhitelisted(NodeAddressInfo node)
			{
				if (!IPMarshall.Instance.IsWhiteList(node.Address, out var acceptanceType)
				    || acceptanceType == AppSettingsBase.WhitelistedNode.AcceptanceTypes.Always)
				{
					provider.RequestDisconnect(node);
				}
			}
			
			//connectable
			for (int i = 0; i < Math.Min(nConnectable - this.connections.maxConnected, nConnectable); i++)
				RemoveIfNotWhitelisted(connectedConnectableNodes[i].Key);
			
			//non-connectable
			for(int i = 0; i < Math.Min(nNonConnectable - this.nonConnectableConnections.maxConnected, nNonConnectable); i++)
				RemoveIfNotWhitelisted(connectedNonConnectableNodes[i].Key);
			
			//mobile
			for(int i = 0; i < Math.Min(connectedMobileNodes.Count - this.mobileConnections.maxConnected, connectedMobileNodes.Count); i++)
				RemoveIfNotWhitelisted(connectedMobileNodes[i].Key);
			
			// request peers, print stats
			foreach((NodeAddressInfo node, ConnectionMetrics metric) in connectedNodes)
			{
				LockRead(metric, l =>
				{
					if (metric.LatencyStd < this.maxLatency && metric.Latency > this.maxLatency)
					{
						IPMarshall.Instance.Quarantine(node.Address, IPMarshall.QuarantineReason.LatencyTooHigh,
							DateTimeEx.CurrentTime.AddDays(1)
							, $"{TAG} Latency is {1000 * metric.Latency:0.000}ms, which is above tolerance ({1000 * this.maxLatency}ms).",
							5, TimeSpan.FromMinutes(peerIPsRequestPeriod * 5));
						provider.RequestDisconnect(node);
					}

					NLog.IPCrawler.Verbose($"{TAG} connected peer {node} has metric"
					                       + $" {metric.Metric(provider, node):E1},"
					                       + $" {metric.Stats[ConnectionEvent.Type.InputSliceSync.ToString()]:0.0} slices avg"
					                       + $" ({metric.Stats[ConnectionEvent.Type.SyncError.ToString()]:0.0} issues avg)"
					                       + $" and latency {1000 * metric.Latency:0.000} ms +/- {1000*metric.LatencyStd:0.000}.");

					//metric.PrintStats(node); //ultra-verbose

					if (now > metric.LastPeerIpsRequested.AddSeconds(peerIPsRequestPeriod))
					{
						provider.RequestPeerIPs(node);
						l.UpgradeToWriterLock(this.mutexTimeout);
						metric.LastPeerIpsRequested = now;
					}
				});
			}

			double avg = 0;
			LockRead(this.countsLock, _ =>
			{
				connections.peerCounts.Enqueue(nConnectable);
				avg = Convert.ToDouble(connections.peerCounts.Sum(x => x)) /
				      connections.peerCounts.Count; // average last n connections.nConnected counts

				nonConnectableConnections.peerCounts.Enqueue(nNonConnectable);
				mobileConnections.peerCounts.Enqueue(connectedMobileNodes.Count);
			});

			if(now > lastHubIpsRequested.AddSeconds(hubIPsRequestPeriod) && 
			   (avg < this.averagePeerCount || connectionCandidates.Count < this.averagePeerCount)) {
				provider.RequestHubIPs();
				lastHubIpsRequested = now;
			}
			
			for (int i = 0; i < Math.Min(this.averagePeerCount - Convert.ToInt32(avg), Math.Min(this.maxConnectionRequestPerCrawl, connectionCandidates.Count)); i++)
			{
				if (nPending >= this.maxPendingConnectionRequests)
				{
					NLog.IPCrawler.Information($"too many pending connection requests {nPending} > {this.maxPendingConnectionRequests}");
					break;
				}
				var candidate = connectionCandidates[i];
				var node = candidate.Key;
				var metric = candidate.Value;
				LockWrite(metric, l =>
				{
					provider.RequestConnect(node);
					this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Pending, l);
					nPending++;
					metric.NextConnectionAttempt = now.AddSeconds(peerReconnectionPeriod);
				});
				
			}
			string message0 = LockRead(this.countsLock, _ => $"{TAG} {nConnectable} connectable peer(s) ({avg:0.00} in avg)"
			                                                 + $" with average latency {SumOfConnectedPeerLatencies() / nConnectable:0.000} s,"
			                                                 + $" we have {nPending} pending connection requests, {connectionCandidates.Count} potential other connectable peers ready"
			                                                 + $" to connect to and {nConnectableInTimeout} peers in timeout.");
		
			string message1 = LockRead(this.countsLock, _ => $"{TAG} we have {connectedMobileNodes.Count} mobile and {nNonConnectable} non-connectable peers connected");

			NLog.IPCrawler.Information(message0);
			NLog.IPCrawler.Information(message1);
			
			if (ipCrawlerPrintNConnectionsPeriod > 0 && now > lastIPCrawlerPrintNConnectionsPeriod.AddSeconds(ipCrawlerPrintNConnectionsPeriod))
			{
				
				NLog.Default.Information(message0);
				NLog.Default.Information(message1);
				lastIPCrawlerPrintNConnectionsPeriod = now;
			}
			
			return Task.CompletedTask;
		}

		private void TryToDiscoverLocalNodes(IConnectionsProvider provider, DateTime now, List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectedNodes)
		{
			if (this.LocalNodesDiscoveryPorts.Any())
			{
				if (!this.p2pVerified)
				{
					switch (this.localNodesMode)
					{
						case 0: //Auto
							if (connectedNodes.Count >= autoLocalNodesMinConnections)
							{
								try
								{
									this.isConnectable = provider.IsPublicAndConnectable();
									this.p2pVerified = true;
								}
								catch (Exception e)
								{
									if (e.Message == nameof(provider.IsPublicAndConnectable))
									{
										autoLocalNodesMinConnections = Math.Min(autoLocalNodesMinConnections + 1,
											this.connections.maxConnected +
											this.nonConnectableConnections.maxConnected +
											this.mobileConnections.maxConnected);
										NLog.IPCrawler.Verbose(e,
											$"{TAG} unconfirmed connectability status, will now require ({autoLocalNodesMinConnections}) connections...");
									}
								}
							}

							break;
						case 1: //Slave				
							this.p2pVerified = true;
							this.isConnectable = false;
							break;
						case 2: //Master
							this.p2pVerified = true;
							this.isConnectable = true;
							break;
						default:
							NLog.IPCrawler.Warning($"{TAG} invalid value LocalNodesMode, defaulting to 'Auto'");
							break;
					}
				}

				if (now > lastDiscoverLocalNodesAction.AddDays(1))
				{
					this.localNodesCandidates = LocalNodesCandidates(this.LocalNodesDiscoveryPorts);
					this.lastDiscoverLocalNodesAction = DateTimeEx.CurrentTime;
				}

				for (var i = 0; i < Math.Min(this.localNodesCandidates.Count, 5); i++)
				{
					var candidate = this.localNodesCandidates.Dequeue();
					if (TryConnectLocalNode(candidate, this.localNodesDiscoveryTimeout))
					{
						if (!this.localConnections.store.TryAdd(candidate, new ConnectionMetrics()))
							throw new ApplicationException($"Fatal: could not add {candidate} to local connections");
					}
				}

				if (this.p2pVerified && !this.isConnectable && !this.isSlave && localConnections.nConnected > 0)
				{
					NLog.IPCrawler.Information(
						$"{TAG} ***Important*** We are disconnecting from the outside world, we will now only sync from the local network nodes. You can deactivate that feature by setting 'LocalNodesDiscoveryPorts' : [], in config.json");

					this.connections.maxConnected = 0;
					this.mobileConnections.maxConnected = 0;
					this.nonConnectableConnections.maxConnected = 0;
					// they will all be risconnected later 
					this.isSlave = true;
				}

				if (this.p2pVerified && !this.isConnectable && this.isSlave && localConnections.nConnected == 0)
				{
					NLog.IPCrawler.Information($"{TAG} ***Important*** no more local connections, deactivating slave mode");
					this.connections.maxConnected = this.connections.maxConnectedReference;
					this.mobileConnections.maxConnected = this.mobileConnections.maxConnectedReference;
					this.nonConnectableConnections.maxConnected = this.nonConnectableConnections.maxConnectedReference;
					this.p2pVerified = false;
					this.isSlave = false;
				}
			}
		}
	}



	public struct ConnectionEvent {
		public enum Type {
			Input,
			Output,
			Timeout,
			SyncError,
			PeerListRequestError,
			InputSliceSync,
			SyncComplete,
			Login,
			Logout
		}

		public uint nBytes;
		public DateTime timestamp;
		public Type type;

		public ConnectionEvent(Type type, DateTime timestamp, uint nBytes) {
			this.type = type;
			this.timestamp = timestamp;
			this.nBytes = nBytes;
		}
	}

	public class ConnectionMetrics : IHasReaderWriterLock{
		public enum ConnectionStatus
		{
			NotConnected,
			Pending,
			Connected,
			Lost,
			DynamicBlacklist,
			Filtered
		}

		public const string BYTES_IN = "BytesIn";
		public const string BYTES_OUT = "BytesOut";
		public const string SUM_BYTES_OUT = "SumBytesOut";
		public const string SUM_BYTES_IN = "SumBytesIn";
		public const string TOTAL_BYTES_OUT = "SumBytesOut";
		public const string TOTAL_BYTES_IN = "SumBytesIn";

		public bool DynamicBlacklistOnNextEvent { get; set; }
		public bool FilterOnNextEvent { get; set; }
		
		public bool ForgetOnNextDisconnection { get; set; }
		
		public Dictionary<string, double> Stats => this.stats;
		
		private readonly Dictionary<ConnectionEvent.Type, FixedConcurrentQueue<ConnectionEvent>> history = new Dictionary<ConnectionEvent.Type, FixedConcurrentQueue<ConnectionEvent>>();

		private readonly Dictionary<string, double> stats = new Dictionary<string, double>();

		private bool isStatsDirty = true;
		private double metric;

		public Double LastMetric => metric;

		private DateTime lastEventTimestamp = DateTimeEx.MinValue;

		public ConnectionMetrics(uint historySize = 100) {
			this.Status = ConnectionStatus.NotConnected;

			this.NextConnectionAttempt = this.LastPeerIpsReceived = this.LastPeerIpsRequested = DateTimeEx.MinValue;

			foreach(ConnectionEvent.Type type in Enum.GetValues(typeof(ConnectionEvent.Type))) {
				this.history[type] = new FixedConcurrentQueue<ConnectionEvent>(historySize);
				this.stats[NameOf(type)] = 0;
			}

			this.stats[BYTES_IN] = 0;
			this.stats[BYTES_OUT] = 0;
			this.stats[SUM_BYTES_OUT] = 0;
			this.stats[SUM_BYTES_IN] = 0;
			this.stats[TOTAL_BYTES_OUT] = 0;
			this.stats[TOTAL_BYTES_IN] = 0;
		}
		
		public ConnectionStatus Status { get; set; }
		public DateTime LastPeerIpsReceived { get; set; }
		public DateTime LastPeerIpsRequested { get; set; }

		public DateTime NextConnectionAttempt { get; set; }
		
		public double Latency { get; set; }
		public double LatencyStd { get; set; }
		private static string NameOf(ConnectionEvent.Type type) {
			return Enum.GetName(typeof(ConnectionEvent.Type), type);
		}

		public void AddEvent(ConnectionEvent e)
		{
			uint overflowBytes = 0;
			if(!this.history[e.type].Enqueue(e, out ConnectionEvent overflow))
				overflowBytes = overflow.nBytes;

			this.lastEventTimestamp = e.timestamp;
			this.isStatsDirty = true;
			double delta = Convert.ToDouble(e.nBytes) - overflowBytes;
			switch(e.type) {
				case ConnectionEvent.Type.Input:
					this.stats[SUM_BYTES_IN] += delta;
					this.stats[TOTAL_BYTES_IN] += e.nBytes;
					break;
				case ConnectionEvent.Type.Output:
					this.stats[SUM_BYTES_OUT] += delta;
					this.stats[TOTAL_BYTES_OUT] += e.nBytes;

					break;
			}

		}

		private bool UpdateStats() {
			if(!this.isStatsDirty) {
				return false;
			}

			this.isStatsDirty = false;

			foreach((ConnectionEvent.Type eventType, FixedConcurrentQueue<ConnectionEvent> eventHistory) in this.history) {
				int nEvents = eventHistory.Count;

				//better would be to fit a poisson distribution on an histogram, 
				//that would allow us to predict the probability of any event to occur over some timespan
				//e.g. https://stackoverflow.com/questions/25828184/fitting-to-poisson-histogram
				double stat = 0.0;

				foreach(ConnectionEvent oldEvent in eventHistory) {
					stat += Math.Pow(0.95, Math.Max(0.5, (this.lastEventTimestamp - oldEvent.timestamp).TotalSeconds));
				}

				this.stats[NameOf(eventType)] = stat;

				if(nEvents <= 1) {
					continue;
				}

				double deltaT = (this.lastEventTimestamp - eventHistory.First().timestamp).TotalSeconds;
				double epsilon = deltaT <= .1 ? 1.0 : 0;
				switch(eventType) {
					case ConnectionEvent.Type.Input:
						this.stats[BYTES_IN] = (this.stats[SUM_BYTES_IN] + epsilon) / (deltaT + epsilon);

						break;
					case ConnectionEvent.Type.Output:
						this.stats[BYTES_OUT] = (this.stats[SUM_BYTES_OUT] + epsilon) / (deltaT + epsilon);

						break;
				}

			}

			return true;
		}

		public bool UpdateMetric(IConnectionsProvider provider, NodeAddressInfo key)
		{
			if (!this.UpdateStats())
				return false;
			
			double multiplier = 1.0;

			switch(key.PeerInfo.PeerType) {
				case Enums.PeerTypes.Unknown:
					multiplier += 0.0;

					break;
				case Enums.PeerTypes.Mobile:
					multiplier += 0.5;

					break;
				case Enums.PeerTypes.Sdk:
					multiplier += 1.5;

					break;
				case Enums.PeerTypes.FullNode:
					multiplier += 2.0;

					break;
				case Enums.PeerTypes.Hub:
					multiplier += -1e6;

					break;
			}

			switch(key.PeerInfo.GossipSupportType) {
				case Enums.GossipSupportTypes.None:
					multiplier += 0;

					break;
				case Enums.GossipSupportTypes.Basic:
					multiplier += 1;

					break;
				case Enums.GossipSupportTypes.Full:
					multiplier += 2;

					break;
			}

			bool anyBlockchainMatch = false;

			var peerChains = key.PeerInfo.GetSupportedBlockchains();

			var commonChains = peerChains.Intersect(provider.ChainSettings.Keys).ToList();

			foreach (var chain in commonChains)
			{
				anyBlockchainMatch = true;

				switch(key.GetNodeShareType(chain).ShareType.SharingType) {
					case Enums.ChainSharingTypes.None:
						multiplier += 0;

						break;
					case Enums.ChainSharingTypes.BlockOnly:
						if (provider.ChainSettings[chain].ShareType.SharingType == Enums.ChainSharingTypes.BlockOnly)
							multiplier += 1;

						multiplier += 1;

						break;
					case Enums.ChainSharingTypes.DigestThenBlocks:
						if (provider.ChainSettings[chain].ShareType.SharingType == Enums.ChainSharingTypes.DigestThenBlocks)
							multiplier += 1;

						multiplier += 1;
						
						break;
					case Enums.ChainSharingTypes.Full:
						multiplier += 2;

						break;
				}
			}


			if(!anyBlockchainMatch || !key.IsConnectable) {
				multiplier = 1.0; // we reset multiplier to its minimum possible value
			}
			
			// modulate the multiplier with the latency
			// 0.1 second is considered the good enough value, under which we stop caring
			multiplier *= 1.0 / Math.Max(0.1, this.Latency); 

			double metric = this.StatsMetric();

			if (metric > 0)
				metric *= multiplier;
			else
				metric /= multiplier;

			this.metric = metric;
			return true;
		}
		public double Metric(IConnectionsProvider provider, NodeAddressInfo key)
		{
			this.UpdateMetric(provider, key);
			return this.metric;
		}
		private double StatsMetric() {
			return (  (1e-3  * this.stats[BYTES_OUT]) 
			        + (1e-3  * this.stats[BYTES_IN]) 
			        + (1.0   * this.stats[NameOf(ConnectionEvent.Type.Input)]) 
			        + (1.0   * this.stats[NameOf(ConnectionEvent.Type.Output)]) 
			        + (10.0  * this.stats[NameOf(ConnectionEvent.Type.Login)]))
					+ (50.0) * this.stats[NameOf(ConnectionEvent.Type.InputSliceSync)]
					+ (500.0) * this.stats[NameOf(ConnectionEvent.Type.SyncComplete)] //this peer is up-to-date with the public BlockHeight, it has a lot of value
					- (50.0) * this.stats[NameOf(ConnectionEvent.Type.PeerListRequestError)]
			        - (50.0) * this.stats[NameOf(ConnectionEvent.Type.SyncError)]
			        - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Logout)]) 
			        - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Timeout)]);
		}

		public void PrintStats(NodeAddressInfo node) {
			this.UpdateStats();

			foreach((string name, double value) in this.stats) {
				NLog.IPCrawler.Information($"{IPCrawler.TAG} stats for {node}: {name} = {value:0.00}");
			}
		}

		public ReaderWriterLock Mutex { get; } = new ReaderWriterLock();
	}
}