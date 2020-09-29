using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionsProvider {
		void RequestHubIPs();
		void RequestPeerIPs(NodeAddressInfo peerIP);
		void RequestConnect(NodeAddressInfo node);
		void RequestDisconnect(NodeAddressInfo node);

		bool SupportsChain(BlockchainType blockchainType);

	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class IPCrawler {

		public const string TAG = "[" + nameof(IPCrawler) + "]";
		private readonly int averagePeerCount;
		private readonly int maxPeerCount;
		private readonly int maxMobilePeerCount;
		private readonly int maxNonConnectableNodes;
		private readonly double hubIPsRequestPeriod;
		private readonly double peerIPsRequestPeriod;
		private readonly double peerReconnectionPeriod;
		private readonly double dynamicBlacklistPeriod;
		
		private DateTime lastHubIpsReceived;
		private DateTime lastHubIpsRequested;
		private readonly Dictionary<NodeAddressInfo, ConnectionMetrics> connections = new Dictionary<NodeAddressInfo, ConnectionMetrics>();
		private readonly Dictionary<NodeAddressInfo, ConnectionMetrics> mobileConnections = new Dictionary<NodeAddressInfo, ConnectionMetrics>();
		private readonly FixedQueue<int> peerCounts = new FixedQueue<int>(10);
		private int nConnected = 0;
		private int nNonConnectable = 0;
		private int nMobileConnected = 0;
		private readonly List<NodeAddressInfo> dynamicBlacklist = new List<NodeAddressInfo>();
		private readonly List<NodeAddressInfo> filteredNodes = new List<NodeAddressInfo>();
		
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
						return true;
					case AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots:
						NLog.IPCrawler.Information($"{TAG} whitelisted node's acceptanceType is '{AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots}' so we accept it only if a slot remains.");
						break;
					default:
						NLog.IPCrawler.Error($"{TAG} Undefined whitelisted node's acceptanceType: '{acceptanceType}', refusing.");
						break;
				}
			}
			
			if (node.PeerInfo.PeerType == Enums.PeerTypes.Mobile)
				return this.nMobileConnected < this.maxMobilePeerCount;
			
			return this.nConnected < this.maxPeerCount;
		}
		public IPCrawler(int averagePeerCount, int maxPeerCount, int maxMobilePeerCount
			, double hubIPsRequestPeriod = 1800.0, double peerIPsRequestPeriod = 600.0
			, double peerReconnectionPeriod = 60.0, double dynamicBlacklistPeriod = 24 * 60 * 60, int maxNonConnectableNodes = -1) {
			
			this.maxPeerCount = maxPeerCount;
			this.averagePeerCount = averagePeerCount;
			this.maxMobilePeerCount = maxMobilePeerCount;

			this.lastHubIpsReceived = this.lastHubIpsRequested = DateTimeEx.MinValue;
			this.peerIPsRequestPeriod = peerIPsRequestPeriod;
			this.hubIPsRequestPeriod = hubIPsRequestPeriod;
			this.peerReconnectionPeriod = peerReconnectionPeriod;
			this.dynamicBlacklistPeriod = dynamicBlacklistPeriod;
			this.maxNonConnectableNodes = maxNonConnectableNodes < 0 ? maxPeerCount : maxNonConnectableNodes;
		}

		public List<NodeAddressInfo> AllNodesList => this.connections.Keys.ToList();
		
		private ConnectionMetrics GocConnectionMetrics(NodeAddressInfo node)
		{
			var store = node.PeerInfo.PeerType == Enums.PeerTypes.Mobile ? this.mobileConnections : this.connections;

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
			ConnectionMetrics connection = this.GocConnectionMetrics(node);
			connection.LastPeerIpsReceived = timestamp;
			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Input, timestamp, Convert.ToUInt32(nodes.Count * 4U))); //FIXME nBytes is a bad approximation, not too important anyways

			this.CombineIPs(nodes);
		}

		public int SyncConnections(List<PeerConnection> connected, DateTime timestamp)
		{
			
			//FIXME: this method should not be necessary and the proper hooks should be added to e.g. ConnectionStore
			// to make certain we are always notified of new connections/disconnections
			// Please note that this is the only method that uses PeerConnection type, and this method should be removed

			List<PeerConnection> connectedMobile =
				connected.Where(c => c.NodeAddressInfo.PeerInfo.PeerType == Enums.PeerTypes.Mobile).ToList();
			
			List<PeerConnection> connectedOther =
				connected.Where(c => c.NodeAddressInfo.PeerInfo.PeerType != Enums.PeerTypes.Mobile).ToList();	
				
			int correctionsMade = 0;

			void MissingLogins(IReadOnlyDictionary<NodeAddressInfo, ConnectionMetrics> store, IEnumerable<PeerConnection> connections)
			{
				foreach (var node in connections)
				{
					bool missing = false;
					if (store.TryGetValue(node.NodeAddressInfo, out var metrics))
					{
						switch (metrics.Status)
						{
							case ConnectionMetrics.ConnectionStatus.Connected:
								continue;
							case ConnectionMetrics.ConnectionStatus.Pending:
							case ConnectionMetrics.ConnectionStatus.Filtered:
							case ConnectionMetrics.ConnectionStatus.Lost:
							case ConnectionMetrics.ConnectionStatus.NotConnected:
								NLog.IPCrawler.Verbose(
									$"{TAG} SyncConnections: found a wrong status for {node.NodeAddressInfo.PeerInfo.PeerType} node {node.NodeAddressInfo}, status was {metrics.Status} ");

								break;
							default:
								NLog.IPCrawler.Error(
									$"{TAG} SyncConnections: bad status for {node.NodeAddressInfo.PeerInfo.PeerType} node {node.NodeAddressInfo}, status was {metrics.Status} ");
								break;
						}
					}
					else
					{
						NLog.IPCrawler.Verbose(
							$"{TAG} SyncConnections: missing {node.NodeAddressInfo.PeerInfo.PeerType} node detected {node.NodeAddressInfo}");
					}
					
					HandleLogin(node.NodeAddressInfo, node.ConnectionTime, node.connection.Latency);
					correctionsMade++;
				}
			}

			MissingLogins(this.connections, connectedOther);
			MissingLogins(this.mobileConnections, connectedMobile);
			


			void MissingLogouts(IReadOnlyDictionary<NodeAddressInfo, ConnectionMetrics> store, IEnumerable<PeerConnection> connections)
			{
				
				var toDisconnect = store.Where(pair =>
					pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected &&
					!connections.Select(c => c.NodeAddressInfo).Contains(pair.Key)).ToList();

				foreach (var node in toDisconnect)
				{
					NLog.IPCrawler.Verbose($"{TAG} SyncConnections: found a missing HandleLogout for {node.Key.PeerInfo.PeerType} node {node.Key}, status was {node.Value.Status}... ");
					HandleLogout(node.Key, timestamp);
					correctionsMade++;
				}
			}
			
			MissingLogouts(this.connections, connectedOther);
			MissingLogouts(this.mobileConnections, connectedMobile);

			return correctionsMade;

		}
		public void CombineIPs(List<NodeAddressInfo> nodes)
		{
			List<NodeAddressInfo> newIPs = nodes.Where(node => !this.connections.ContainsKey(node)).ToList();
;
			foreach(NodeAddressInfo newIP in newIPs) {
				NLog.IPCrawler.Verbose($"{TAG} new node detected: {newIP}, registering...");
				ConnectionMetrics cInfo = new ConnectionMetrics();
				this.connections.Add(newIP, cInfo);
			}
		}

		public void SyncFilteredNodes(List<NodeAddressInfo> nodes, IConnectionsProvider provider)
		{
			List<NodeAddressInfo> unfilter = this.filteredNodes.Where(node => !nodes.Contains(node)).ToList();
			
			foreach(NodeAddressInfo node in unfilter) {
				if (this.connections.TryGetValue(node, out var connectionMetrics))
				{
					if (connectionMetrics.Status == ConnectionMetrics.ConnectionStatus.Filtered)
					{
						connectionMetrics.Status = ConnectionMetrics.ConnectionStatus.NotConnected;
						connectionMetrics.NextConnectionAttempt = DateTimeEx.MinValue;
						
					}
				} //else nothing to do
			}

			foreach (var node in nodes)
			{
				if (this.connections.TryGetValue(node, out var metrics))
				{
					switch (metrics.Status)
					{
						case ConnectionMetrics.ConnectionStatus.Filtered:
							break;
						case ConnectionMetrics.ConnectionStatus.Connected:
						case ConnectionMetrics.ConnectionStatus.Pending:
							provider.RequestDisconnect(node);
							metrics.FilterOnNextEvent = true;
							break;
						case ConnectionMetrics.ConnectionStatus.DynamicBlacklist:
							NLog.IPCrawler.Error($"{TAG} Filtering requested on a dynamic blacklist ndoe, this is illegal, updating status to Filtered.");
							metrics.Status = ConnectionMetrics.ConnectionStatus.Filtered;
							break;
						case ConnectionMetrics.ConnectionStatus.NotConnected:
						case ConnectionMetrics.ConnectionStatus.Lost:	
							metrics.Status = ConnectionMetrics.ConnectionStatus.Filtered;
							break;
						default:
							throw new NotImplementedException($"{TAG} unimplemented case");
						
					}
				}
				else
				{
					metrics = new ConnectionMetrics
					{
						Status = ConnectionMetrics.ConnectionStatus.Filtered,
					};

					this.connections.Add(node, metrics);
				}
				
				metrics.NextConnectionAttempt = DateTimeEx.MaxValue;
				
			}

			this.filteredNodes.Clear();
			this.filteredNodes.AddRange(nodes);

		}
		
		public void DisconnectAndBlacklist(NodeAddressInfo node, IConnectionsProvider provider, DateTime expiry)
		{
			if (this.connections.TryGetValue(node, out var connectionMetrics))
			{
				if (IPMarshall.Instance.IsWhiteList(node.Address, out var acceptanceType))
				{
					NLog.IPCrawler.Error($"{TAG} Trying to forget whitelisted node {node}, aborting...");
					return;
				}
					
				IPMarshall.Instance.Quarantine(node.Address, IPMarshall.QuarantineReason.DynamicBlacklist, expiry);
				
				if (connectionMetrics.Status == ConnectionMetrics.ConnectionStatus.Connected ||
				    connectionMetrics.Status == ConnectionMetrics.ConnectionStatus.Pending)
				{
					provider.RequestDisconnect(node);
				}
				
				connectionMetrics.NextConnectionAttempt = expiry;
				connectionMetrics.DynamicBlacklistOnNextEvent = true;
			}
		}

		private void UpdateConnectionStatus(NodeAddressInfo node, ConnectionMetrics metric, ConnectionMetrics.ConnectionStatus newStatus)
		{

			if (metric.Status == newStatus)
				return;
			
			void UpdateCounter(int delta)
			{
				if (node.PeerInfo.PeerType == Enums.PeerTypes.Mobile)
					this.nMobileConnected += delta;
				else
				{
					this.nConnected += delta;
					if (!node.IsConnectable) this.nNonConnectable += delta;
				}
				
				
			}
			
			if (metric.Status != ConnectionMetrics.ConnectionStatus.Connected && newStatus == ConnectionMetrics.ConnectionStatus.Connected)
				UpdateCounter(+1); // Any -> Connected
			else if (metric.Status == ConnectionMetrics.ConnectionStatus.Connected)
				UpdateCounter(-1); // Connected -> Any

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
		}
		public void HandleLogin(NodeAddressInfo node, DateTime timestamp, double latency)
		{
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			if (metric.Status == ConnectionMetrics.ConnectionStatus.Filtered)
			{
				NLog.IPCrawler.Error($"{TAG} {nameof(this.HandleLogin)} this node is supposed to be filtered, a login is not supposed to happen");
				metric.FilterOnNextEvent = true;
			}
			
			metric.Latency = latency;
																
			metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Login, timestamp, 0));

			this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected);
		}

		public void HandleLogout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Logout, timestamp, 0));
			
			this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.NotConnected);
		}

		public void HandleTimeout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Timeout, timestamp, 0));

			this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Lost);
			
		}

		public void HandleInput(NodeAddressInfo node, DateTime timestamp, uint nBytes) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Input, timestamp, nBytes));

			this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected);

		}

		public void HandleOutput(NodeAddressInfo node, DateTime timestamp, uint nBytes) {
			ConnectionMetrics metric = this.GocConnectionMetrics(node);

			metric.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Output, timestamp, nBytes));

			this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Connected);

		}

		public double SumOfConnectedPeerMetrics(IConnectionsProvider provider)
		{
			var connectedNodes = ConnectedNodes(this.connections).Where(pair => pair.Key.IsConnectable);

			double sum = 0.0;

			foreach (var node in connectedNodes)
				sum += node.Value.WeightedMetric(provider, node.Key);

			return sum;
		}
		public double SumOfConnectedPeerLatencies()
		{
			var connectedNodes = ConnectedNodes(this.connections).Where(pair => pair.Key.IsConnectable);

			double sum = 0.0;

			foreach (var node in connectedNodes)
				sum += node.Value.Latency;

			return sum;
		}
		private static List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> ConnectedNodes(Dictionary<NodeAddressInfo, ConnectionMetrics> store)
		{
			return store.Where(pair => 
				pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected)
				.OrderByDescending(c => c.Value.Metric()).ToList();
		}
		
		public void Crawl(IConnectionsProvider provider, DateTime now) {

			if (this.dynamicBlacklist.Count > 0)
			{
				foreach (var node in dynamicBlacklist)
					DisconnectAndBlacklist(node, provider, DateTimeEx.CurrentTime.AddSeconds(dynamicBlacklistPeriod));
				dynamicBlacklist.Clear();
			}
			
			if(now > lastHubIpsRequested.AddSeconds(hubIPsRequestPeriod)) {
				provider.RequestHubIPs();
				lastHubIpsRequested = now;
			}

			var connectedNodes = ConnectedNodes(connections);
			var connectedConnectableNodes = connectedNodes.Where(pair => pair.Key.IsConnectable).ToList();
			var connectedNonConnectableNodes = connectedNodes.Where(pair => !pair.Key.IsConnectable).ToList();
			var connectedMobileNodes = ConnectedNodes(mobileConnections);
			
			
			
			int nConnectable = connectedConnectableNodes.Count;
			int nNonConnectable = connectedNonConnectableNodes.Count;
			
			//remove extra connections
			
			//connectable
			for(int i = 0; i < Math.Min(nConnectable - maxPeerCount, nConnectable); i++)
				provider.RequestDisconnect(connectedConnectableNodes[i].Key);
			
			//non-connectable
			for(int i = 0; i < Math.Min(nNonConnectable - maxNonConnectableNodes, nNonConnectable); i++)
				provider.RequestDisconnect(connectedNonConnectableNodes[i].Key);
			
			//mobile
			for(int i = 0; i < Math.Min(connectedMobileNodes.Count - maxMobilePeerCount, connectedMobileNodes.Count); i++)
				provider.RequestDisconnect(connectedMobileNodes[i].Key);
			
			// request peers, print stats
			foreach((NodeAddressInfo node, ConnectionMetrics connectionMetric) in connectedNodes) {
				if(now > connectionMetric.LastPeerIpsRequested.AddSeconds(peerIPsRequestPeriod)) {
					provider.RequestPeerIPs(node);
					connectionMetric.LastPeerIpsRequested = now;
				}

				NLog.IPCrawler.Verbose($"{TAG} connected peer {node} has metric" 
				                       + $" {connectionMetric.WeightedMetric(provider, node):E1}" 
				                       + $" and latency {connectionMetric.Latency:0.000} s.");

//                connectionMetric.PrintStats(node);
			}
			
			
			List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectionCandidates = connections.Where(pair =>
	        (pair.Value.Status != ConnectionMetrics.ConnectionStatus.Connected) && 
	        (pair.Value.Status != ConnectionMetrics.ConnectionStatus.Pending) &&
	        pair.Key.IsConnectable &&
	        (pair.Value.NextConnectionAttempt < now) &&
			!IPMarshall.Instance.IsQuarantined(pair.Key.Address)).OrderByDescending(pair => pair.Value.WeightedMetric(provider, pair.Key)).ToList();
			
			
			

			peerCounts.Enqueue(nConnectable);
			double avg = Convert.ToDouble(peerCounts.Sum(x => x)) / peerCounts.Count; // average last n nConnected counts

			for (int i = 0; i < Math.Min(averagePeerCount - Convert.ToInt32(avg), connectionCandidates.Count); i++)
			{
				var candidate = connectionCandidates[i];
				var node = candidate.Key;
				var metric = candidate.Value;
				provider.RequestConnect(node);
				this.UpdateConnectionStatus(node, metric, ConnectionMetrics.ConnectionStatus.Pending);
				metric.NextConnectionAttempt = now.AddSeconds(peerReconnectionPeriod);
			}

			NLog.IPCrawler.Information($"{TAG} {nConnectable} connectable peer(s) ({avg:0.00} in avg)" 
			+ $" with average latency {SumOfConnectedPeerLatencies()/nConnectable:0.000} s,"
			+ $" we have {connectionCandidates.Count} potential other connectable peers ready"
			+ $" to connect to and {connections.Count - nConnected - connectionCandidates.Count} peers in timeout.");
			
			NLog.IPCrawler.Information($"{TAG} we have {connectedMobileNodes.Count} mobile and {nNonConnectable} non-connectable peers connected");

		}
	}



	public class ConnectionEvent {
		public enum Type {
			Input,
			Output,
			Timeout,
			Login,
			Logout
		}

		public uint nBytes;
		public DateTime timestamp;

		public Type type = Type.Login;

		public ConnectionEvent(Type type, DateTime timestamp, uint nBytes) {
			this.type = type;
			this.timestamp = timestamp;
			this.nBytes = nBytes;
		}
	}

	public class ConnectionMetrics {
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

		public bool DynamicBlacklistOnNextEvent { get; set; }
		public bool FilterOnNextEvent { get; set; }
		private readonly Dictionary<ConnectionEvent.Type, FixedQueue<ConnectionEvent>> history = new Dictionary<ConnectionEvent.Type, FixedQueue<ConnectionEvent>>();

		private readonly Dictionary<string, double> stats = new Dictionary<string, double>();

		private bool isStatsDirty = true;

		private DateTime lastEventTimestamp = DateTimeEx.MinValue;

		public ConnectionMetrics(uint historySize = 100) {
			this.Status = ConnectionStatus.NotConnected;

			this.NextConnectionAttempt = this.LastPeerIpsReceived = this.LastPeerIpsRequested = DateTimeEx.MinValue;

			foreach(ConnectionEvent.Type type in Enum.GetValues(typeof(ConnectionEvent.Type))) {
				this.history[type] = new FixedQueue<ConnectionEvent>(historySize);
				this.stats[NameOf(type)] = 0;
			}

			this.stats[BYTES_IN] = 0;
			this.stats[BYTES_OUT] = 0;
			this.stats[SUM_BYTES_OUT] = 0;
			this.stats[SUM_BYTES_IN] = 0;
		}

		public ConnectionStatus Status { get; set; }
		public DateTime LastPeerIpsReceived { get; set; }
		public DateTime LastPeerIpsRequested { get; set; }

		public DateTime NextConnectionAttempt { get; set; }
		
		public double Latency { get; set; }

		private static string NameOf(ConnectionEvent.Type type) {
			return Enum.GetName(typeof(ConnectionEvent.Type), type);
		}

		public void AddEvent(ConnectionEvent e) {
			this.history[e.type].Enqueue(e, out ConnectionEvent overflow);

			this.lastEventTimestamp = e.timestamp;
			this.isStatsDirty = true;
			uint overflowBytes = overflow?.nBytes ?? 0;

			switch(e.type) {
				case ConnectionEvent.Type.Input:
					this.stats[SUM_BYTES_IN] += e.nBytes - overflowBytes;

					break;
				case ConnectionEvent.Type.Output:
					this.stats[SUM_BYTES_OUT] += e.nBytes - overflowBytes;

					break;
			}

		}

		private void UpdateStats() {
			if(!this.isStatsDirty) {
				return;
			}

			this.isStatsDirty = false;

			foreach((ConnectionEvent.Type eventType, FixedQueue<ConnectionEvent> eventHistory) in this.history) {
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
				double epsilon = deltaT == 0 ? 1e-5 : 0;
				switch(eventType) {
					case ConnectionEvent.Type.Input:
						this.stats[BYTES_IN] = (this.stats[SUM_BYTES_IN] + epsilon) / (deltaT + epsilon);

						break;
					case ConnectionEvent.Type.Output:
						this.stats[BYTES_OUT] = (this.stats[SUM_BYTES_OUT] + epsilon) / (deltaT + epsilon);

						break;
				}

			}

		}

		public double WeightedMetric(IConnectionsProvider provider, NodeAddressInfo key)
		{

				
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

			foreach(BlockchainType blockchainType in key.PeerInfo.GetSupportedBlockchains()) {
				if(provider.SupportsChain(blockchainType)) {
					anyBlockchainMatch = true;

					switch(key.GetNodeShareType(blockchainType).ShareType.SharingType) {
						case Enums.ChainSharingTypes.None:
							multiplier += 0;

							break;
						case Enums.ChainSharingTypes.BlockOnly:
							multiplier += 1;

							break;
						case Enums.ChainSharingTypes.DigestThenBlocks:
							multiplier += 1.5;

							break;
						case Enums.ChainSharingTypes.Full:
							multiplier += 2;

							break;
					}
				}
			}

			if(!anyBlockchainMatch) {
				multiplier = 1.0; // we reset multiplier to its minimum possible value
			}
			
			if (!key.IsConnectable)
			{
				multiplier = -1.0; //cripple it
			}
			// modulate the multiplier with the latency
			// 0.1 second is considered the good enough value, under which we stop caring
			multiplier *= 1.0 / Math.Max(0.1, this.Latency); 

			double metric = this.Metric();

			if(metric > 0) {
				return multiplier * metric;
			}

			return metric / multiplier;

		}
		
		public double Metric() {
			this.UpdateStats();
			return (  (1e-3  * this.stats[BYTES_OUT]) 
			        + (1e-3  * this.stats[BYTES_IN]) 
			        + (1.0   * this.stats[NameOf(ConnectionEvent.Type.Input)]) 
			        + (1.0   * this.stats[NameOf(ConnectionEvent.Type.Output)]) 
			        + (10.0  * this.stats[NameOf(ConnectionEvent.Type.Login)])) 
			        - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Logout)]) 
			        - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Timeout)]);
		}

		public void PrintStats(NodeAddressInfo node) {
			this.UpdateStats();

			foreach((string name, double value) in this.stats) {
				NLog.IPCrawler.Information($"{IPCrawler.TAG} stats for {node}: {name} = {value:0.00}");
			}
		}
	}
}