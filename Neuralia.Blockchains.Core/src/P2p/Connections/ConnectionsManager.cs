using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using RestSharp;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionsManager : ISimpleRoutedTaskHandler, IColoredRoutedTaskHandler, ILoopThread {
		IIPCrawler Crawler { get; }
	}

	public interface IConnectionsManager<R> : ILoopThread<ConnectionsManager<R>>, IConnectionsManager
		where R : IRehydrationFactory {
	}

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class ConnectionsManager<R> : LoopThread<ConnectionsManager<R>>, IConnectionsManager<R>
		where R : IRehydrationFactory {
		private const decimal LOW_CONNECTION_PCT = 0.4M;

		private const decimal CRITICAL_LOW_CONNECTION_PCT = 0.2M;

		private const decimal MaxNullChainNodesPercent = 0.1M; // maximum of 10% null chain nodes.  mode than that, we will disconnect.

		private const int MaxConnectionAttemptCount = 3; // maximum of times we try a peer
		private const int minAcceptableNullChainCount = 1; // maximum of times we try a peer

		private const int MAX_SECONDS_BEFORE_NEXT_PEER_LIST_REQUEST = 3 * 60;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT = 3 * 60;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT = 3 * 60;

		// these are limited wait times, when we are in times of need
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT_LIMITED = 10;
		private const int MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT_LIMITED = 10;

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

		private DateTime nextHubContact = DateTimeEx.MinValue;
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

		protected virtual Task HandleColoredTask(IColoredTask task) {
			if(task is ConnectionsManager.RequestMoreConnectionsTask requestConnectionsTask) {
				// ok, sombody requested more connections. lets do it!
				this.explicitConnectionRequests.Add(requestConnectionsTask);

				// lets act now!
				this.nextAction = DateTimeEx.CurrentTime;
			}

			return Task.CompletedTask;
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
		protected async Task RequestPeerLists() {
			// join together the peer connection we have with the peer activity connection. this way we can correlate and make an educated decision
			IEnumerable<PeerJoinedInfo> matchedConnections = this.GetJoinedPeerInfos();

			// now we choose the ones that were either never contacted (probably received a connection from them) or the ones we contacted long ago, so we can bother them again
			IEnumerable<PeerJoinedInfo> availablePeers = matchedConnections.Where(m => {
				return (m.ConnectionManagerActivityInfo != null) && ((DateTimeEx.CurrentTime - m.ConnectionManagerActivityInfo.lastPeerListRequestAttempt) > TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_PEER_LIST_REQUEST));
			});

			foreach(PeerJoinedInfo peer in availablePeers) {
				// thats it, we can query this peer and ask for his/her peer list

				// ok, contact the peer and ask for their connection

				// yup, we are asking for it, so lets update our records
				peer.ConnectionManagerActivityInfo.lastPeerListRequestAttempt = DateTimeEx.CurrentTime; // set it just in case

				// ask for the peers!
				//TODO: create workflow here
				try {
					NLog.Default.Verbose($"attempting to query peer list from peer {peer.PeerConnection.ScopedAdjustedIp}");

					IClientPeerListRequestWorkflow<R> peerListRequest = this.clientWorkflowFactory.CreatePeerListRequest(peer.PeerConnection);

					if(peerListRequest != null) {
						peerListRequest.Completed += (success, wf) => {
							// run this task in the connection manager thread by sending a delegated task
							SimpleTask task = new SimpleTask();

							task.Action += sender => {
								peer.ConnectionManagerActivityInfo.lastPeerListRequestAttempt = DateTimeEx.CurrentTime; // update it
							};

							this.ReceiveTask(task);

							return Task.CompletedTask;
						};

						await this.networkingService.WorkflowCoordinator.AddWorkflow(peerListRequest).ConfigureAwait(false);
					} else {
						peer.ConnectionManagerActivityInfo.lastPeerListRequestAttempt = DateTimeEx.CurrentTime; // update it
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "failed to query peer list");
				}
			}
		}

		protected async Task CreateConnectionAttempt(NodeAddressInfo node) {
			if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
				// its paused, we dont do anything, just return
				return;
			}

			ConnectionManagerActivityInfo connectionManagerActivityInfo = null;

			NodeActivityInfo nodeActivityInfo = this.connectionStore.GetNodeActivityInfo(node);

			if(nodeActivityInfo == null) {
				nodeActivityInfo = new NodeActivityInfo(node, true);
			}

			if(this.peerActivityInfo.ContainsKey(node.ScopedIp)) {
				connectionManagerActivityInfo = this.peerActivityInfo[node.ScopedIp];
			} else {
				connectionManagerActivityInfo = new ConnectionManagerActivityInfo(nodeActivityInfo);

				connectionManagerActivityInfo.lastConnectionAttempt = DateTimeEx.MinValue;
				connectionManagerActivityInfo.lastPeerListRequestAttempt = DateTimeEx.CurrentTime; // since we get the list during the handshake

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

			connectionManagerActivityInfo.lastConnectionAttempt = DateTimeEx.CurrentTime; // set it just in case
			connectionManagerActivityInfo.connectionAttemptCounter++;
			connectionManagerActivityInfo.inProcess = true;

			// thats it, lets launch a connection

			try {
				NLog.Default.Verbose($"attempting connection attempt {connectionManagerActivityInfo.connectionAttemptCounter} to peer {node.ScopedAdjustedIp}");
				ClientHandshakeWorkflow<R> handshake = this.clientWorkflowFactory.CreateRequestHandshakeWorkflow(ConnectionStore<R>.CreateEndpoint(node));

				handshake.Error2 += (workflow, ex) => {
					// anything to do here?
					return Task.CompletedTask;
				};

				handshake.Completed2 += async (success, wf) => {
					// run this task in the connection manager thread by sending a delegated task
					SimpleTask task = new SimpleTask();

					task.Action += sender => {
						connectionManagerActivityInfo.lastConnectionAttempt = DateTimeEx.CurrentTime; // update it
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
									NLog.Default.Verbose($"Reached max connection attempt for peer {node.ScopedAdjustedIp}. this peer will now be ignored form now on.");

									this.connectionStore.AddIgnorePeerNode(nodeActivityInfo);

									// remove it all, its over
									if(this.peerActivityInfo.ContainsKey(node.ScopedIp)) {
										this.peerActivityInfo.Remove(node.ScopedIp);
									}
								} else {
									NLog.Default.Verbose($"Reached max connection attempt for peer {node.ScopedAdjustedIp}. will retry later");
								}
							} else {
								NLog.Default.Verbose($"Failed to connect to peer {node.ScopedAdjustedIp}. Attempt {connectionManagerActivityInfo.connectionAttemptCounter} of {PEER_CONNECTION_ATTEMPT_COUNT}");
							}
						}
					};

					this.ReceiveTask(task);
				};

				await this.networkingService.WorkflowCoordinator.AddWorkflow(handshake).ConfigureAwait(false);
			} catch(Exception ex) {
				NLog.Default.Error(ex, "failed to create handshake");
			}
		}

		protected override async Task ProcessLoop(LockContext lockContext) {
			try {
				this.CheckShouldCancel();

				// first thing, lets check if we have any tasks received to process
				await this.CheckTasks().ConfigureAwait(false);

				this.CheckShouldCancel();

				if(this.ShouldAct(ref this.nextAction)) {
					// ok, its time to act
					int secondsToWait = 3; // default next action time in seconds. we can play on this

					if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
						// its paused, we dont do anything, just return
						this.nextAction = DateTimeEx.CurrentTime.AddSeconds(secondsToWait);

						return;
					}

					//-------------------------------------------------------------------------------
					// phase 1: Ensure we maintain our connection information up to date

					// first thing, lets detect new incoming connections that we may not have in our activity list (since we did not create thei connection) and give them and activity log
					IEnumerable<PeerJoinedInfo> newIncomingConnections = this.GetJoinedPeerInfos().Where(p => (p.ConnectionManagerActivityInfo == null) && (p.PeerConnection.direction == PeerConnection.Directions.Incoming));

					foreach(PeerJoinedInfo newIncomingConnection in newIncomingConnections) {
						ConnectionManagerActivityInfo connectionManagerActivityInfo = new ConnectionManagerActivityInfo(newIncomingConnection.PeerConnection.NodeActivityInfo);

						// we assume we just talked to them through the handshake
						connectionManagerActivityInfo.lastConnectionAttempt = DateTimeEx.CurrentTime;
						connectionManagerActivityInfo.lastPeerListRequestAttempt = DateTimeEx.CurrentTime;

						// and add them to our list
						this.peerActivityInfo.Add(connectionManagerActivityInfo.ScopedIp, connectionManagerActivityInfo);
					}

					this.CheckShouldCancel();

					// now get this number every time, since we gain new connections from the server all the time, and others just disconnect
					int activeConnectionsCount = this.connectionStore.ActiveConnectionsCount;

					// the list of active connections we will act on
					List<PeerConnection> activeConnections = this.connectionStore.AllConnectionsList;

					//-------------------------------------------------------------------------------
					// phase 2: search for more connections if we dont have enough

					decimal averagePerCount = GlobalSettings.ApplicationSettings.AveragePeerCount;

					int criticalLowConnectionLevel = (int) Math.Ceiling(averagePerCount * CRITICAL_LOW_CONNECTION_PCT);
					int lowConnectionLevel = (int) Math.Ceiling(averagePerCount * LOW_CONNECTION_PCT);

					if((activeConnectionsCount < GlobalSettings.ApplicationSettings.MaxPeerCount) || this.explicitConnectionRequests.Any()) {
						// ok, in here, we will need to get more connections
						this.CheckShouldCancel();

						// get the list of IPs that we can connect to
						List<NodeAddressInfo> availableNodes = await this.GetAvailableNodesList(activeConnections).ConfigureAwait(false);

						if(this.explicitConnectionRequests.Any() && (activeConnectionsCount >= Math.Max(GlobalSettings.ApplicationSettings.MaxPeerCount - 2, 0))) {
							// ok, no choice, we will have to cut loose some connections to make room for now ones
							List<PeerConnection> expendableConnections = this.explicitConnectionRequests.SelectMany(c => c.ExpendableConnections).Distinct().ToList();

							// ok, lets disconnect these guys...
							await this.DisconnectPeers(expendableConnections).ConfigureAwait(false);

							// refresh our current count now that we cleared some
							activeConnectionsCount = this.connectionStore.ActiveConnectionsCount;
						}

						this.CheckShouldCancel();

						if(!availableNodes.Any()) {
							// ok, we really have NO connections. let's contact a HUB and request more peers

							await this.ContactHubs().ConfigureAwait(false);

							secondsToWait = 30;
						} else {
							if((activeConnectionsCount < criticalLowConnectionLevel) || this.explicitConnectionRequests.Any()) {
								// ok, in this case its an urgent situation, lets try to connect aggressively

								availableNodes.Shuffle();
								IEnumerable<NodeAddressInfo> nodes = availableNodes.Take(3);

								foreach(NodeAddressInfo node in nodes) {
									await this.CreateConnectionAttempt(node).ConfigureAwait(false);
									this.CheckShouldCancel();
								}
							} else if(activeConnectionsCount < lowConnectionLevel) {
								// still serious, but we can rest a bit
								IEnumerable<NodeAddressInfo> nodes = availableNodes.Take(2);

								foreach(NodeAddressInfo nodeAddressInfo in nodes) {
									await this.CreateConnectionAttempt(nodeAddressInfo).ConfigureAwait(false);
									this.CheckShouldCancel();
								}

								secondsToWait = 10;
							} else {
								if(activeConnectionsCount < GlobalSettings.ApplicationSettings.AveragePeerCount) {
									// ok, we still try to get more, but we can take it easy, we have a good average
									IEnumerable<NodeAddressInfo> nodes = availableNodes.Take(1);

									foreach(NodeAddressInfo node in nodes) {
										await this.CreateConnectionAttempt(node).ConfigureAwait(false);
										this.CheckShouldCancel();
									}

									secondsToWait = 60;
								} else {
									// ok, we still try to get more, but we can take it easy, we have a good minimum
									IEnumerable<NodeAddressInfo> nodes = availableNodes.Take(1);

									foreach(NodeAddressInfo node in nodes) {
										await this.CreateConnectionAttempt(node).ConfigureAwait(false);
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
					if(activeConnectionsCount > GlobalSettings.ApplicationSettings.MaxPeerCount) {
						this.CheckShouldCancel();

						int amountToRemove = activeConnectionsCount - GlobalSettings.ApplicationSettings.MaxPeerCount;
						List<PeerConnection> activeConnectionsCopy = activeConnections.Where(c => !c.Locked).ToList();

						activeConnectionsCopy.Shuffle();

						// Disconnect and remove the peers, bye bye...
						//TODO: once we have statistics about peers, then use heuristics to remove the less favorable ones...
						await this.DisconnectPeers(activeConnectionsCopy.Take(amountToRemove)).ConfigureAwait(false);
					}

					//first thing, lets remove nodes that support chains that we dont, if we dont need more null chain nodes
					List<PeerConnection> nullChainConnections = activeConnections.Where(c => c.NoSupportedChains && !c.Locked).ToList();

					if(nullChainConnections.Any()) {
						decimal percenNullChains = (decimal) nullChainConnections.Count / Math.Max(activeConnectionsCount, 1);

						if((percenNullChains > MaxNullChainNodesPercent) && (nullChainConnections.Count > minAcceptableNullChainCount)) {
							this.CheckShouldCancel();

							// we have too much, lets remove some. first, find how many we have too much
							int bestMaximum = (int) (nullChainConnections.Count * MaxNullChainNodesPercent);

							int connectionsToRemove = Math.Max(nullChainConnections.Count - bestMaximum - minAcceptableNullChainCount, 0);

							if(connectionsToRemove > 0) {
								nullChainConnections.Shuffle();
								List<NodeAddressInfo> ignoreList = new List<NodeAddressInfo>();

								// remove the peers
								await this.DisconnectPeers(nullChainConnections.Take(connectionsToRemove), info => {
									ignoreList.Add(ConnectionStore<R>.GetEndpointInfoNode(info));
								}).ConfigureAwait(false);

								this.connectionStore.AddIgnorePeerNodes(ignoreList);
							}
						}
					}

					this.ProcessLoopActions();

					//---------------------------------------------------------------
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextAction = DateTimeEx.CurrentTime.AddSeconds(secondsToWait);
				}
			} catch(OperationCanceledException) {
				throw;
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to process connections");
				this.nextAction = DateTimeEx.CurrentTime.AddSeconds(10);
			}
		}

		protected virtual async Task ContactHubs() {
			if(GlobalSettings.ApplicationSettings.EnableHubs && (this.nextHubContact < DateTimeEx.CurrentTime)) {
				bool useWeb = GlobalSettings.ApplicationSettings.HubContactMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
				bool useGossip = GlobalSettings.ApplicationSettings.HubContactMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
				bool sent = false;

				if(useWeb) {
					try {
						if(await this.ContactHubsWeb().ConfigureAwait(false)) {
							sent = true;
						}
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to contact hubs through web");

						// do nothing, we will sent it on chain
						sent = false;
					}
				}

				if(!sent || useGossip) {
					await this.ContactHubsGossip().ConfigureAwait(false);
					sent = true;
				}

				if(!sent) {
					throw new ApplicationException("Failed to send contact hubs");
				}
			}
		}

		protected Task<bool> ContactHubsWeb() {
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			return Repeater.RepeatAsync(async () => {
				Dictionary<string, object> parameters = new Dictionary<string, object>();

				using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {
					List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, true, true, false);
					NodeAddressInfoList nodeAddressInfoList = new NodeAddressInfoList(currentAvailableNodes);

					GlobalSettings.Instance.NodeInfo.Dehydrate(dehydrator);
					nodeAddressInfoList.Dehydrate(dehydrator);

					SafeArrayHandle bytes = dehydrator.ToArray();
					parameters.Add("data", bytes.Entry.ToBase64());
					bytes.Return();
				}

				IRestResponse result = await restUtility.Post(GlobalSettings.ApplicationSettings.HubsWebAddress, "hub/query", parameters).ConfigureAwait(false);

				// ok, check the result
				if(result.StatusCode == HttpStatusCode.OK) {
					// ok, all good

					using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(ByteArray.FromBase64(result.Content))) {
						NodeAddressInfoList infoList = new NodeAddressInfoList();
						
						Guid ip = rehydrator.ReadGuid();
						var ipAddress = IPUtils.GuidToIP(ip);
						
						this.connectionStore.AddPeerReportedPublicIp(ipAddress, ConnectionStore.PublicIpSource.Hub);
						
						infoList.Rehydrate(rehydrator);

						NLog.Default.Verbose($"adding {infoList.Nodes.Count} connections received from hubs");
						this.connectionStore.AddAvailablePeerNodes(infoList, true);
					}

					this.nextHubContact = DateTimeEx.CurrentTime.AddMinutes(3);

					return true;
				}

				throw new ApplicationException("Failed to query web hubs");
			});
		}

		protected async Task ContactHubsGossip() {
			try {
				NodeAddressInfoList entries = this.connectionStore.GetHubNodes();

				if((entries != null) && entries.Nodes.Any()) {
					await this.CreateConnectionAttempt(entries.Nodes.First()).ConfigureAwait(false);

					this.nextHubContact = DateTimeEx.CurrentTime.AddMinutes(3);
				}
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to query neuralium hubs.");

				throw;
			}
		}

		protected virtual void ProcessLoopActions() {
		}

		protected virtual async Task<List<NodeAddressInfo>> GetAvailableNodesList(List<PeerConnection> activeConnections, bool onlyConnectables = true) {
			List<NodeAddressInfo> GetAvailableNodes() {
				List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, onlyConnectables);

				// lets get a list of connected IPs
				List<string> connectedIps = activeConnections.Select(c => c.ScopedIp).ToList();

				// lets make sure we remove the ones we are already connected to.
				return currentAvailableNodes.Where(an => !connectedIps.Contains(an.ScopedIp)).ToList();
			}

			List<NodeAddressInfo> availableNodes = GetAvailableNodes();

			if(!availableNodes.Any() && this.ShouldAct(ref this.nextUpdateNodeCountAction)) {
				// thats bad, we have no more available nodes to connect to. might have emptied our list. 
				if(this.connectionStore.ActiveConnectionsCount == 0) {
					// no choice, lets reload from our static sources
					this.connectionStore.FreeSomeIgnorePeers();

					this.networkingService.ConnectionStore.LoadStaticStartNodes();
				} else {
					// ok, we have some peers, lets request their lists
					// first, lets reload the ones they already provided us

					NodeAddressInfoList startNodes = new NodeAddressInfoList();

					foreach(PeerConnection conn in activeConnections) {
						startNodes.AddNodes(conn.PeerNodes.Nodes);
					}

					// make sure we remove ourselves otherwise it gives false positives
					NodeAddressInfoList filteredNodes = new NodeAddressInfoList();
					filteredNodes.SetNodes(startNodes.Nodes.Distinct().Where(n => !this.connectionStore.IsOurAddress(n)));

					if(filteredNodes.Empty) {
						// no choice, lets query new lists
						await this.RequestPeerLists().ConfigureAwait(false);
					} else {
						this.connectionStore.AddAvailablePeerNodes(filteredNodes, false);
					}
				}

				availableNodes = GetAvailableNodes();

				// lets take a while before we attempt this again
				if(!availableNodes.Any()) {
					this.nextUpdateNodeCountAction = DateTimeEx.CurrentTime.AddSeconds(60);

					return availableNodes;
				}
			}

			// get the list of connection attemps that are really too fresh to contact again. also, 
			DateTime time = DateTimeEx.CurrentTime;
			List<string> tooFreshConnections = this.peerActivityInfo.Values.Where(a => ((a.lastConnectionSetAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT)) > time) || ((a.lastConnectionAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT)) > time)).Select(a => a.ScopedIp).ToList();

			this.CheckShouldCancel();

			// now filter to keep only the ones that are contactable
			List<NodeAddressInfo> reducedAvailableNodes = availableNodes.Where(n => !tooFreshConnections.Contains(n.ScopedIp)).ToList();

			if(!reducedAvailableNodes.Any()) {
				if(onlyConnectables) {
					// we got nothing, lets try all connections
					reducedAvailableNodes = await this.GetAvailableNodesList(activeConnections, false).ConfigureAwait(false);
				} else {
					// ok, this is bad, we tried them all and got nothing, we need to lower the limit
					tooFreshConnections = this.peerActivityInfo.Values.Where(a => ((a.lastConnectionSetAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_SET_ATTEMPT_LIMITED)) > time) || ((a.lastConnectionAttempt + TimeSpan.FromSeconds(MAX_SECONDS_BEFORE_NEXT_CONNECTION_ATTEMPT_LIMITED)) > time)).Select(a => a.ScopedIp).ToList();

					this.CheckShouldCancel();

					// now filter to keep only the ones that are contactable
					reducedAvailableNodes = availableNodes.Where(n => !tooFreshConnections.Contains(n.ScopedIp)).ToList();
				}
			}

			// pick nodes not already connecting
			reducedAvailableNodes = reducedAvailableNodes.Where(n => !this.peerActivityInfo.ContainsKey(n.ScopedIp) || !this.peerActivityInfo[n.ScopedIp].inProcess).ToList();

			// mix up the list to ensure a certain randomness from times to times
			reducedAvailableNodes.Shuffle();

			return reducedAvailableNodes;
		}

		/// <summary>
		///     THis method will disconnect and remove a set of peers if we have to many
		/// </summary>
		/// <param name="removables"></param>
		/// <param name="loopAction"></param>
		private async Task DisconnectPeers(IEnumerable<PeerConnection> removables, Action<PeerConnection> loopAction = null) {
			foreach(PeerConnection info in removables) {
				this.CheckShouldCancel();

				// thats it, we say bye bye to this connection
				NLog.Default.Verbose($"Removing null chain peer {ConnectionStore<R>.GetEndpointInfoNode(info).ScopedAdjustedIp} because we have too many.");

				// lets remove it from the list
				this.connectionStore.RemoveConnection(info);

				try {
					info.connection.Dispose();
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to close connection");
				}

				// run custom actions
				if(loopAction != null) {
					loopAction(info);
				}
			}
		}

		protected override Task Initialize(LockContext lockContext) {
			if(!this.connectionStore.GetIsNetworkAvailable && !GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.LocalhostOnly) {
				throw new NetworkInformationException();
			}

			this.networkingService.ConnectionStore.LoadStaticStartNodes();

			return base.Initialize(lockContext);
		}

		/// <summary>
		///     Check if we received any tasks and process them
		/// </summary>
		/// <param name="lockContext"></param>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns></returns>
		protected async Task<List<Guid>> CheckTasks() {
			List<Guid> tasks = await this.coloredTaskReceiver.CheckTasks().ConfigureAwait(false);

			tasks.AddRange(await this.RoutedTaskReceiver.CheckTasks(async () => {
				// check this every loop, for responsiveness
				this.CheckShouldCancel();
			}).ConfigureAwait(false));

			return tasks;
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
			///     when did we last try to run the full try 3 times set
			/// </summary>
			public readonly DateTime lastConnectionSetAttempt = DateTimeEx.MinValue;

			/// <summary>
			///     how many times did we try to connect?
			/// </summary>
			public int connectionAttemptCounter;

			/// <summary>
			///     how many times did we do the full try 3 times cycle
			/// </summary>
			public int connectionSetAttemptCounter;

			public bool inProcess;

			public DateTime lastConnectionAttempt = DateTimeEx.MinValue;

			public DateTime lastPeerListRequestAttempt = DateTimeEx.MinValue;

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

		public IIPCrawler Crawler => null;
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