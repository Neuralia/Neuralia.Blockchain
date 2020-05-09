using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Neuralia.Blockchains.Core.Logging;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionsProvider {
		void RequestHubIPs();
		void RequestPeerIPs(NodeAddressInfo peerIP);
		void RequestConnect(NodeAddressInfo node);
		void RequestDisconnect(NodeAddressInfo node);

		bool SupportsChain(BlockchainType blockchainType);

	}

	public class NATRule {
		public NATRule(NodeAddressInfo from, IPAddress to, bool increment)
		{
			FromNode = from;
			ToIP = to;
			IncrementIPWithPortDelta = increment;
		}
		public NodeAddressInfo FromNode { get; set; } // Example "172.17.0.1:4000" -> matches sockets 172.17.0.1:4000, 172.17.0.1:4001, ...
		public IPAddress ToIP { get; set; } 
		// Example with IncrementIPWithPortDelta == true:  "172.23.0.42" -> translated to sockets 172.23.0.42:4000, 172.23.0.43:4001, ... 
		// Example with IncrementIPWithPortDelta == false:  "172.23.0.42" -> translated to sockets 172.23.0.42:4000, 172.23.0.42:4001, ... 

		public bool IncrementIPWithPortDelta { get; set; } = true;
	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class IPCrawler {

		public const string TAG = "[IPCrawler]";
		private readonly int averagePeerCount;
		private readonly Dictionary<NodeAddressInfo, ConnectionMetrics> connections = new Dictionary<NodeAddressInfo, ConnectionMetrics>();
		private readonly double hubIPsRequestPeriod;
		private readonly int maxPeerCount;
		private readonly FixedQueue<int> peerCounts = new FixedQueue<int>(10);
		private readonly double peerIPsRequestPeriod;
		private readonly double peerReconnectionPeriod;
		private DateTime lastHubIpsReceived;
		private DateTime lastHubIpsRequested;
		private readonly List<NATRule> translations;
		private readonly List<IPAddress> blacklist;
		public IPCrawler(int averagePeerCount, int maxPeerCount
			, double hubIPsRequestPeriod = 1800.0, double peerIPsRequestPeriod = 600.0
			, double peerReconnectionPeriod = 60.0
			, List<NATRule> translations = null
			, List<IPAddress> blacklist = null) {
			this.maxPeerCount = maxPeerCount;
			this.averagePeerCount = averagePeerCount;
			this.lastHubIpsReceived = this.lastHubIpsRequested = DateTime.MinValue;
			this.peerIPsRequestPeriod = peerIPsRequestPeriod;
			this.hubIPsRequestPeriod = hubIPsRequestPeriod;
			this.peerReconnectionPeriod = peerReconnectionPeriod;
			this.translations = translations;
			this.blacklist = blacklist;
		}

		private ConnectionMetrics GocConnectionMetrics(NodeAddressInfo node) {
//            node = node.MapToIPv6();
			if(!this.connections.ContainsKey(node)) {
				this.connections[node] = new ConnectionMetrics();
			}

			return this.connections[node];
		}
		
		private void TranslateIps(List<NodeAddressInfo> nodes)
		{
			// This is a feature that should only be used in some debug scenarios (e.g. docker)
			//
			// Let "172.17.0.1:4000" -> "172.23.0.42:****" be a user-provided translation rule
			// "4000" is the "source port offset", "42" is the "destination ip offset"
			// It will try to match a node with ip 172.17.0.1, then look at this node's port.
			// For example, let that node be 172.17.0.1:4001.
			// "4001" is "node's port"
			// It will then look at the destination of the translation rule and add ("node's port" - "source port offset") to "destination ip offset"
			// ... and take the modulo of the result. So in the end we have
			// 172.17.0.1:4001 -> 172.23.0.{(42 + 4001 - 4000)%256}:4001 ->  172.23.0.43:4001 
			//
			// This supports only IPV4 for the moment.
	
			
			foreach (var node in nodes)
			{
				var matches = translations.Where(fromTo => fromTo.FromNode.AdjustedIp == node.AdjustedIp).ToList();

				if (matches.Count > 0)
				{
					var translation = matches[0];
					
					if(matches.Count > 1)
						NLog.IPCrawler.Warning($"{TAG} duplicate translation source detected of address {node.AdjustedIp}, will be using {translation.FromNode}.");
					
					if (node.IsIpV6)
						NLog.IPCrawler.Warning($"{TAG} trying to translate an IPV6 Address, not implemented.");

					var translationFrom = Regex.Match(node.Ip, @"(?<a>\d{1,3}).(?<b>\d{1,3}).(?<c>\d{1,3}).(?<d>\d{1,3})");

					var translationTo = Regex.Match(translation.ToIP.ToString(), @"(?<a>\d{1,3}).(?<b>\d{1,3}).(?<c>\d{1,3}).(?<d>\d{1,3})");

					var sourcePortOffset = translation.FromNode.RealPort;
					var destinationIPOffset = Int32.Parse(translationTo.Groups["d"].ToString());
					var d = translation.IncrementIPWithPortDelta ? (destinationIPOffset + node.RealPort - sourcePortOffset) % 256 : destinationIPOffset;
					node.Address =
						IPAddress.Parse(
							$"{translationTo.Groups["a"]}.{translationTo.Groups["b"]}.{translationTo.Groups["c"]}.{d}");
				}
				
				
			}
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

		public void SyncConnections(List<NodeAddressInfo> connected, DateTime timestamp)
		{
			//FIXME: this method should not be necessary and the proper hooks should be added to e.g. ConnectionStore
			// to make certain we are always notified of new connections/disconnections
			List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectedIps = this.connections.Where(pair => 
				pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected).ToList();

			var toConnect = connected.Where(node =>
				!this.connections.ContainsKey(node) ||
				this.connections[node].Status != ConnectionMetrics.ConnectionStatus.Connected).ToList();
			
			foreach (var node in toConnect)
			{
				NLog.IPCrawler.Verbose($"{TAG} SyncConnections: found a missing HandleLogin... ");
				this.HandleLogin(node, timestamp);
			}

			var toDisconnect = this.connections.Where(pair =>
				pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected &&
				!connected.Contains(pair.Key)).Select(pair => pair.Key).ToList();

			foreach (var node in toDisconnect)
			{
				NLog.IPCrawler.Verbose($"{TAG} SyncConnections: found a missing HandleLogout... ");
				this.HandleLogout(node, timestamp);
			}

		}
		public void CombineIPs(List<NodeAddressInfo> nodes)
		{

			this.TranslateIps(nodes);
			
			List<NodeAddressInfo> newIPs = nodes.Where(node =>
			{
				bool isNew = !this.connections.ContainsKey(node);
				bool isBlacklisted = this.blacklist.Contains(node.Address);
				
				if(isBlacklisted)
					NLog.IPCrawler.Verbose($"{TAG} {node} is blacklisted, not registering...");
				
				return isNew && !isBlacklisted;
			}).ToList();
			
			foreach(NodeAddressInfo newIP in newIPs) {
				NLog.IPCrawler.Verbose($"{TAG} new node detected: {newIP}, registering...");
				ConnectionMetrics cInfo = new ConnectionMetrics();
				this.connections.Add(newIP, cInfo);
			}
		}

		public void HandleLogin(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics connection = this.GocConnectionMetrics(node);

			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Login, timestamp, 0));

			connection.Status = ConnectionMetrics.ConnectionStatus.Connected;
		}

		public void HandleLogout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics connection = this.GocConnectionMetrics(node);

			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Logout, timestamp, 0));

			connection.Status = ConnectionMetrics.ConnectionStatus.NotConnected;
		}

		public void HandleTimeout(NodeAddressInfo node, DateTime timestamp) {
			ConnectionMetrics connection = this.GocConnectionMetrics(node);

			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Timeout, timestamp, 0));

			connection.Status = ConnectionMetrics.ConnectionStatus.Lost;
		}

		public void HandleInput(NodeAddressInfo node, DateTime timestamp, uint nBytes) {
			ConnectionMetrics connection = this.GocConnectionMetrics(node);

			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Input, timestamp, nBytes));

			connection.Status = ConnectionMetrics.ConnectionStatus.Connected;

		}

		public void HandleOutput(NodeAddressInfo node, DateTime timestamp, uint nBytes) {
			ConnectionMetrics connection = this.GocConnectionMetrics(node);

			connection.AddEvent(new ConnectionEvent(ConnectionEvent.Type.Output, timestamp, nBytes));

			connection.Status = ConnectionMetrics.ConnectionStatus.Connected;

		}
		


		public void Crawl(IConnectionsProvider provider, DateTime now) {
			if(now > this.lastHubIpsRequested.AddSeconds(this.hubIPsRequestPeriod)) {
				provider.RequestHubIPs();
				this.lastHubIpsRequested = now;
			}

			List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectedIps = this.connections.Where(pair => 
				pair.Value.Status == ConnectionMetrics.ConnectionStatus.Connected).OrderByDescending(c => c.Value.Metric()).ToList();

			List<KeyValuePair<NodeAddressInfo, ConnectionMetrics>> connectionCandidates = this.connections.Where(pair =>
	        (pair.Value.Status != ConnectionMetrics.ConnectionStatus.Connected) && 
	        (pair.Value.Status != ConnectionMetrics.ConnectionStatus.Pending) &&
	        (pair.Value.NextConnectionAttempt < now)).OrderByDescending(pair => pair.Value.WeightedMetric(provider, pair.Key)).ToList();

			int nConnected = connectedIps.Count;

			foreach((NodeAddressInfo node, ConnectionMetrics connectionMetric) in connectedIps) {
				if(now > connectionMetric.LastPeerIpsRequested.AddSeconds(this.peerIPsRequestPeriod)) {
					provider.RequestPeerIPs(node);
					connectionMetric.LastPeerIpsRequested = now;
				}

				NLog.IPCrawler.Verbose($"{TAG} connected peer {node} has metric {connectionMetric.Metric()}.");

//                connectionMetric.PrintStats(node);
			}

			for(int i = 0; i < Math.Min(nConnected - this.maxPeerCount, connectedIps.Count); i++) {
				//remove extra connections
				provider.RequestDisconnect(connectedIps[i].Key);
			}

			this.peerCounts.Enqueue(nConnected);
			double avg = Convert.ToDouble(this.peerCounts.Sum(x => x)) / this.peerCounts.Count; // average last n nConnected counts

			for(int i = 0; i < Math.Min(this.averagePeerCount - Convert.ToInt32(avg), connectionCandidates.Count); i++) {
				provider.RequestConnect(connectionCandidates[i].Key);
				connectionCandidates[i].Value.Status = ConnectionMetrics.ConnectionStatus.Pending;
				connectionCandidates[i].Value.NextConnectionAttempt = now.AddSeconds(this.peerReconnectionPeriod);
			}

			NLog.IPCrawler.Information($"{TAG} average peer count is {avg} peer(s), we have {connectionCandidates.Count} potential other peers ready"
			+ $" to connect to and {this.connections.Count - nConnected - connectionCandidates.Count} peers in timeout.");
		}
	}

	public class FixedQueue<T> : Queue<T> {

		public FixedQueue(uint limit) {
			this.Limit = limit;
		}

		public uint Limit { get; set; }

		public new void Enqueue(T obj) {
			this.Enqueue(obj, out T overflow);
		}

		public void Enqueue(T obj, out T overflow) {
			base.Enqueue(obj);
			overflow = default;

			if(this.Count > this.Limit) {
				this.TryDequeue(out overflow);
			}
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
		public enum ConnectionStatus //TODO use some pertinent Neuralium enum
		{
			NotConnected,
			Pending,
			Connected,
			Lost
		}

		public const string BYTES_IN = "BytesIn";
		public const string BYTES_OUT = "BytesOut";
		public const string SUM_BYTES_OUT = "SumBytesOut";
		public const string SUM_BYTES_IN = "SumBytesIn";

		private readonly Dictionary<ConnectionEvent.Type, FixedQueue<ConnectionEvent>> history = new Dictionary<ConnectionEvent.Type, FixedQueue<ConnectionEvent>>();

		private readonly Dictionary<string, double> stats = new Dictionary<string, double>();

		private bool isStatsDirty = true;

		private DateTime lastEventTimestamp = DateTime.MinValue;

		public ConnectionMetrics(uint historySize = 100) {
			this.Status = ConnectionStatus.NotConnected;

			this.NextConnectionAttempt = this.LastPeerIpsReceived = this.LastPeerIpsRequested = DateTime.MinValue;

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

				switch(eventType) {
					case ConnectionEvent.Type.Input:
						this.stats[BYTES_IN] = this.stats[SUM_BYTES_IN] / deltaT;

						break;
					case ConnectionEvent.Type.Output:
						this.stats[BYTES_OUT] = this.stats[SUM_BYTES_OUT] / deltaT;

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
					multiplier += 2.0;

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

			double metric = this.Metric();

			if(metric > 0) {
				return multiplier * metric;
			}

			return metric / multiplier;

		}
		
		public double Metric() {
			this.UpdateStats();

			return ((+1.0 * this.stats[BYTES_OUT]) + (1.0 * this.stats[BYTES_IN]) + (1.0 * this.stats[NameOf(ConnectionEvent.Type.Input)]) + (1.0 * this.stats[NameOf(ConnectionEvent.Type.Output)]) + (10.0 * this.stats[NameOf(ConnectionEvent.Type.Login)])) - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Logout)]) - (100.0 * this.stats[NameOf(ConnectionEvent.Type.Timeout)]);
		}

		public void PrintStats(NodeAddressInfo node) {
			this.UpdateStats();

			foreach((string name, double value) in this.stats) {
				NLog.IPCrawler.Information($"{IPCrawler.TAG} stats for {node}: {name} = {value}");
			}
		}
	}
}