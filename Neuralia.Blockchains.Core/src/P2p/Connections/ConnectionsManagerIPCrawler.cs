using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;


namespace Neuralia.Blockchains.Core.P2p.Connections {

	public interface IConnectionsManagerIPCrawler : IConnectionsProvider, ISimpleRoutedTaskHandler, IColoredRoutedTaskHandler, ILoopThread {
		IIPCrawler Crawler { get; }
		
	}
	public interface IConnectionsManagerIPCrawler<R> : ILoopThread<ConnectionsManagerIPCrawler<R>>, IConnectionsManagerIPCrawler
		where R : IRehydrationFactory {
	}
	
	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class ConnectionsManagerIPCrawler<R> : LoopThread<ConnectionsManagerIPCrawler<R>>, IConnectionsManagerIPCrawler<R>
		where R : IRehydrationFactory {

		private readonly IClientWorkflowFactory<R> clientWorkflowFactory;

		private readonly ColoredRoutedTaskReceiver coloredTaskReceiver;

		protected readonly IConnectionStore connectionStore;

		private readonly List<ConnectionsManager.RequestMoreConnectionsTask> explicitConnectionRequests = new List<ConnectionsManager.RequestMoreConnectionsTask>();

		private IPCrawler ipCrawler;

		public IIPCrawler Crawler {
			get {
				if(this.ipCrawler == null) {
					this.ipCrawler = this.CreateIPCrawler();
				}

				return this.ipCrawler;
			}
		}

		private readonly INetworkingService<R> networkingService;

		private readonly DateTime nextHubContact = DateTimeEx.MinValue;

		/// <summary>
		///     The receiver that allows us to act as a task endpoint mailbox
		/// </summary>
		private readonly SimpleRoutedTaskReceiver RoutedTaskReceiver;

		private ConcurrentDictionary<Task, string> ipCrawlerRequests = new ();

		private DateTime? nextAction;
		private DateTime? nextSyncProxiesAction;
		private DateTime? nextDiscoverLocalNodesAction;

		public ConnectionsManagerIPCrawler(ServiceSet<R> serviceSet) : base(Convert.ToInt32(1000 * GlobalSettings.ApplicationSettings.IPCrawlerProcessLoopPeriod)) {
			this.networkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();

			this.clientWorkflowFactory = serviceSet.InstantiationService.GetClientWorkflowFactory(serviceSet);

			this.connectionStore = this.networkingService.ConnectionStore;

			this.coloredTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleColoredTask);

			this.RoutedTaskReceiver = new SimpleRoutedTaskReceiver();

			this.RoutedTaskReceiver.TaskReceived += () => {
			};
			
			this.nextDiscoverLocalNodesAction = this.nextSyncProxiesAction = this.nextAction = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.IPCrawlerStartupDelay);

			this.ReceiveTask(new SimpleTask(async s => {
				await this.networkingService.ServiceSet.PortMappingService.DiscoverAndSetup().ConfigureAwait(false);
			}));

