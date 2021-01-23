using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Locking;

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


		public class ClientHandshakeException : WorkflowException
		{
			public enum ExceptionDetails
			{
				Unknown,
				IsHub,
				BadHub,
				NoAnswer,
				TimeOutOfSync,
				Duplicate,
				ConnectionDropped,
				ConnectionError,
				ConnectionsSaturated,
				InvalidPeer,
				InvalidNetworkId,
				ChainUnsupported,
				Rejected,
				Loopback,
				AlreadyConnected,
				AlreadyConnecting,
				ClientHandshakeConfirmFailed,
				ClientHandshakeConfirmDropped,
				CanGoNoFurther,
				ClientVersionRefused,
			}

			public ExceptionDetails Details { get; private set; } = ExceptionDetails.Unknown;
			public ClientHandshakeException(ExceptionDetails details) : base($"{details}") {
				this.Details = details;
			}

		}

		public NetworkEndPoint Endpoint { get; }

		protected override void LogWorkflowException(Exception ex) {

			if(ex is WorkflowException || ex is AggregateException agg && agg.InnerException is WorkflowException) {
				// do nothing
			} else {
				base.LogWorkflowException(ex);
			}
		}

		protected override async Task PerformWork(LockContext lockContext) {
			try {
				if(!await this.PerformConnection().ConfigureAwait(false)) {
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Unknown);
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

		private async Task<bool> PerformConnection()
		{
			this.CheckShouldCancel();
			
			TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger = this.MessageFactory.CreateHandshakeWorkflowTriggerSet(this.CorrelationId);
			NLog.Default.Verbose("Sending correlation id {0}", this.CorrelationId);

			// lets inform the of our client version
			handshakeTrigger.Message.clientSoftwareVersion.SetVersion(GlobalSettings.BlockchainCompatibilityVersion);
			
			// now we inform them of our listening port, in case its non standard. 0 means disabled
			handshakeTrigger.Message.listeningPort = this.networkingService.LocalPort;

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
				NLog.Default.Verbose("Connection already exists");
				throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Duplicate);
			}
			
			// tell the server our own time schedule
			handshakeTrigger.Message.localTime = this.timeService.CurrentRealTime;

			try {
				this.serverConnection = this.GetNewConnection(this.Endpoint);

				if(!this.SendMessage(this.serverConnection, handshakeTrigger)) {
					NLog.Default.Verbose($"Connection with peer  {this.serverConnection.ScopedAdjustedIp} was terminated");
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.NoAnswer);
				}

				TargettedMessageSet<ServerHandshake<R>, R> serverHandshake = null;

				try {
					serverHandshake = this.WaitSingleNetworkMessage<ServerHandshake<R>, TargettedMessageSet<ServerHandshake<R>, R>, R>();
				} catch(Exception ex) {

					NLog.Default.Verbose("Failed to connect to peer");
					// this can happen if for some reason the other side cuts the connection early.
					this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo); //FIXME: this is now IPMarshall's job
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ConnectionDropped);
				}
				

				switch (serverHandshake.Message.Status)
				{ 
					case ServerHandshake<R>.HandshakeStatuses.Ok:
						break;
					case ServerHandshake<R>.HandshakeStatuses.Loopback:
						NLog.Default.Verbose("We attempted to connect to ourselves. let's cancel that");

						if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(handshakeTrigger.Message.nonce)) {
							ClientHandshakeWorkflow.ConnectingNonces.RemoveSafe(handshakeTrigger.Message.nonce);
						}
						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddLocalAddress(this.Endpoint.EndPoint.Address);
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Loopback);
					case ServerHandshake<R>.HandshakeStatuses.AlreadyConnected:
						NLog.Default.Verbose("We are already connected to this peer. removing");
						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.AlreadyConnected);
					case ServerHandshake<R>.HandshakeStatuses.AlreadyConnecting:
						NLog.Default.Verbose("We are already connecting to this peer. removing");
						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.AlreadyConnecting);
					case ServerHandshake<R>.HandshakeStatuses.ConnectionsSaturated:
						NLog.Default.Verbose("This peer is saturated.");
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ConnectionsSaturated);
					case ServerHandshake<R>.HandshakeStatuses.InvalidPeer:
						NLog.Default.Verbose("This peer is invalid.");
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.InvalidPeer);
					case ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused:
						NLog.Default.Verbose("This peer's client version is refused.");
						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ClientVersionRefused);
					case ServerHandshake<R>.HandshakeStatuses.InvalidNetworkId:
						NLog.Default.Verbose("This peer's network Id is invalid.");
						// let's make the connection as one of ours.
						this.networkingService.ConnectionStore.AddIgnorePeerNode(this.serverConnection.NodeAddressInfo);
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.InvalidNetworkId);	
					case ServerHandshake<R>.HandshakeStatuses.TimeOutOfSync:
						NLog.Default.Verbose("This peer's current time seems out of sync.");
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.InvalidNetworkId);

					case ServerHandshake<R>.HandshakeStatuses.ChainUnsupported:
						NLog.Default.Verbose("This peer's chain is not supported.");
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ChainUnsupported);
		
					default:
						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Unknown);
						
				}


				// lets take note of this peer's type
				this.serverConnection.NodeInfo = serverHandshake.Message.nodeInfo;

				TargettedMessageSet<ClientHandshakeConfirm<R>, R> clientConfirm = this.ProcessServerHandshake(handshakeTrigger, serverHandshake.Message, this.serverConnection);

				if(clientConfirm == null) {
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ClientHandshakeConfirmFailed);
				}

				if(!this.SendMessage(this.serverConnection, clientConfirm)) {
					NLog.Default.Verbose($"Connection with peer  {this.serverConnection.ScopedAdjustedIp} was terminated");
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ClientHandshakeConfirmDropped);
				}

				TargettedMessageSet<ServerHandshakeConfirm<R>, R> serverResponse = this.WaitSingleNetworkMessage<ServerHandshakeConfirm<R>, TargettedMessageSet<ServerHandshakeConfirm<R>, R>, R>();

				if(this.ProcessServerHandshakeConfirm(handshakeTrigger, serverHandshake.Message, serverResponse.Message, this.serverConnection)) {
					// it is a confirmed connection, we are now friends
					
					NLog.Default.Verbose($"Handshake confirmed, adding connection  {this.serverConnection.ScopedAdjustedIp} to connection store (async).");
					try
					{
						await this.AddValidConnection(serverResponse.Message, this.serverConnection).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						NLog.Connections.Warning(e,$"[ClientHandshakeWorkflow] Failed to add connection with {this.serverConnection.NodeAddressInfo}");
						throw;
					}

					return this.SendClientReadyReply(handshakeTrigger, this.serverConnection);
				}
			} finally {
				if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(handshakeTrigger.Message.nonce)) {
					ClientHandshakeWorkflow.ConnectingNonces.RemoveSafe(handshakeTrigger.Message.nonce);
				}
			}
			
			return false;
		}

		protected virtual bool SendClientReadyReply(TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger, PeerConnection serverConnection) {
			// lets inform the server that we are ready to go forward
			TargettedMessageSet<ClientReady<R>, R> clientReady = this.MessageFactory.CreateClientReadySet(handshakeTrigger.Header);

			if(!this.SendMessage(serverConnection, clientReady)) {
				NLog.Default.Verbose($"Connection with peer  {serverConnection.ScopedAdjustedIp} was terminated");

				return false;
			}

			return true;
		}

		protected virtual long GenerateRandomHandshakeNonce() {
			return GlobalRandom.GetNextUInt();
		}

		protected virtual long GenerateRandomConfirmNonce() {
			return GlobalRandom.GetNextUInt();
		}

		protected virtual async Task AddValidConnection(ServerHandshakeConfirm<R> serverHandshakeConfirm, PeerConnection peerConnectionn) {
			// take the peer nodes
			peerConnectionn.SetPeerNodes(serverHandshakeConfirm.nodes);

			// handshake confirmed
			await this.networkingService.ConnectionStore.ConfirmConnection(peerConnectionn).ConfigureAwait(false);
			await this.networkingService.ConnectionStore.FullyConfirmConnection(peerConnectionn).ConfigureAwait(false);

			NLog.Default.Verbose($"handshake with {peerConnectionn.ScopedAdjustedIp} is now confirmed");
			
		}

		protected virtual TargettedMessageSet<ClientHandshakeConfirm<R>, R> ProcessServerHandshake(TriggerMessageSet<HandshakeTrigger<R>, R> handshakeTrigger, ServerHandshake<R> serverHandshake, PeerConnection peerConnectionn) {

			NLog.Default.Verbose("Sending client confirm response");
			TargettedMessageSet<ClientHandshakeConfirm<R>, R> clientConfirm = this.MessageFactory.CreateClientConfirmSet(handshakeTrigger.Header);
			NLog.Default.Verbose("Sending again correlation id {0}", this.CorrelationId);

			if(serverHandshake.nodeInfo != NodeInfo.Hub) {
				// first, lets confirm their time definition is within acceptable range
				if(!this.timeService.WithinAcceptableRange(serverHandshake.localTime, TimeSpan.FromSeconds(9))) {
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.TimeOutOfSync;

					NLog.Default.Verbose($"Sending handshake negative response, server time is out of sync with a delta of {DateTimeEx.CurrentTime - serverHandshake.localTime.ToUniversalTime()}.");
					this.SendFinalMessage(peerConnectionn, clientConfirm);
					
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.TimeOutOfSync);
				}

				// now we validate our peer
				// then we validate the client Scope, make sure its not too old
				if(!GlobalSettings.BlockchainCompatibilityVersion.IsVersionAcceptable(serverHandshake.clientSoftwareVersion)) {
					// we do not accept this version
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

					NLog.Default.Verbose("Sending handshake negative response, the peer version is unacceptable");
					this.SendFinalMessage(peerConnectionn, clientConfirm);

					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ClientVersionRefused);
				}

				// ok, seem its all in order, lets take its values
				peerConnectionn.clientSoftwareVersion.SetVersion(serverHandshake.clientSoftwareVersion);

				peerConnectionn.SetGeneralSettings(serverHandshake.generalSettings);

				// now we check the blockchains and the version they allow
				foreach(KeyValuePair<BlockchainType, ChainSettings> chainSetting in serverHandshake.nodeInfo.GetChainSettings()) {

					// validate the blockchain valid minimum version
					peerConnectionn.AddSupportedChain(chainSetting.Key, this.networkingService.IsChainVersionValid(chainSetting.Key, serverHandshake.clientSoftwareVersion));

					// store the reported settings for later use
					peerConnectionn.SetChainSettings(chainSetting.Key, chainSetting.Value);
				}

				if(peerConnectionn.NoSupportedChains || peerConnectionn.NoValidChainVersion) {
					// ok, this is peer is just not usable, we have to disconnect
					clientConfirm.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

					NLog.Default.Verbose("Sending handshake negative response, the peer version is unacceptable");
					this.SendFinalMessage(peerConnectionn, clientConfirm);

					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ClientVersionRefused);
				}

				// ok, here the peer is usable

				ConnectionStore.PublicIpSource source = ConnectionStore.PublicIpSource.Peer;

				if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.SkipHubCheck && this.networkingService.ConnectionStore.IsNeuraliumHub(peerConnectionn)) {

					NLog.Default.Verbose("The non hub reported peer is listed as a hub!");

					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.BadHub);
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
					NLog.Default.Verbose("The reported hub is not listed as a hub!");

					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.BadHub);
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

			switch (serverHandshakeConfirm.Status)
			{
				case ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Ok:
				{
					
					if(serverHandshake.nodeInfo == NodeInfo.Hub) {
						NLog.Default.Verbose("The peer reported as a hub, but did not Stop the connection when it should. This is illegal");

						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.BadHub);
					}

					return true;
				}
				case ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.CanGoNoFurther:
				{

					if(serverHandshake.nodeInfo != NodeInfo.Hub) {
						NLog.Default.Verbose("The peer stops the connection like a hub, but does not report as one. This is illegal");

						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.CanGoNoFurther);
					}

					if(!GlobalSettings.ApplicationSettings.UndocumentedDebugConfigurations.SkipHubCheck && !this.networkingService.ConnectionStore.IsNeuraliumHub(peerConnectionn)) {

						NLog.Default.Verbose("The peer behaves like a hub but is not recorded as a hub. this is illegal");

						throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.BadHub);
					}

					// ok, this node does not go any further. lets take the results it nicely sent us and add it to our contents
					// take the peer nodes
					this.networkingService.ConnectionStore.AddAvailablePeerNodes(serverHandshakeConfirm.nodes, false);

					NLog.Default.Verbose("Server tells us it can go no further. we will now disconnect");

					// this connection is not added, goes no further
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.IsHub);
				}
				case ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Rejected:
					NLog.Default.Verbose("The peer rejected our connection :(");
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Rejected);
				case ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Error:
					NLog.Default.Verbose("The peer reported and error. the connection failed.");
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.ConnectionError);
				default:
					NLog.Default.Verbose("Something wrong happened, we can go no further.");
					throw new ClientHandshakeException(ClientHandshakeException.ExceptionDetails.Unknown);
			}

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