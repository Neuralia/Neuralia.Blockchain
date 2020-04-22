using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake {

	public interface IClientHandshakeWorkflow : IHandshakeWorkflow {
		NetworkEndPoint Endpoint { get; }
	}

	public static class ClientHandshakeWorkflow {
		public static ConcurrentDictionary<long, bool> ConnectingNonces = new ConcurrentDictionary<long, bool>();
	}

	public class ClientHandshakeWorkflow<R> : ClientWorkflow<HandshakeMessageFactory<R>, R>, IClientHandshakeWorkflow
		where R : IRehydrationFactory {

		protected PeerConnection serverConnection;

		public ClientHandshakeWorkflow(NetworkEndPoint endpoint, ServiceSet<R> serviceSet) : base(serviceSet) {
			this.Endpoint = endpoint;

			this.ExecutionMode = Workflow.ExecutingMode.Single;
			this.PeerUnique = true;
		}

		public NetworkEndPoint Endpoint { get; }

		protected override async Task PerformWork(LockContext lockContext) {
			try {
				if(!await this.PerformConnection().ConfigureAwait(false)) {
					this.CloseConnection();
				}
			} catch {
				this.CloseConnection();

				throw;
			}
		}

		private void CloseConnection() {
			// we failed to connect, this connection is a dud, we ensure it is removed from anywhere it may be
			this.serverConnection?.Dispose();
			this.serverConnection = null;
		}

		private async Task<bool> PerformConnection() {
			this.CheckShouldCancel();

			var handshakeTrigger = this.MessageFactory.CreateHandshakeWorkflowTriggerSet(this.CorrelationId);
			Log.Verbose("Sending correlation id {0}", this.CorrelationId);

			// lets inform the of our client version
			handshakeTrigger.Message.clientSoftwareVersion.SetVersion(GlobalSettings.SoftwareVersion);

			// tell the server our own time schedule
			handshakeTrigger.Message.localTime = this.timeService.CurrentRealTime;

			// now we inform them of our listening port, in case its non standard. 0 means disabled
			handshakeTrigger.Message.listeningPort = GlobalSettings.ApplicationSettings.Port;

			// generate a random nonce
			handshakeTrigger.Message.nonce = this.GenerateRandomHandshakeNonce();

			handshakeTrigger.Message.PerceivedIP = ConnectionStore<R>.GetEndpointIp(this.Endpoint).ToString();

			// register the nonce to help detect and avoid loopbacks
			ClientHandshakeWorkflow.ConnectingNonces.AddSafe(handshakeTrigger.Message.nonce, true);

			// now the supported chains and settings
			handshakeTrigger.Message.nodeInfo = GlobalSettings.Instance.NodeInfo;

			handshakeTrigger.Message.generalSettings = this.networkingService.GeneralSettings;
			
			// lets make one last check, to ensure this connection is not already happening (maybe they tried to connect to us) before we contact them
			if(this.networkingService.ConnectionStore.PeerConnectionExists(this.Endpoint, PeerConnection.Directions.Outgoing)) {
				// thats it, we are already connecting, lets stop here and ignore it. we are done and we wont go further.

				Log.Verbose("Connection already exists");

				return false;
			}

			try {
				this.serverConnection = this.GetNewConnection(this.Endpoint);

				if(!this.SendMessage(this.serverConnection, handshakeTrigger)) {
					Log.Verbose($"Connection with peer  {this.serverConnection.ScoppedAdjustedIp} was terminated");

					return false;
				}

				TargettedMessageSet<ServerHandshake<R>, R> serverHandshake = null;

				try {
					serverHandshake = this.WaitSingleNetworkMessage<ServerHandshake<R>, TargettedMessageSet<ServerHandshake<R>, R>, R>();
				} catch(Exception ex) {

					Log.Verbose("Failed to connect to peer");

					// this can happen if for some reason the other side cuts the connection early.
					this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);

					return false;
				}

				//TODO: check if any errors
				if(serverHandshake.Message.Status != ServerHandshake<R>.HandshakeStatuses.Ok) {

					if(serverHandshake.Message.Status == ServerHandshake<R>.HandshakeStatuses.Loopback) {
						Log.Verbose("We attempted to connect to ourselves. let's cancel that");

						if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(handshakeTrigger.Message.nonce)) {
							ClientHandshakeWorkflow.ConnectingNonces.RemoveSafe(handshakeTrigger.Message.nonce);
						}

						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddLocalAddress(this.Endpoint.EndPoint.Address);

						return false;
					} else if(serverHandshake.Message.Status == ServerHandshake<R>.HandshakeStatuses.AlreadyConnected) {
						Log.Verbose("We are already connected to this peer. removing");

						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);

						return false;
					} else if(serverHandshake.Message.Status == ServerHandshake<R>.HandshakeStatuses.AlreadyConnecting) {
						Log.Verbose("We are already connecting to this peer. removing");

						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);

						return false;
					} else {
						Log.Verbose("Server returned an error: {0}", serverHandshake.Message.Status);

						return false;
					}

				}

				// lets take note of this peer's type
				this.serverConnection.NodeInfo = serverHandshake.Message.nodeInfo;

				var clientConfirm = this.ProcessServerHandshake(handshakeTrigger, serverHandshake.Message, this.serverConnection);

				if(clientConfirm == null) {
					return false;
				}

				if(!this.SendMessage(this.serverConnection, clientConfirm)) {
					Log.Verbose($"Connection with peer  {this.serverConnection.ScoppedAdjustedIp} was terminated");

					return false;
				}

				var serverResponse = this.WaitSingleNetworkMessage<ServerHandshakeConfirm<R>, TargettedMessageSet<ServerHandshakeConfirm<R>, R>, R>();

				if(this.ProcessServerHandshakeConfirm(handshakeTrigger, serverHandshake.Message, serverResponse.Message, this.serverConnection)) {
					// it is a confirmed connection, we are now friends

					await this.AddValidConnection(serverResponse.Message, this.serverConnection).ConfigureAwait(false);

					this.SendClientReadyReply(handshakeTrigger, this.serverConnection);

					return true;
				} 
			} finally {
				if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(handshakeTrigger.Message.nonce)) {
					ClientHandshakeWorkflow.ConnectingNonces.RemoveSafe(handshakeTrigger.Message.nonce);
				}
			}

			return false;
		}

		protected virtual void SendClientReadyReply(TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger, PeerConnection serverConnection) {
			// lets inform the server that we are ready to go forward
			var clientReady = this.MessageFactory.CreateClientReadySet(handshakeTrigger.Header);

			if(!this.SendMessage(serverConnection, clientReady)) {
				Log.Verbose($"Connection with peer  {serverConnection.ScoppedAdjustedIp} was terminated");

			}
		}

		protected virtual long GenerateRandomHandshakeNonce() {
			return GlobalRandom.GetNextUInt();
		}

		protected virtual long GenerateRandomConfirmNonce() {
			return GlobalRandom.GetNextUInt();
		}

		protected virtual Task AddValidConnection(ServerHandshakeConfirm<R> serverHandshakeConfirm, PeerConnection peerConnectionn) {
			// take the peer nodes
			peerConnectionn.SetPeerNodes(serverHandshakeConfirm.nodes);

			// handshake confirmed
			this.networkingService.ConnectionStore.ConfirmConnection(peerConnectionn);
			this.networkingService.ConnectionStore.FullyConfirmConnection(peerConnectionn);

			Log.Verbose($"handshake with {peerConnectionn.ScoppedAdjustedIp} is now confirmed");

			return Task.CompletedTask;
		}

		protected virtual TargettedMessageSet<ClientHandshakeConfirm<R>, R> ProcessServerHandshake(TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger, ServerHandshake<R> serverHandshake, PeerConnection peerConnectionn) {

			Log.Verbose("Sending client confirm response");
			var clientConfirm = this.MessageFactory.CreateClientConfirmSet(handshakeTrigger.Header);
			Log.Verbose("Sending again correlation id {0}", this.CorrelationId);

			if(serverHandshake.nodeInfo != NodeInfo.Hub) {
				// first, lets confirm their time definition is within acceptable range
				if(!this.timeService.WithinAcceptableRange(serverHandshake.localTime, TimeSpan.FromSeconds(3))) {
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.TimeOutOfSync;

					Log.Verbose("Sending handshake negative response");
					this.SendFinalMessage(peerConnectionn, clientConfirm);

					return null;
				}

				// now we validate our peer
				// then we validate the client Scope, make sure its not too old
				if(!GlobalSettings.SoftwareVersion.IsVersionAcceptable(serverHandshake.clientSoftwareVersion)) {
					// we do not accept this version
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

					Log.Verbose("Sending handshake negative response, the peer version is unacceptable");
					this.SendFinalMessage(peerConnectionn, clientConfirm);

					return null;
				}

				// ok, seem its all in order, lets take its values
				peerConnectionn.clientSoftwareVersion.SetVersion(serverHandshake.clientSoftwareVersion);

				peerConnectionn.SetGeneralSettings(serverHandshake.generalSettings);
				
				// now we check the blockchains and the version they allow
				foreach(var chainSetting in serverHandshake.nodeInfo.GetChainSettings()) {

					// validate the blockchain valid minimum version
					peerConnectionn.AddSupportedChain(chainSetting.Key, this.networkingService.IsChainVersionValid(chainSetting.Key, serverHandshake.clientSoftwareVersion));

					// store the reported settings for later use
					peerConnectionn.SetChainSettings(chainSetting.Key, chainSetting.Value);
				}

				if(peerConnectionn.NoSupportedChains || peerConnectionn.NoValidChainVersion) {
					// ok, this is peer is just not usable, we have to disconnect
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

					Log.Verbose("Sending handshake negative response, the peer version is unacceptable");
					this.SendFinalMessage(peerConnectionn, clientConfirm);

					return null;
				}

				// ok, here the peer is usable

				ConnectionStore.PublicIpSource source = ConnectionStore.PublicIpSource.Peer;

				if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.SkipHubCheck && this.networkingService.ConnectionStore.IsNeuraliumHub(peerConnectionn)) {

					Log.Verbose("The non hub reported peer is listed as a hub!");

					return null;
				}

				if(serverHandshake.Connectable.HasValue) {
					this.networkingService.ConnectionStore.AddPeerReportedConnectable(serverHandshake.Connectable.Value, source);
				}

				this.networkingService.ConnectionStore.AddPeerReportedPublicIp(IPUtils.GuidToIP(serverHandshake.PerceivedIP), source);

				// then send our OK
				
				// lets send the server our list of nodeAddressInfo IPs

				clientConfirm.Message.SetNodes(this.networkingService.ConnectionStore.GetPeerNodeList(serverHandshake.nodeInfo, serverHandshake.nodeInfo.GetSupportedBlockchains(), NodeSelectionHeuristicTools.NodeSelectionHeuristics.Default, new[] {peerConnectionn.NodeAddressInfo}.ToList(), false, 20));
			} else {

				if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.SkipHubCheck && !this.networkingService.ConnectionStore.IsNeuraliumHub(peerConnectionn)) {
					Log.Verbose("The reported hub is not listed as a hub!");

					return null;
				}

				this.networkingService.ConnectionStore.AddPeerReportedPublicIp(IPUtils.GuidToIP(serverHandshake.PerceivedIP), ConnectionStore.PublicIpSource.Hub);

				// lets send the server our list of nodeAddressInfo IPs
				clientConfirm.Message.SetNodes(this.networkingService.ConnectionStore.GetPeerNodeList(NodeInfo.Hub, this.networkingService.SupportedChains, NodeSelectionHeuristicTools.NodeSelectionHeuristics.Default, new[] {peerConnectionn.NodeAddressInfo}.ToList(), false, 20));
			}

			// generate a random nonce and send it to the server
			clientConfirm.Message.nonce = this.GenerateRandomConfirmNonce();

			return clientConfirm;
		}

		protected virtual bool ProcessServerHandshakeConfirm(TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger, ServerHandshake<R> serverHandshake, ServerHandshakeConfirm<R> serverHandshakeConfirm, PeerConnection peerConnectionn) {

			if(serverHandshakeConfirm.Status == ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.CanGoNoFurther) {

				if(serverHandshake.nodeInfo != NodeInfo.Hub) {
					Log.Verbose("The peer stops the connection like a hub, but does not report as one. This is illegal");

					return false;
				}

				if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.SkipHubCheck && !this.networkingService.ConnectionStore.IsNeuraliumHub(peerConnectionn)) {

					Log.Verbose("The peer behaves like a hub but is not recorded as a hub. this is illegal");

					return false;
				}

				// ok, this node does not go any further. lets take the results it nicely sent us and add it to our contents
				// take the peer nodes
				this.networkingService.ConnectionStore.AddAvailablePeerNodes(serverHandshakeConfirm.nodes, false);

				Log.Verbose("Server tells us it can go no further. we will now disconnect");
				
				// this connection is not added, goes no further
				return false;
			}

			if(serverHandshakeConfirm.Status == ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Rejected) {
				Log.Verbose("The peer rejected our connection :(");

				return false;
			}

			if(serverHandshakeConfirm.Status == ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Error) {
				Log.Verbose("The peer reported and error. the connection failed.");

				return false;
			}

			if(serverHandshakeConfirm.Status != ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Ok) {
				Log.Verbose("Something wrong happened, we can go no further.");
				
				return false;
			}

			if(serverHandshake.nodeInfo == NodeInfo.Hub) {
				Log.Verbose("The peer reported as a hub, but did not Stop the connection when it should. This is illegal");

				return false;
			}

			return true;
		}

		protected override HandshakeMessageFactory<R> CreateMessageFactory() {
			return new HandshakeMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is IClientHandshakeWorkflow clientHandshakeWorkflow) {
				return ConnectionStore<R>.GetEndpointIp(this.Endpoint).ToString() == ConnectionStore<R>.GetEndpointIp(clientHandshakeWorkflow.Endpoint).ToString();
			}

			return base.CompareOtherPeerId(other);
		}
	}
}