			this.connectionStore.IncomingPeerConnectionConfirmed += async connection => {
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} incoming connection detected: {connection.NodeAddressInfo}!");
				this.HandleNewConnection(connection);
			};

			this.connectionStore.NewAvailablePeerNode += async node => {
				if(this.connectionStore.IsNeuraliumHub(node)) {
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {node} is a hub, not adding as a peer!");

					return;
				}
				this.Crawler.HandleHubIPs(new List<NodeAddressInfo> {node}, DateTimeEx.CurrentTime);
			};

		}

		public bool IsPublicAndConnectable()
		{
			if (this.connectionStore.PublicIpMode == IPMode.Unknown)
				throw new Exception($"{nameof(IsPublicAndConnectable)} IPMode unknown!");
			
			return this.connectionStore.IsConnectable;
		}
		
		protected virtual IPCrawler CreateIPCrawler() {
			return new IPCrawler(GlobalSettings.ApplicationSettings.AveragePeerCount
				, GlobalSettings.ApplicationSettings.MaxPeerCount
				, GlobalSettings.ApplicationSettings.MaxMobilePeerCount
				, GlobalSettings.ApplicationSettings.MaxNonConnectablePeerCount
				, GlobalSettings.ApplicationSettings.LocalNodes.Select(n => new NodeAddressInfo(n.Ip, n.Port, NodeInfo.Full)).ToList()
				, GlobalSettings.ApplicationSettings.HubIPsRequestPeriod
				, GlobalSettings.ApplicationSettings.PeerIPsRequestPeriod
				, GlobalSettings.ApplicationSettings.PeerReconnectionPeriod
				, 24 * 60 * 60
				, GlobalSettings.ApplicationSettings.MaxConnectionRequestPerCrawl
				, GlobalSettings.ApplicationSettings.LocalNodesDiscoveryPorts
				, GlobalSettings.ApplicationSettings.LocalNodesDiscoveryTimeout
				, Convert.ToInt32(GlobalSettings.ApplicationSettings.LocalNodesDiscoveryMode)
				, GlobalSettings.ApplicationSettings.MaxPeerLatency
				, GlobalSettings.ApplicationSettings.IPCrawlerPrintNConnections
				, GlobalSettings.ApplicationSettings.MaxPendingConnectionRequests);
		}

		private void HandleNewConnection(PeerConnection connection) {
			this.Crawler.HandleLogin(connection.NodeAddressInfo, connection.ConnectionTime, connection.connection.ConnectionLatency, connection.connection.ConnectionLatencyStd);

			connection.Disposed += disposed => {
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} connection disposed detected: {disposed.NodeAddressInfo}!");
				var now = DateTimeEx.CurrentTime;

				this.ReceiveTask(new SimpleTask(s => {
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} HandleLogout: {disposed.NodeAddressInfo}");
					this.Crawler.HandleLogout(disposed.NodeAddressInfo, now);
				}));
			};

			if (!this.Crawler.CanAcceptNewConnection(connection.NodeAddressInfo)) // have we reached max peer count?
			{
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} CanAcceptNewConnection returned false for {connection.NodeAddressInfo}");
				
				this.RequestDisconnect(connection.NodeAddressInfo); // disconnect already!
			}
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

		public void RequestHubIPs() {
			NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestHubIPs)}.");

			this.ipCrawlerRequests.TryAdd(this.ContactHubs(), nameof(this.RequestHubIPs));
		}

		public void RequestPeerIPs(NodeAddressInfo node) {

			this.CheckShouldCancel();
			List<PeerConnection> activeConnections = this.connectionStore.AllConnectionsList;

			// lets get a list of connected IPs
			List<PeerConnection> connectedIps = activeConnections.Where(c => c.NodeAddressInfo.Equals(node)).ToList();

			NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)} from {node}, {connectedIps.Count} connected ips over {activeConnections.Count} connections");

			foreach(PeerConnection peer in connectedIps) {
				try {
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)}: attempting to query peer list from peer {peer.ScopedAdjustedIp}");

					IClientPeerListRequestWorkflow<R> peerListRequest = this.clientWorkflowFactory.CreatePeerListRequest(peer);

					if(peerListRequest != null) {
						peerListRequest.Completed += (success, wf) => {
							// run this task in the connection manager thread by sending a delegated task

							ImmutableList<NodeAddressInfo> nodes = peer.PeerNodes.Nodes;

							this.ReceiveTask(new SimpleTask(s => {
								NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)}: success={success}, {nodes.Count} ips returned.");

								if(success) {
									this.Crawler.HandlePeerIPs(node, nodes.Where(n => !this.connectionStore.IsOurAddress(n)).ToList(), DateTimeEx.CurrentTime);
								} else {
									this.Crawler.HandlePeerListRequestError(node, DateTimeEx.CurrentTime);
								}
							}));

							return Task.CompletedTask;
						};

						this.ipCrawlerRequests.TryAdd(this.networkingService.WorkflowCoordinator.AddWorkflow(peerListRequest), $"{nameof(this.RequestPeerIPs)}_{peer.NodeAddressInfo}");
					} else {
						List<NodeAddressInfo> nodes = new List<NodeAddressInfo>();
						this.Crawler.HandlePeerIPs(node, nodes, DateTimeEx.CurrentTime);
					}
				} catch(Exception ex) {
					NLog.IPCrawler.Error(ex, "failed to query peer list");
				}
			}

		}

		public void RequestConnect(NodeAddressInfo node) {
			NLog.IPCrawler.Information($"{IPCrawler.TAG} {nameof(this.RequestConnect)}: {node} ");

			ImmutableList<PeerConnection> shouldBeEmpty = this.connectionStore.AllConnectionsList.Where(peerConnection => peerConnection.NodeAddressInfo.Equals(node)).ToImmutableList();

			if(shouldBeEmpty.IsEmpty) {
				this.ipCrawlerRequests.TryAdd(this.CreateConnectionAttempt(node), $"{nameof(this.RequestConnect)}_{node}");
			} else {
				NLog.IPCrawler.Warning($"[Crawler] {nameof(this.RequestConnect)}: {node} already connected, calling HandleLogin(), this hints at a bug.");
				this.HandleNewConnection(shouldBeEmpty.Single());
			}
		}

		public void RequestDisconnect(NodeAddressInfo node) {
			NLog.IPCrawler.Information($"{IPCrawler.TAG} {nameof(this.RequestDisconnect)}: {node}.");

			// lets get a list of connected IPs
			List<PeerConnection> connectedNodes = this.connectionStore.AllConnectionsList.Where(c => c.NodeAddressInfo.Equals(node)).ToList();

			this.ipCrawlerRequests.TryAdd(this.DisconnectPeers(connectedNodes), $"{nameof(this.RequestDisconnect)}_{node}");

		}

		public Dictionary<BlockchainType, ChainSettings> ChainSettings => this.networkingService.ChainSettings;

		protected virtual Task HandleColoredTask(IColoredTask task) {
			if(task is ConnectionsManager.RequestMoreConnectionsTask requestConnectionsTask) {
				// ok, sombody requested more connections. lets do it!
				this.explicitConnectionRequests.Add(requestConnectionsTask);

				// lets act now!
				this.nextAction = DateTimeEx.CurrentTime;
			}

			return Task.CompletedTask;
		}

		protected async Task CreateConnectionAttempt(NodeAddressInfo node) {
			if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
				// its paused, we dont do anything, just return
				return;
			}
			this.CheckShouldCancel();
			// thats it, lets launch a connection

			try {
				ClientHandshakeWorkflow<R> handshake = this.clientWorkflowFactory.CreateRequestHandshakeWorkflow(ConnectionStore<R>.CreateEndpoint(node));

				handshake.Error2 += (workflow, ex) => {
					if(ex.GetBaseException() is ClientHandshakeWorkflow<R>.ClientHandshakeException clientEx) {

						switch(clientEx.Details) {
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.IsHub:
								NLog.IPCrawler.Verbose($"{IPCrawler.TAG} the {node} is a hub.");

								break;
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.CanGoNoFurther:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.BadHub:
								NLog.IPCrawler.Debug(ex, $"{IPCrawler.TAG} the {node} is a misbehaving hub.");

								break;
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.Duplicate:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.Loopback:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.AlreadyConnected:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.AlreadyConnecting:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ConnectionDropped:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.NoAnswer:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.TimeOutOfSync:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ConnectionError:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ConnectionsSaturated:
								// 'acceptable' reasons to fail TODO some might warrant warnings
								NLog.IPCrawler.Verbose($"{IPCrawler.TAG} CreateConnectionAttempt on {node} failed with 'acceptable' reason '{clientEx.Details}', not putting in quarantine.");

								break;

							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ClientHandshakeConfirmDropped:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ClientHandshakeConfirmFailed:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.ClientVersionRefused:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.InvalidNetworkId:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.InvalidPeer:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.Rejected:
							case ClientHandshakeWorkflow<R>.ClientHandshakeException.ExceptionDetails.Unknown:
								// unacceptable reasons to fail
								NLog.IPCrawler.Verbose($"{IPCrawler.TAG} CreateConnectionAttempt on {node} failed with a 'serious' reason '{clientEx.Details}', putting in quarantine.");

								if(IPMarshall.Instance.IsWhiteList(node.Address, out var acceptanceType))
									NLog.IPCrawler.Verbose($"{IPCrawler.TAG} not placing whitelisted node {node} with acceptance type '{acceptanceType}' in quarantine.");
								else
									IPMarshall.Instance.Quarantine(node.Address, IPMarshall.QuarantineReason.FailedHandshake, DateTimeEx.CurrentTime.AddMinutes(5), $"{IPCrawler.TAG}.{clientEx.Details}");

								break;

							default:
								NLog.IPCrawler.Debug($"{IPCrawler.TAG} CreateConnectionAttempt failed with unhandled reason '{clientEx.Details}', not putting in quarantine.");

								break;
						}

					}

					return Task.CompletedTask;
				};

				handshake.Completed2 += (success, wf) => {
					// run this task in the connection manager thread by sending a delegated task

					this.ReceiveTask(new SimpleTask(s => {
						var now = DateTimeEx.CurrentTime;

						try {
							if(success) {
								List<PeerConnection> peers = this.connectionStore.AllConnectionsList.Where(peer => peer.NodeAddressInfo.Equals(node)).ToList();

								if(peers.Any()) //should be exaclty 1
								{
									PeerConnection peer = peers.Single();

									this.HandleNewConnection(peers.Single());

									peer.connection.DataReceived += bytes => {
										//TODO: make sure this handler is properly unregistered on disconnect

										var nBytes = (uint) bytes.Length;

										this.ReceiveTask(new SimpleTask(s2 => {

//											NLog.IPCrawler.Debug($"{IPCrawler.TAG} {nameof(Crawler.HandleInput)}--{nBytes} bytes from node {node}");
											this.Crawler.HandleInput(node, DateTimeEx.CurrentTime, nBytes, peer.connection.ConnectionLatency, peer.connection.ConnectionLatencyStd);
										}));
										
										return Task.CompletedTask;
									};

									peer.connection.DataSent += bytes => {
										//TODO: make sure this handler is properly unregistered on disconnect

										var nBytes = (uint) bytes.Length;

										this.ReceiveTask(new SimpleTask(s2 => {
//											NLog.IPCrawler.Debug($"{IPCrawler.TAG} {nameof(Crawler.HandleOutput)}--{nBytes} bytes from node {node}");
											this.Crawler.HandleOutput(node, DateTimeEx.CurrentTime, nBytes, peer.connection.ConnectionLatency, peer.connection.ConnectionLatencyStd);
										}));
									};
								} else {
									NLog.IPCrawler.Verbose($"{IPCrawler.TAG} could not find node {node} within AllConnectionsList. Other side probably dropped the connection right after handshake.");
									this.Crawler.HandleTimeout(node, now);
								}
							} else //success == false
								this.Crawler.HandleTimeout(node, now);

						} catch(Exception ex) {
							//TODO: what to do here?
						}
					}));

					return Task.CompletedTask;
				};

				await this.networkingService.WorkflowCoordinator.AddWorkflow(handshake).ConfigureAwait(false);
			} catch(Exception ex) {
				NLog.IPCrawler.Debug(ex, $"{IPCrawler.TAG} failed to complete handshake with node {node}");
			}
		}

		protected virtual List<NodeAddressInfo> GetFilteredNodes() {
			return new List<NodeAddressInfo>();
		}

		protected virtual async Task<List<NodeAddressInfo>> SyncProxies() {
			//NOP
			return new List<NodeAddressInfo>();
		}

		protected override Task DisposeAllAsync() {
			return base.DisposeAllAsync();
		}


		protected override async Task ProcessLoop(LockContext lockContext) {
			try {

				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ProcessLoop)}, acting next at {this.nextAction}.");

				
				this.CheckShouldCancel();
				// first thing, lets check if we have any tasks received to process
				await this.CheckTasks().ConfigureAwait(false);
				
				this.CheckShouldCancel();

				if(this.ShouldAct(ref this.nextSyncProxiesAction)) {
					this.Crawler.QueueDynamicBlacklist(await this.SyncProxies().ConfigureAwait(false));
					this.nextSyncProxiesAction = DateTimeEx.CurrentTime.AddSeconds(60); //FIXME: AppSettings parameter?
				}
				

				if(!this.ShouldAct(ref this.nextAction))
					return;
				
				if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} networking status is paused.");
					// its paused, we dont do anything, just return
					this.nextAction = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.IPCrawlerCrawlPeriod);

					return;
				}

				//JD asked me to call this every loop for the need of child classes
				this.connectionStore.LoadStaticStartNodes();

				// Synchronize with ConnectionStore. needs to be called
				List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, true);
				this.ipCrawler.CombineIPs(currentAvailableNodes);

				// FIXME: this whole synchronization idea is a workaround while we add proper new connection/disconnection signals
				List<PeerConnection> currentConnectedNodes = this.connectionStore.AllConnections.Select(pair => pair.Value).ToList();

				int correctionsMade = this.Crawler.SyncConnections(currentConnectedNodes, DateTimeEx.CurrentTime); //won't give the real disconnection time, not very important anyway.

				if(correctionsMade > 0)
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} SyncConnections: {correctionsMade} corrections made");

				//at this moment, we know the ip crawler's nodes are in synch with connectionStore's nodes, now is a good time to filter them out
				this.Crawler.SyncFilteredNodes(this.GetFilteredNodes(), this);

				await this.Crawler.Crawl(this, DateTimeEx.CurrentTime).ConfigureAwait(false);

				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {this.ipCrawlerRequests.Count} pending tasks: ");

				foreach((Task task, string name) in this.ipCrawlerRequests)
				{
					switch (task.Status)
					{
						case TaskStatus.Canceled:
							NLog.IPCrawler.Verbose($"{IPCrawler.TAG} Task {name} Canceled.");
							this.ipCrawlerRequests.TryRemove(task, out _);
							break;
						case TaskStatus.Faulted:
							NLog.IPCrawler.Error($"{IPCrawler.TAG} Task {name} Faulted.");
							this.ipCrawlerRequests.TryRemove(task, out _);
							break;
						case TaskStatus.RanToCompletion:
							NLog.IPCrawler.Verbose($"{IPCrawler.TAG} Task {name} Completed.");
							this.ipCrawlerRequests.TryRemove(task, out _);
							break;
						case TaskStatus.Created:
							break;
						case TaskStatus.Running:
							break;
						case TaskStatus.WaitingForActivation:
							break;
						case TaskStatus.WaitingForChildrenToComplete:
							break;
						case TaskStatus.WaitingToRun:
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
				
				this.ProcessLoopActions();
				//-
				// done, lets sleep for a while

				// lets act again in X seconds
				this.nextAction = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.IPCrawlerCrawlPeriod);
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} scheduling next action at {this.nextAction}, now awaiting {this.ipCrawlerRequests.Count} remaining tasks.");


				foreach ((Task task, string name) in this.ipCrawlerRequests){
					NLog.IPCrawler.Verbose($"Incomplete Task {name} has status {task.Status}, awaiting...");
					await task.TimeoutAfter(TimeSpan.FromSeconds(0.5 * GlobalSettings.ApplicationSettings.IPCrawlerCrawlPeriod)).ConfigureAwait(false);
					NLog.IPCrawler.Verbose($"Task {name} now has status {task.Status}.");
				}

			} catch(OperationCanceledException) {
				throw;
			} catch(Exception ex) {
				NLog.IPCrawler.Error(ex, "Failed to process connections");
				this.nextAction = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.IPCrawlerStartupDelay);
			}
		}

		protected virtual async Task ContactHubs() {
			this.CheckShouldCancel();
			if(GlobalSettings.ApplicationSettings.EnableHubs && (this.nextHubContact < DateTimeEx.CurrentTime)) {
				bool useWeb = GlobalSettings.ApplicationSettings.HubContactMethod.HasFlag(AppSettingsBase.ContactMethods.Web);
				bool useGossip = GlobalSettings.ApplicationSettings.HubContactMethod.HasFlag(AppSettingsBase.ContactMethods.Gossip);
				var sent = false;

				if(useWeb) {
					try {
						if(await this.ContactHubsWeb().ConfigureAwait(false)) {
							sent = true;
						}
					} catch(Exception ex) {
						NLog.IPCrawler.Warning(ex, "Failed to contact hubs through web.");

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

		protected async Task<bool> ContactHubsWeb() {
			this.CheckShouldCancel();
			var restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.FormData);

			var parameters = new Dictionary<string, object>();

			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {
				List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, true, false, false); //ipv6 nodes might seem non-connectable to some ipv4 nodes
				
				currentAvailableNodes.Shuffle();
				
				int Score(NodeAddressInfo nai, Enums.PeerTypes targetType)
				{
					int score = 0;
					if (nai.IsConnectable)
						score += 5;
					if (nai.IsPeerTypeKnown)
					{
						score += 5;
						if (nai.PeerInfo.PeerType == targetType)
							score += 5;
					}
					return score;
				}
				var nodeAddressInfoList = new NodeAddressInfoList(currentAvailableNodes.OrderByDescending(n => Score(n, Enums.PeerTypes.Mobile)).Take(1) //JD wants us to send a few mobile too
					.Concat(currentAvailableNodes.OrderByDescending(n => Score(n, Enums.PeerTypes.FullNode)).Take(9)));

				GlobalSettings.Instance.NodeInfo.Dehydrate(dehydrator);
				nodeAddressInfoList.Dehydrate(dehydrator);

				SafeArrayHandle bytes = dehydrator.ToArray();
				parameters.Add("data", bytes.Entry.ToBase64());
				bytes.Return();
			}

			parameters.Add("networkid", GlobalSettings.Instance.NetworkId);

			var restParameterSet = new RestUtility.RestParameterSet<NodeAddressInfoList>();
			restParameterSet.parameters = parameters;
			restParameterSet.transform = webResult => {
				using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(ByteArray.FromBase64(webResult));

				var infoList = new NodeAddressInfoList();
				Guid ip = rehydrator.ReadGuid();
				var ipAddress = IPUtils.GuidToIP(ip); 
				
				this.connectionStore.AddPeerReportedPublicIp(ipAddress, ConnectionStore.PublicIpSource.Hub);
				
				infoList.Rehydrate(rehydrator);

				return infoList;
			};
			
			(bool sent, NodeAddressInfoList infoListResult) = await restUtility.PerformSecurePost(GlobalSettings.ApplicationSettings.HubsWebAddress, "hub/query", restParameterSet).ConfigureAwait(false);

			if(sent) {
				NLog.IPCrawler.Debug($"{IPCrawler.TAG} {nameof(this.Crawler.HandleHubIPs)} (Web): {infoListResult.Nodes.Count} ips returned");
				this.Crawler.HandleHubIPs(infoListResult.Nodes.Where(n => !this.connectionStore.IsOurAddress(n)).ToList(), DateTimeEx.CurrentTime);

				this.connectionStore.AddAvailablePeerNodes(infoListResult, true);
				
				return true;
			}
			
			throw new QueryHubsException("Failed to query web hubs (Web)");
		}

		protected async Task ContactHubsGossip() {
			this.CheckShouldCancel();
			try {
				NodeAddressInfoList infoList = this.connectionStore.GetHubNodes();

				//TODO: remove duplication with ContactHubsWeb
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ContactHubsGossip)}: {infoList.Nodes.Count} ips returned");

				foreach(var node in infoList.Nodes) {
					await this.CreateConnectionAttempt(node).ConfigureAwait(false);
				}

			} catch(Exception ex) {
				NLog.IPCrawler.Error(ex, "Failed to query neuralium hubs (Gossip).");

				throw;
			}
		}

		protected virtual void ProcessLoopActions() {
		}

		/// <summary>
		///     THis method will disconnect and remove a set of peers if we have to many
		/// </summary>
		/// <param name="removables"></param>
		/// <param name="loopAction"></param>
		private Task DisconnectPeers(IEnumerable<PeerConnection> removables, Action<PeerConnection> loopAction = null) {
			foreach(PeerConnection peer in removables) {

				// thats it, we say bye bye to this connection
				NLog.IPCrawler.Verbose($"Removing peer {ConnectionStore<R>.GetEndpointInfoNode(peer).ScopedAdjustedIp}.");

				// lets remove it from the list
				this.connectionStore.RemoveConnection(peer);

				try {
					peer.connection.Dispose();
				} catch(Exception ex) {
					NLog.IPCrawler.Verbose(ex, "Failed to close connection");
				}

				// run custom actions
				if(loopAction != null) {
					loopAction(peer);
				}
			}

			return Task.CompletedTask;
		}

		protected override Task Initialize(LockContext lockContext) {
			if(!NetworkInterface.GetIsNetworkAvailable() && !GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.LocalhostOnly) {
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

			tasks.AddRange(await this.RoutedTaskReceiver.CheckTasks(() => {
				// check this every loop, for responsiveness
				this.CheckShouldCancel();

				return Task.CompletedTask;
			}).ConfigureAwait(false));

			return tasks;
		}

		protected virtual Task<List<NodeAddressInfo>> GetAvailableNodesList(bool onlyConnectables = true) {
			List<PeerConnection> activeConnections = this.connectionStore.AllConnectionsList;

			List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, onlyConnectables);

			// lets get a list of connected IPs
			List<string> connectedIps = activeConnections.Select(c => c.ScopedIp).ToList();

			// lets make sure we remove the ones we are already connected to.
			return Task.FromResult(currentAvailableNodes.Where(an => !connectedIps.Contains(an.ScopedIp)).ToList());
		}
		

	}

	
}