using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using RestSharp;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {

	/// <summary>
	///     A special coordinator thread that is responsible for managing various aspects of the networking stack
	/// </summary>
	public class ConnectionsManagerIPCrawler<R> : LoopThread<ConnectionsManager<R>>, IConnectionsManager<R>, IConnectionsProvider
		where R : IRehydrationFactory {

		private readonly IClientWorkflowFactory<R> clientWorkflowFactory;

		private readonly ColoredRoutedTaskReceiver coloredTaskReceiver;

		private readonly IConnectionStore connectionStore;

		private readonly List<ConnectionsManager.RequestMoreConnectionsTask> explicitConnectionRequests = new List<ConnectionsManager.RequestMoreConnectionsTask>();

		private readonly IGlobalsService globalsService;

		private readonly IPCrawler ipCrawler;
		private readonly INetworkingService<R> networkingService;

		private readonly DateTime nextHubContact = DateTime.MinValue;

		/// <summary>
		///     The receiver that allows us to act as a task endpoint mailbox
		/// </summary>
		private readonly SimpleRoutedTaskReceiver RoutedTaskReceiver;

		private List<(string, Task)> ipCrawlerRequests = new List<(string, Task)>();

		private DateTime? nextAction;
		private DateTime? nextUpdateNodeCountAction;

		public ConnectionsManagerIPCrawler(ServiceSet<R> serviceSet) : base(10_000) {
			this.globalsService = serviceSet.GlobalsService;
			this.networkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();

			this.clientWorkflowFactory = serviceSet.InstantiationService.GetClientWorkflowFactory(serviceSet);

			this.connectionStore = this.networkingService.ConnectionStore;

			this.coloredTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleColoredTask);

			this.RoutedTaskReceiver = new SimpleRoutedTaskReceiver();

			this.RoutedTaskReceiver.TaskReceived += () => {
			};

			var translations = new List<NATRule>();

			foreach(var rule in GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.NATRules) {

				try {
					translations.Add(new NATRule(
						new NodeAddressInfo(rule.FromNode.Ip, rule.FromNode.Port, NodeInfo.Full)
						, IPAddress.Parse(rule.ToIP.Ip)
						, rule.IncrementIPWithPortDelta));
				} catch(Exception ex) {
					
					// lets just die silently if it fails
				}
			}
			
			List<IPAddress> blacklist = GlobalSettings.ApplicationSettings.Blacklist.Select(node => IPAddress.Parse(node.Ip)).ToList();

			this.ipCrawler = new IPCrawler(GlobalSettings.ApplicationSettings.AveragePeerCount, GlobalSettings.ApplicationSettings.MaxPeerCount, 
				1800.0, 600.0, 60.0, translations, blacklist);
			
			
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

			this.ipCrawlerRequests.Add((nameof(this.RequestHubIPs), this.ContactHubs()));
		}

		public void RequestPeerIPs(NodeAddressInfo node) {

			List<PeerConnection> activeConnections = this.connectionStore.AllConnectionsList;

			// lets get a list of connected IPs
			List<PeerConnection> connectedIps = activeConnections.Where(c => c.NodeAddressInfo.Equals(node)).ToList();

			NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)} from {node}, {connectedIps.Count} connected ips over {activeConnections.Count} connections");

			foreach(PeerConnection peer in connectedIps) {
				try {
					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)}: attempting to query peer list from peer {peer.ScoppedAdjustedIp}");

					ClientPeerListRequestWorkflow<R> peerListRequest = this.clientWorkflowFactory.CreatePeerListRequest(peer);

					peerListRequest.Completed += (success, wf) => {
						// run this task in the connection manager thread by sending a delegated task

						ImmutableList<NodeAddressInfo> nodes = peer.PeerNodes.Nodes;

						this.ReceiveTask(new SimpleTask(s => {
							NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.RequestPeerIPs)}: succes={success}, {nodes.Count} ips returned.");

							if(success) {
								this.ipCrawler.HandlePeerIPs(node, nodes.ToList(), DateTimeEx.CurrentTime);
							} else {
								this.ipCrawler.HandleTimeout(node, DateTimeEx.CurrentTime);
							}
						}));

						return Task.CompletedTask;
					};

					this.ipCrawlerRequests.Add((nameof(this.RequestPeerIPs), this.networkingService.WorkflowCoordinator.AddWorkflow(peerListRequest)));
				} catch(Exception ex) {
					NLog.IPCrawler.Error(ex, "failed to query peer list");
				}
			}

		}

		public void RequestConnect(NodeAddressInfo node) {
			NLog.IPCrawler.Information($"{IPCrawler.TAG} {nameof(this.RequestConnect)}: {node} ");

			ImmutableList<PeerConnection> shouldBeEmpty = this.connectionStore.AllConnectionsList.Where(peerConnection => peerConnection.NodeAddressInfo.Equals(node)).ToImmutableList();

			if(shouldBeEmpty.IsEmpty) {
				this.ipCrawlerRequests.Add((nameof(this.RequestConnect), this.CreateConnectionAttempt(node)));
			} else {
				NLog.IPCrawler.Warning($"[IpCrawler] {nameof(this.RequestConnect)}: {node} already connected, calling HandleLogin(), this hints at a bug.");

				this.ipCrawler.HandleLogin(node, DateTimeEx.CurrentTime);
			}
		}

		public void RequestDisconnect(NodeAddressInfo node) {
			NLog.IPCrawler.Information($"{IPCrawler.TAG} {nameof(this.RequestDisconnect)}: {node}.");

			// lets get a list of connected IPs
			List<PeerConnection> connectedNodes = this.connectionStore.AllConnectionsList.Where(c => c.NodeAddressInfo.Equals(node)).ToList();

			this.ipCrawlerRequests.Add((nameof(this.RequestDisconnect), this.DisconnectPeers(connectedNodes, peer => {
					                           // run this task in the connection manager thread by sending a delegated task

					                           this.ReceiveTask(new SimpleTask(s => {
						                           NLog.IPCrawler.Verbose($"{IPCrawler.TAG} HandleLogout: {node}");
						                           this.ipCrawler.HandleLogout(peer.NodeAddressInfo, DateTimeEx.CurrentTime);
					                           }));
				                           })));

		}

		public bool SupportsChain(BlockchainType blockchainType) {
			return this.networkingService.SupportsChain(blockchainType);
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

		protected async Task CreateConnectionAttempt(NodeAddressInfo node) {
			if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
				// its paused, we dont do anything, just return
				return;
			}

			// thats it, lets launch a connection

			try {
				ClientHandshakeWorkflow<R> handshake = this.clientWorkflowFactory.CreateRequestHandshakeWorkflow(ConnectionStore<R>.CreateEndpoint(node));

				handshake.Error2 += (workflow, ex) => {
					// anything to do here?
					NLog.IPCrawler.Debug(ex, $"{IPCrawler.TAG} failed to complete handshake with node {node}");

					return Task.CompletedTask;
				};

				handshake.Completed2 += (success, wf) => {
					// run this task in the connection manager thread by sending a delegated task

					this.ReceiveTask(new SimpleTask(s => {

						try {

							NLog.IPCrawler.Information($"{IPCrawler.TAG} Login to {node} result: {success}");

							if(success) {
								this.ipCrawler.HandleLogin(node, DateTimeEx.CurrentTime);
								List<PeerConnection> peers = this.connectionStore.AllConnectionsList.Where(peer => peer.NodeAddressInfo.Equals(node)).ToList();

								if(peers.Any()) //should be exaclty 1
								{
									PeerConnection peer = peers.Single();

									peer.connection.DataReceived += bytes => {
										//TODO: make sure this handler is properly unregistered on disconnect

										var nBytes = (uint) bytes.Length;

										this.ReceiveTask(new SimpleTask(s2 => {

//											NLog.IPCrawler.Debug($"{IPCrawler.TAG} {nameof(this.ipCrawler.HandleInput)}--{nBytes} bytes from node {node}");
											this.ipCrawler.HandleInput(node, DateTimeEx.CurrentTime, nBytes);
										}));
									};

									peer.connection.DataSent += bytes => {
										//TODO: make sure this handler is properly unregistered on disconnect

										var nBytes = (uint) bytes.Length;

										this.ReceiveTask(new SimpleTask(s2 => {
//											NLog.IPCrawler.Debug($"{IPCrawler.TAG} {nameof(this.ipCrawler.HandleOutput)}--{nBytes} bytes from node {node}");
											this.ipCrawler.HandleOutput(node, DateTimeEx.CurrentTime, nBytes);
										}));
									};
								} else {
									NLog.IPCrawler.Debug($"{IPCrawler.TAG} could not find node {node} within AllConnectionsList. This is unexpected.");

									foreach(PeerConnection c in this.connectionStore.AllConnectionsList) {
										NLog.IPCrawler.Verbose($"{IPCrawler.TAG} Found connection {c.NodeAddressInfo} searching for {node}");
									}

									NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ipCrawler.HandleTimeout)} (Failed Login) from node {node}");
									this.ipCrawler.HandleTimeout(node, DateTimeEx.CurrentTime);
								}
							} else {
								this.ipCrawler.HandleTimeout(node, DateTimeEx.CurrentTime);
							}
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

		protected override async Task ProcessLoop(LockContext lockContext) {
			try {

				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ProcessLoop)}.");

				this.CheckShouldCancel();

				// first thing, lets check if we have any tasks received to process
				await this.CheckTasks().ConfigureAwait(false);

				this.CheckShouldCancel();

				// Synchronize with ConnectionStore
				List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, true);
				this.ipCrawler.CombineIPs(currentAvailableNodes);

				// FIXME: this whole synchronization idea is a workaround while we add proper new connection/disconnection signals
				List<NodeAddressInfo> currentConnectedNodes =
					this.connectionStore.AllConnections.Select(pair => pair.Value.NodeAddressInfo).ToList();
				
				this.ipCrawler.SyncConnections(currentConnectedNodes, DateTimeEx.CurrentTime); //won't give the real connection time, not very important anyway.

				if(this.ShouldAct(ref this.nextAction)) {
					// ok, its time to act
					var secondsToWait = 3; // default next action time in seconds. we can play on this

					if(this.networkingService.NetworkingStatus == NetworkingService.NetworkingStatuses.Paused) {
						// its paused, we dont do anything, just return
						this.nextAction = DateTimeEx.CurrentTime.AddSeconds(secondsToWait);

						return;
					}

					this.ipCrawler.Crawl(this, DateTimeEx.CurrentTime);

					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {this.ipCrawlerRequests.Count} pending tasks: ");

					foreach((string name, Task task) in this.ipCrawlerRequests) {
						if(task.IsCompleted) {
							NLog.IPCrawler.Verbose($"{IPCrawler.TAG} Task {name} Completed.");
						}

						if(task.IsFaulted) {
							NLog.IPCrawler.Error($"{IPCrawler.TAG} Task {name} Faulted.");
						}
					}

					this.ipCrawlerRequests = this.ipCrawlerRequests.Where(el => !el.Item2.IsCompleted).ToList();

					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {this.ipCrawlerRequests.Count} remaining tasks.");

					this.ProcessLoopActions();

					//-
					// done, lets sleep for a while

					// lets act again in X seconds
					this.nextAction = DateTimeEx.CurrentTime.AddSeconds(secondsToWait);
				}
			} catch(OperationCanceledException) {
				throw;
			} catch(Exception ex) {
				NLog.IPCrawler.Error(ex, "Failed to process connections");
				this.nextAction = DateTimeEx.CurrentTime.AddSeconds(GlobalSettings.ApplicationSettings.MaxPeerCount);
			}
		}

		protected virtual async Task ContactHubs() {
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

		protected Task<bool> ContactHubsWeb() {
			var restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			return Repeater.RepeatAsync(async () => {
				var parameters = new Dictionary<string, object>();

				using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {
					List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, true, true, false);
					var nodeAddressInfoList = new NodeAddressInfoList(currentAvailableNodes);

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

					using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(ByteArray.FromBase64(result.Content));

					var infoList = new NodeAddressInfoList();
					infoList.Rehydrate(rehydrator);

					NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ipCrawler.HandleHubIPs)} (Web): {infoList.Nodes.Count} ips returned");
					this.ipCrawler.HandleHubIPs(infoList.Nodes.ToList(), DateTimeEx.CurrentTime);

					this.connectionStore.AddAvailablePeerNodes(infoList, true);

					return true;
				}

				throw new ApplicationException("Failed to query web hubs (Web)");
			});
		}

		protected async Task ContactHubsGossip() {
			try {
				NodeAddressInfoList infoList = this.connectionStore.GetHubNodes();

				//TODO: remove duplication with ContactHubsWeb
				NLog.IPCrawler.Verbose($"{IPCrawler.TAG} {nameof(this.ipCrawler.HandleHubIPs)} (Gossip): {infoList.Nodes.Count} ips returned");
				this.ipCrawler.HandleHubIPs(infoList.Nodes.ToList(), DateTimeEx.CurrentTime);

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
				this.CheckShouldCancel();

				// thats it, we say bye bye to this connection
				NLog.IPCrawler.Verbose($"Removing peer {ConnectionStore<R>.GetEndpointInfoNode(peer).ScoppedAdjustedIp}.");

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

			tasks.AddRange(await this.RoutedTaskReceiver.CheckTasks(async () => {
				// check this every loop, for responsiveness
				this.CheckShouldCancel();
			}).ConfigureAwait(false));

			return tasks;
		}

		protected virtual Task<List<NodeAddressInfo>> GetAvailableNodesList(bool onlyConnectables = true) {
			List<PeerConnection> activeConnections = this.connectionStore.AllConnectionsList;

			List<NodeAddressInfo> currentAvailableNodes = this.connectionStore.GetAvailablePeerNodes(null, false, true, onlyConnectables);

			// lets get a list of connected IPs
			List<string> connectedIps = activeConnections.Select(c => c.ScoppedIp).ToList();

			// lets make sure we remove the ones we are already connected to.
			return Task.FromResult(currentAvailableNodes.Where(an => !connectedIps.Contains(an.ScoppedIp)).ToList());
		}
	}

}