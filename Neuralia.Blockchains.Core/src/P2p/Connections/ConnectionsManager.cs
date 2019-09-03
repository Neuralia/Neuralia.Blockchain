using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {

	public interface IConnectionsManager : ISimpleRoutedTaskHandler, IColoredRoutedTaskHandler, ILoopThread {
	}

	public interface IConnectionsManager<R> : ILoopThread<ConnectionsManager<R>>, IConnectionsManager
		where R : IRehydrationFactory {
	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class ConnectionsManager<R> : LoopThread<ConnectionsManager<R>>, IConnectionsManager<R>
		where R : IRehydrationFactory {

		private const decimal MaxNullChainNodesPercent = 0.1M; // maximum of 10% null chain nodes.  mode than that, we will disconnect.

		private const int MaxConnectionAttemptCount = 3; // maximum of times we try a peer
		private const int minAcceptableNullChainCount = 1; // maximum of times we try a peer

		private const int MAX_SECONDS_BEFORE_NEXT_PEER_LIST_REQUEST = 3 * 60;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT = 3 * 60;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT = 3 * 60;

		private const int PEER_CONNECTION_ATTEMPT_COUNT = 3;
		private const int PEER_CONNECTION_SET_ATTEMPT_COUNT = 2;

		protected readonly IClientWorkflowFactory<R> clientWorkflowFactory;

		protected readonly ColoredRoutedTaskReceiver coloredTaskReceiver;

		protected readonly IConnectionStore connectionStore;

		private readonly List<ConnectionsManager.RequestMoreConnectionsTask> explicitConnectionRequests = new List<ConnectionsManager.RequestMoreConnectionsTask>();

		protected readonly IGlobalsService globalsService;
		private readonly INetworkingService<R> networkingService;

		/// <summary>
		///     collection where we store information about our connection attempts
		/// </summary>
		/// <returns></returns>
		private readonly Dictionary<string, ConnectionManagerActivityInfo> peerActivityInfo = new Dictionary<string, ConnectionManagerActivityInfo>();

		/// <summary>
		///     The receiver that allows us to act as a task endpoint mailbox
		/// </summary>
		protected readonly SimpleRoutedTaskReceiver RoutedTaskReceiver;

		private DateTime? nextAction;

		private DateTime nextHubContact = DateTime.MinValue;
		private DateTime? nextUpdateNodeCountAction;

		public ConnectionsManager(ServiceSet<R> serviceSet) : base(1000) {

			this.globalsService = serviceSet.GlobalsService;
			this.networkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();

			this.clientWorkflowFactory = serviceSet.InstantiationService.GetClientWorkflowFactory(serviceSet);

			this.connectionStore = this.networkingService.ConnectionStore;

			this.coloredTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleColoredTask);

			this.RoutedTaskReceiver = new SimpleRoutedTaskReceiver();

			this.RoutedTaskReceiver.TaskReceived += () => {
			};
		}

		/// <summary>
		///     interface method to receive tasks into our mailbox
		/// </summary>
		/// <param name="task"></param>
		public void ReceiveTask(ISimpleTask task) {
			this.RoutedTaskReceiver.ReceiveTask(task);
		}

		/// <summary>
		///     interface method to receive tasks into our mailbox
		/// </summary>
		/// <param name="task"></param>
		public void ReceiveTask(IColoredTask task) {
			this.coloredTaskReceiver.ReceiveTask(task);
		}

		protected virtual void HandleColoredTask(IColoredTask task) {

			if(task is ConnectionsManager.RequestMoreConnectionsTask requestConnectionsTask) {
				// ok, sombody requested more connections. lets do it!
				this.explicitConnectionRequests.Add(requestConnectionsTask);

				// lets act now!
				this.nextAction = DateTime.Now;
			}
		}

		/// <summary>
		///     returns the list of peer connection from the connection store, matched with our own list of activity connection for
		///     this peer.
		/// </summary>
		/// <returns></returns>
		private IEnumerable<PeerJoinedInfo> GetJoinedPeerInfos() {
			return this.connectionStore.AllConnectionsList.Join(this.peerActivityInfo.Values, pi => pi.ScopedIp, pai => pai.ScopedIp, (pi, pai) => new PeerJoinedInfo {PeerConnection = pi, ConnectionManagerActivityInfo = pai});
		}

		/// <summary>
		///     lets ask our peers to provide us with their peer list
		/// </summary>
		protected void RequestPeerLists() {
			// join together the peer connection we have with the peer activity connection. this way we can correlate and make an educated decision
			var matchedConnections = this.GetJoinedPeerInfos();

			// now we choose the ones that were either never contacted (probably received a connection from them) or the ones we contacted long ago, so we can bother them again
			var availablePeers = matchedConnections.Where(m => {
				return (m.ConnectionManagerActivityInfo != null) && ((DateTime.Now - m.ConnectionManagerActivityInfo.lastPeerListRequestAttempt) > TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_PEER_LIST_REQUEST));
			});

			foreach(PeerJoinedInfo peer in availablePeers) {
				// thats it, we can query this peer and ask for his/her peer list

				// ok, contact the peer and ask for their connection

				// yup, we are asking for it, so lets update our records
				peer.ConnectionManagerActivityInfo.lastPeerListRequestAttempt = DateTime.Now; // set it just in case

				// ask for the peers!
				//TODO: create workflow here
				ClientPeerListRequestWorkflow<R> peerListRequest = null;

				try {
					Log.Verbose($"attempting to query peer list from peer {peer.PeerConnection.ScopedAdjustedIp}");

					peerListRequest = this.clientWorkflowFactory.CreatePeerListRequest(peer.PeerConnection);

					peerListRequest.Completed += (success, wf) => {
						// run this task in the connection manager thread by sending a delegated task
						SimpleTask task = new SimpleTask();

						task.Action += sender => {
							peer.ConnectionManagerActivityInfo.lastPeerListRequestAttempt = DateTime.Now; // update it
						};

						this.ReceiveTask(task);
					};

					this.networkingService.WorkflowCoordinator.AddWorkflow(peerListRequest);
				} catch(Exception ex) {
					Log.Error(ex, "failed to query peer list");
				}
			}
		}

		protected void CreateConnectionAttempt(NodeAddressInfo node) {
			ConnectionManagerActivityInfo connectionManagerActivityInfo = null;

			NodeActivityInfo nodeActivityInfo = this.connectionStore.GetNodeActivityInfo(node);

			if(nodeActivityInfo == null) {
				nodeActivityInfo = new NodeActivityInfo(node, true);
			}

			if(this.peerActivityInfo.ContainsKey(node.ScopedIp)) {
				connectionManagerActivityInfo = this.peerActivityInfo[node.ScopedIp];
			} else {
				connectionManagerActivityInfo = new ConnectionManagerActivityInfo(nodeActivityInfo);

				connectionManagerActivityInfo.lastConnectionAttempt = DateTime.MinValue;
				connectionManagerActivityInfo.lastPeerListRequestAttempt = DateTime.Now; // since we get the list during the handshake

				this.peerActivityInfo.Add(connectionManagerActivityInfo.ScopedIp, connectionManagerActivityInfo);
			}

			if(connectionManagerActivityInfo.inProcess) {
				// already working
				return;
			}

			// lets make one last check, to ensure this connection is not already happening (maybe they tried to connect to us) before we contact them
			if(this.connectionStore.PeerConnectionExists(nodeActivityInfo.Node.NetworkEndPoint, PeerConnection.Directions.Outgoing)) {
				// thats it, we are already connecting, lets stop here and ignore it
				return;
			}

			connectionManagerActivityInfo.lastConnectionAttempt = DateTime.Now; // set it just in case
			connectionManagerActivityInfo.connectionAttemptCounter++;
			connectionManagerActivityInfo.inProcess = true;

			// thats it, lets launch a connection

			try {
				Log.Verbose($"attempting connection attempt {connectionManagerActivityInfo.connectionAttemptCounter} to peer {node.ScopedAdjustedIp}");
				var handshake = this.clientWorkflowFactory.CreateRequestHandshakeWorkflow(ConnectionStore<R>.CreateEndpoint(node));

				handshake.Error2 += (workflow, ex) => {

					// anything to do here?
				};

				handshake.Completed2 += (success, wf) => {
					// run this task in the connection manager thread by sending a delegated task
					SimpleTask task = new SimpleTask();

					task.Action += sender => {
						connectionManagerActivityInfo.lastConnectionAttempt = DateTime.Now; // update it
						connectionManagerActivityInfo.inProcess = false;

						if(success) {
							connectionManagerActivityInfo.connectionAttemptCounter = 0; // reset it
							++connectionManagerActivityInfo.successfullConnectionCounter;

							nodeActivityInfo.AddEvent(new NodeActivityInfo.NodeActivityEvent(NodeActivityInfo.NodeActivityEvent.NodeActivityEventTypes.Success));
						} else {

							nodeActivityInfo.AddEvent(new NodeActivityInfo.NodeActivityEvent(NodeActivityInfo.NodeActivityEvent.NodeActivityEventTypes.Failure));

							if(connectionManagerActivityInfo.connectionAttemptCounter >= PEER_CONNECTION_ATTEMPT_COUNT) {

								connectionManagerActivityInfo.connectionSetAttemptCounter++;
								connectionManagerActivityInfo.connectionAttemptCounter = 0; // reset it

								if(connectionManagerActivityInfo.connectionSetAttemptCounter >= PEER_CONNECTION_SET_ATTEMPT_COUNT) {

									Log.Verbose($"Reached max connection attempt for peer {node.ScopedAdjustedIp}. this peer will now be ignored form now on.");

									this.connectionStore.AddIgnorePeerNode(nodeActivityInfo);

									// remove it all, its over
									if(this.peerActivityInfo.ContainsKey(node.ScopedIp)) {
										this.peerActivityInfo.Remove(node.ScopedIp);
									}
								} else {

									Log.Verbose($"Reached max connection attempt for peer {node.ScopedAdjustedIp}. will retry later");
								}

							} else {
								Log.Verbose($"Failed to connect to peer {node.ScopedAdjustedIp}. Attempt {connectionManagerActivityInfo.connectionAttemptCounter} of {PEER_CONNECTION_ATTEMPT_COUNT}");
							}

						}
					};

					this.ReceiveTask(task);
				};

				this.networkingService.WorkflowCoordinator.AddWorkflow(handshake);
			} catch(Exception ex) {
				Log.Error(ex, "failed to create handshake");
			}
		}

		protected override void ProcessLoop() {
			try {
				this.CheckShouldCancel();

				// first thing, lets check if we have any tasks received to process
				this.CheckTasks();

				this.CheckShouldCancel();

				if(this.ShouldAct(ref this.nextAction)) {

					// ok, its time to act
					int secondsToWait = 3; // default next action time in seconds. we can play on this

					//-------------------------------------------------------------------------------
					// phase 1: Ensure we maintain our connection information up to date

					// first thing, lets detect new incoming connections that we may not have in our activity list (since we did not create thei connection) and give them and activity log
					var newIncomingConnections = this.GetJoinedPeerInfos().Where(p => (p.ConnectionManagerActivityInfo == null) && (p.PeerConnection.direction == PeerConnection.Directions.Incoming));

					foreach(PeerJoinedInfo newIncomingConnection in newIncomingConnections) {
						ConnectionManagerActivityInfo connectionManagerActivityInfo = new ConnectionManagerActivityInfo(newIncomingConnection.PeerConnection.NodeActivityInfo);

						// we assume we just talked to them through the handshake
						connectionManagerActivityInfo.lastConnectionAttempt = DateTime.Now;
						connectionManagerActivityInfo.lastPeerListRequestAttempt = DateTime.Now;

						// and add them to our list
						this.peerActivityInfo.Add(connectionManagerActivityInfo.ScopedIp, connectionManagerActivityInfo);
					}

					this.CheckShouldCancel();

					// now get this number every time, since we gain new connections from the server all the time, and others just disconnect
					int activeConnectionsCount = this.connectionStore.ActiveConnectionsCount;

					// the list of active connections we will act on
					var activeConnections = this.connectionStore.AllConnectionsList;

					//-------------------------------------------------------------------------------
					// phase 2: search for more connections if we dont have enough

					decimal CRITICAL_LOW_CONNECTION_PCT = 0.2M;
					decimal LOW_CONNECTION_PCT = 0.4M;

					decimal averagePerCount = GlobalSettings.ApplicationSettings.averagePeerCount;

					int criticalLowConnectionLevel = (int) Math.Ceiling(averagePerCount * CRITICAL_LOW_CONNECTION_PCT);
					int lowConnectionLevel = (int) Math.Ceiling(averagePerCount * LOW_CONNECTION_PCT);

					if((activeConnectionsCount < GlobalSettings.ApplicationSettings.maxPeerCount) || this.explicitConnectionRequests.Any()) {
						// ok, in here, we will need to get more connections
						this.CheckShouldCancel();

						// get the list of IPs that we can connect to
						var availableNodes = this.GetAvailableNodesList(activeConnections);

						// pick nodes not already connecting
						availableNodes = availableNodes.Where(n => !this.peerActivityInfo.ContainsKey(n.ScopedIp) || !this.peerActivityInfo[n.ScopedIp].inProcess).ToList();

						if(this.explicitConnectionRequests.Any() && (activeConnectionsCount >= Math.Max(GlobalSettings.ApplicationSettings.maxPeerCount - 2, 0))) {
							// ok, no choice, we will have to cut loose some connections to make room for now ones
							var expendableConnections = this.explicitConnectionRequests.SelectMany(c => c.ExpendableConnections).Distinct().ToList();

							// ok, lets disconnect these guys...
							this.DisconnectPeers(expendableConnections);

							// refresh our current count now that we cleared some
							activeConnectionsCount = this.connectionStore.ActiveConnectionsCount;
						}

						this.CheckShouldCancel();

						if(!availableNodes.Any()) {
							// ok, we really have NO connections. let's contact a HUB and request more peers

							this.ContactHubs();

							secondsToWait = 30;

						} else {

							if((activeConnectionsCount < criticalLowConnectionLevel) || this.explicitConnectionRequests.Any()) {
								// ok, in this case its an urgent situation, lets try to connect aggressively

								availableNodes.Shuffle();
								var nodes = availableNodes.Take(3);

								foreach(NodeAddressInfo node in nodes) {
									this.CreateConnectionAttempt(node);
									this.CheckShouldCancel();
								}
							} else if(activeConnectionsCount < lowConnectionLevel) {
								// still serious, but we can rest a bit
								var nodes = availableNodes.Take(2);

								foreach(NodeAddressInfo nodeAddressInfo in nodes) {
									this.CreateConnectionAttempt(nodeAddressInfo);
									this.CheckShouldCancel();
								}

								secondsToWait = 10;
							} else {
								if(activeConnectionsCount < GlobalSettings.ApplicationSettings.averagePeerCount) {
									// ok, we still try to get more, but we can take it easy, we have a good average
									var nodes = availableNodes.Take(1);

									foreach(NodeAddressInfo node in nodes) {
										this.CreateConnectionAttempt(node);
										this.CheckShouldCancel();
									}

									secondsToWait = 60;
								} else {
									// ok, we still try to get more, but we can take it easy, we have a good minimum
									var nodes = availableNodes.Take(1);

									foreach(NodeAddressInfo node in nodes) {
										this.CreateConnectionAttempt(node);
										this.CheckShouldCancel();
									}

									secondsToWait = 20;
								}
							}
						}

						//ok, we processed them, they can go now
						this.explicitConnectionRequests.Clear();
					}

					//---------------------------------------------------------------
					// Phase 3: Do some cleaning if we have too many connections. remove nodes that dont really serve any purpose.

					// this is another scenario, if we have too many connections...
					if(activeConnectionsCount > GlobalSettings.ApplicationSettings.maxPeerCount) {
						this.CheckShouldCancel();

						int amountToRemove = activeConnectionsCount - GlobalSettings.ApplicationSettings.maxPeerCount;
						var activeConnectionsCopy = activeConnections.Where(c => !c.Locked).ToList();

						activeConnectionsCopy.Shuffle();

						// Disconnect and remove the peers, bye bye...
						//TODO: once we have statistics about peers, then use heuristics to remove the less favorable ones...
						this.DisconnectPeers(activeConnectionsCopy.Take(amountToRemove));
					}

					//first thing, lets remove nodes that support chains that we dont, if we dont need more null chain nodes
					var nullChainConnections = activeConnections.Where(c => c.NoSupportedChains && !c.Locked).ToList();

					if(nullChainConnections.Any()) {
						decimal percenNullChains = (decimal) nullChainConnections.Count / Math.Max(activeConnectionsCount, 1);

						if((percenNullChains > MaxNullChainNodesPercent) && (nullChainConnections.Count > minAcceptableNullChainCount)) {
							this.CheckShouldCancel();

							// we have too much, lets remove some. first, find how many we have too much
							int bestMaximum = (int) (nullChainConnections.Count * MaxNullChainNodesPercent);

							int connectionsToRemove = Math.Max(nullChainConnections.Count - bestMaximum - minAcceptableNullChainCount, 0);

							if(connectionsToRemove > 0) {
								nullChainConnections.Shuffle();
								var ignoreList = new List<NodeAddressInfo>();

								// remove the peers
								this.DisconnectPeers(nullChainConnections.Take(connectionsToRemove), info => {
									ignoreList.Add(ConnectionStore<R>.GetEndpointInfoNode(info));
								});

								this.connectionStore.AddIgnorePeerNodes(ignoreList);
							}
						}
					}

					this.ProcessLoopActions();

					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextAction = DateTime.Now.AddSeconds(secondsToWait);
				}
			} catch(OperationCanceledException) {
				throw;
			} catch(Exception ex) {
				Log.Error(ex, "Failed to process connections");
				this.nextAction = DateTime.Now.AddSeconds(10);
			}
		}

		protected virtual void ContactHubs() {
			if(GlobalSettings.ApplicationSettings.EnableHubs && (this.nextHubContact < DateTime.Now)) {
				try {
					NodeAddressInfoList entries = this.connectionStore.GetHubNodes();

					if((entries != null) && entries.Nodes.Any()) {
						this.CreateConnectionAttempt(entries.Nodes.First());

						this.nextHubContact = DateTime.Now.AddMinutes(3);
					}
				} catch(Exception ex) {
					Log.Error(ex, "Failed to query neuralium hubs.");

					throw;
				}
			}
		}

		protected virtual void ProcessLoopActions() {

		}

		protected virtual List<NodeAddressInfo> GetAvailableNodesList(List<PeerConnection> activeConnections, bool onlyConnectables = true) {

			var availableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, onlyConnectables);

			// lets get a list of connected IPs
			var connectedIps = activeConnections.Select(c => c.ScopedIp).ToList();

			// lets make sure we remove the ones we are already connected to.
			availableNodes = availableNodes.Where(an => !connectedIps.Contains(an.ScopedIp)).ToList();

			if((availableNodes.Count == 0) && this.ShouldAct(ref this.nextUpdateNodeCountAction)) {
				// thats bad, we have no more available nodes to connect to. might have emptied our list. 
				if(this.connectionStore.ActiveConnectionsCount == 0) {
					// no choice, lets reload from our static sources
					this.connectionStore.FreeSomeIgnorePeers();

					this.networkingService.ConnectionStore.LoadStaticStartNodes();
				} else {
					// ok, we have some peers, lets request their lists
					// first, lets reload the ones they already provided us

					var startNodes = new Dictionary<Enums.PeerTypes, NodeAddressInfoList>();

					foreach(PeerConnection conn in activeConnections) {

						foreach(var entry in conn.PeerNodes) {
							if(!startNodes.ContainsKey(entry.Key)) {
								startNodes.Add(entry.Key, new NodeAddressInfoList(entry.Key));
							}

							startNodes[entry.Key].AddNodes(entry.Value.Nodes);
						}
					}

					// make sure we remove ourselves otherwise it gives false positives
					foreach(var entry in startNodes) {

						entry.Value.SetNodes(entry.Value.Nodes.Distinct().Where(n => !this.connectionStore.IsOurAddress(n)));
					}

					if(startNodes.Count == 0) {
						// no choice, lets query new lists
						this.RequestPeerLists();
					} else {

						this.connectionStore.AddAvailablePeerNodes(startNodes, false);
					}
				}

				// lets take a while before we attempt this again
				this.nextUpdateNodeCountAction = DateTime.Now.AddSeconds(60);
			}

			// get the list of connection attemps that are really too fresh to contact again. also, 
			var tooFreshConnections = this.peerActivityInfo.Values.Where(a => ((a.lastConnectionSetAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT)) > DateTime.Now) || ((a.lastConnectionAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT)) > DateTime.Now)).Select(a => a.ScopedIp).ToList();

			this.CheckShouldCancel();

			// now filter to keep only the ones that are contactable
			availableNodes = availableNodes.Where(n => !tooFreshConnections.Contains(n.ScopedIp)).ToList();

			if(!availableNodes.Any() && onlyConnectables) {
				// we got nothing, lets try all connections
				availableNodes = this.GetAvailableNodesList(activeConnections, false);
			} else {
				// mix up the list to ensure a certain randomness from times to times

				availableNodes.Shuffle();
			}

			return availableNodes;
		}

		/// <summary>
		///     THis method will disconnect and remove a set of peers if we have to many
		/// </summary>
		/// <param name="removables"></param>
		/// <param name="loopAction"></param>
		private void DisconnectPeers(IEnumerable<PeerConnection> removables, Action<PeerConnection> loopAction = null) {
			foreach(PeerConnection info in removables) {
				this.CheckShouldCancel();

				// thats it, we say bye bye to this connection
				Log.Verbose($"Removing null chain peer {ConnectionStore<R>.GetEndpointInfoNode(info).ScopedAdjustedIp} because we have too many.");

				// lets remove it from the list
				this.connectionStore.RemoveConnection(info);

				try {
					info.connection.Dispose();
				} catch(Exception ex) {
					Log.Error(ex, "Failed to close connection");
				}

				// run custom actions
				loopAction?.Invoke(info);
			}
		}

		protected override void Initialize() {

			if(!NetworkInterface.GetIsNetworkAvailable() && !GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.localhostOnly) {
				throw new NetworkInformationException();
			}

			this.networkingService.ConnectionStore.LoadStaticStartNodes();

			base.Initialize();
		}

		/// <summary>
		///     Check if we received any tasks and process them
		/// </summary>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns></returns>
		protected List<Guid> CheckTasks() {

			var tasks = this.coloredTaskReceiver.CheckTasks();

			tasks.AddRange(this.RoutedTaskReceiver.CheckTasks(() => {
				// check this every loop, for responsiveness
				this.CheckShouldCancel();
			}));

			return tasks;
		}

		protected override void DisposeAll(bool disposing) {

			if(disposing) {

			}

		}

		private struct PeerJoinedInfo {
			public ConnectionManagerActivityInfo ConnectionManagerActivityInfo;
			public PeerConnection PeerConnection;
		}

		/// <summary>
		///     Store various information about our peers so we play nice with them
		/// </summary>
		private class ConnectionManagerActivityInfo {

			/// <summary>
			///     how many times did we try to connect?
			/// </summary>
			public int connectionAttemptCounter;

			/// <summary>
			///     how many times did we do the full try 3 times cycle
			/// </summary>
			public int connectionSetAttemptCounter;

			public bool inProcess;

			public DateTime lastConnectionAttempt = DateTime.MinValue;

			/// <summary>
			///     when did we last try to run the full try 3 times set
			/// </summary>
			public readonly DateTime lastConnectionSetAttempt = DateTime.MinValue;

			public DateTime lastPeerListRequestAttempt = DateTime.MinValue;

			/// <summary>
			///     how many times have we connected to this peer successfully before
			/// </summary>
			public int successfullConnectionCounter;

			public ConnectionManagerActivityInfo(NodeActivityInfo nodeActivityInfo) {
				this.NodeActivityInfo = nodeActivityInfo;
			}

			public string ScopedIp => this.NodeActivityInfo?.Node?.ScopedIp;

			public NodeActivityInfo NodeActivityInfo { get; }
		}
	}

	public static class ConnectionsManager {
		/// <summary>
		///     a task to request more connections
		/// </summary>
		public class RequestMoreConnectionsTask : ColoredTask {
			/// <summary>
			///     if we must add new connections and already have too much. these connections will be considered expendable and may
			///     be disconnected to make room for more
			/// </summary>
			public List<PeerConnection> ExpendableConnections = new List<PeerConnection>();

			public RequestMoreConnectionsTask() {

			}

			public RequestMoreConnectionsTask(List<PeerConnection> expendableConnections) {
				this.ExpendableConnections.AddRange(expendableConnections);
			}
		}
	}
}