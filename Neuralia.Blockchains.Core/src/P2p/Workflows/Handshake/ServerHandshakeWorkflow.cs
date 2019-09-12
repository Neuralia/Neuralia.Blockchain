using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Cryptography;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake {
	public interface IHandshakeWorkflow {
	}

	public interface IServerHandshakeWorkflow : IHandshakeWorkflow {
	}

	public class ServerHandshakeWorkflow<R> : ServerWorkflow<HandshakeTrigger<R>, HandshakeMessageFactory<R>, R>, IServerHandshakeWorkflow
		where R : IRehydrationFactory {
		public ServerHandshakeWorkflow(TriggerMessageSet<HandshakeTrigger<R>, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(triggerMessage, clientConnection, serviceSet) {

			// allow only one per peer at a time
			//TODO: having replaceable workflow may lead to abuse or DDOS attempts. we may need to lof these replacements, make sure they remain reasonable

			this.ExecutionMode = Workflow.ExecutingMode.SingleRepleacable;

			this.PeerUnique = true;
		}

		protected virtual Enums.PeerTypes PeerType => GlobalSettings.Instance.PeerType;

		protected override void PerformWork() {
			try {
				if(!this.PerformConnection()) {
					this.CloseConnection();
				}
			} catch {
				this.CloseConnection();

				throw;
			}
		}

		private void CloseConnection() {
			// we failed to connect, this connection is a dud, we ensure it is removed from anywhere it may be
			this.ClientConnection?.Dispose();
		}

		private bool PerformConnection() {
			this.CheckShouldCancel();

			var serverHandshake = this.ProcessClientHandshake();

			if(serverHandshake == null) {
				return false;
			}

			Log.Verbose("Sending handshake response");

			if(!this.Send(serverHandshake)) {
				Log.Verbose($"Connection with peer  {this.ClientConnection.ScoppedAdjustedIp} was terminated");

				return false;
			}

			var responseNetworkMessageSet = this.WaitSingleNetworkMessage<ClientHandshakeConfirm<R>, TargettedMessageSet<ClientHandshakeConfirm<R>, R>, R>();

			if(responseNetworkMessageSet.Message.Status != ServerHandshake<R>.HandshakeStatuses.Ok) {
				Log.Verbose($"Client returned an error: {responseNetworkMessageSet.Message.Status}. Sending handshake negative response");

				return false;
			}

			// take the peer nodes
			this.SetReceivedPeerNodes(responseNetworkMessageSet.Message);

			Log.Verbose("Sending server confirm response");

			var serverConfirm = this.ProcessClientHandshakeConfirm(responseNetworkMessageSet.Message);

			if(serverConfirm == null) {
				return false;
			}

			// perform one last validation befoer we go further
			this.PerformFinalValidation(serverConfirm);

			if(serverConfirm.Message.Status == ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Ok) {
				if(!this.Send(serverConfirm)) {
					Log.Verbose($"Connection with peer  {this.ClientConnection.ScoppedAdjustedIp} was terminated");

					return false;
				}

				// handshake confirmed
				this.AddValidConnection();

				// now we wait for the final confirmation
				this.WaitFinalClientReady();

				return true;

				// done
			}

			// we end here.
			this.SendFinal(serverConfirm);

			return false;
		}

		protected virtual void PerformFinalValidation(TargettedMessageSet<ServerHandshakeConfirm<R>, R> serverConfirm) {
			// nothing to do
		}

		protected virtual void WaitFinalClientReady() {
			var finalNetworkMessageSet = this.WaitSingleNetworkMessage<ClientReady<R>, TargettedMessageSet<ClientReady<R>, R>, R>();

			this.networkingService.ConnectionStore.FullyConfirmConnection(this.ClientConnection);
		}

		protected virtual void SetReceivedPeerNodes(ClientHandshakeConfirm<R> message) {
			this.ClientConnection.SetPeerNodes(message.nodes);
		}

		protected virtual TargettedMessageSet<ServerHandshake<R>, R> ProcessClientHandshake() {

			var serverHandshake = this.MessageFactory.CreateServerHandshakeSet(this.triggerMessage.Header);

			if(this.triggerMessage.Message.networkId != GlobalSettings.Instance.NetworkId) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.InvalidNetworkId;

				Log.Verbose("Sending handshake negative response, invalid network id");
				this.SendFinal(serverHandshake);

				return null;
			}

			// first, let's detect loopbacks
			if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(serverHandshake.Message.nonce) || (ProtocolFactory.PROTOCOL_UUID == this.ClientId)) {
				// ok, we tried to connect to ourselves, lets cancel that
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.Loopback;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddLocalAddress(this.ClientConnection.NodeAddressInfoInfo.Address);

				Log.Verbose("We received a connection from ourselves, let's cancel that. Sending handshake negative response; loopback connection");

				this.SendFinal(serverHandshake);

				return null;
			}

			if(!this.CheckPeerValid()) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.InvalidPeer;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfoInfo);

				Log.Verbose("We received a connection from an invalid peer; rejecting connection");

				this.SendFinal(serverHandshake);

				return null;
			}

			// now determine if we are already connected to this peer
			if(this.networkingService.ConnectionStore.PeerConnected(this.ClientConnection.connection)) {
				// ok, we tried to connect to ourselves, lets cancel that
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.AlreadyConnected;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfoInfo);

				Log.Verbose("We received a connection that we are already connected to. Sending handshake negative response; existing connection");

				this.SendFinal(serverHandshake);

				return null;
			}

			// now let's determine if we are already connecting to this peer
			if(this.networkingService.ConnectionStore.PeerConnecting(this.ClientConnection.connection, PeerConnection.Directions.Incoming)) {

				// seems we already are. lets break the tie
				ConnectionStore.ConnectionTieResults tieResult = this.networkingService.ConnectionStore.BreakingConnectionTie(this.ClientConnection.connection, PeerConnection.Directions.Incoming);

				if(tieResult == ConnectionStore.ConnectionTieResults.Challenger) {
					// ok, we tried to connect to ourselves, lets cancel that
					serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.AlreadyConnecting;

					// make sure we ignore it
					this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfoInfo);

					Log.Verbose("We received a connection that we are already connected to. closing");

					this.SendFinal(serverHandshake);

					return null;
				}

			}

			// next thing, lets check if we still have room for more connections
			if(!this.CheckStaturatedConnections(serverHandshake)) {

				Log.Verbose("We are saturated. too many connections");

				return null;
			}

			// ok, we just received a trigger, lets examine it
			Log.Verbose($"Received correlation id {this.CorrelationId}");

			// first, lets confirm their time definition is within acceptable range
			if(!this.timeService.WithinAcceptableRange(this.triggerMessage.Message.localTime)) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.TimeOutOfSync;

				Log.Verbose("Sending handshake negative response");
				this.SendFinal(serverHandshake);

				return null;
			}

			// then we validate the client Scope, make sure its not too old
			if(!this.TestClientVersion(serverHandshake)) {
				Log.Verbose("The client has an invalid version");

				return null;
			}

			// ok, store its version
			this.ClientConnection.clientSoftwareVersion.SetVersion(this.triggerMessage.Message.clientSoftwareVersion);

			// lets take note of this peer's type
			this.ClientConnection.PeerType = this.triggerMessage.Message.peerType;

			// now we check the blockchains and the version they allow
			foreach(var chainSetting in this.triggerMessage.Message.chainSettings) {

				// validate the blockchain valid minimum version
				this.ClientConnection.AddSupportedChain(chainSetting.Key, this.networkingService.IsChainVersionValid(chainSetting.Key, this.triggerMessage.Message.clientSoftwareVersion));
			}

			if(!this.CheckSupportedBlockchains(serverHandshake)) {

				Log.Verbose("The client does not support the blockchains we want");

				return null;
			}

			// let's record what we got
			this.ClientConnection.NodeAddressInfoInfo.RealPort = this.triggerMessage.Message.listeningPort;

			// and check if their connection port is true and available
			serverHandshake.Message.Connectible = this.PerformCounterConnection();

			this.networkingService.ConnectionStore.AddPeerReportedPublicIp(this.triggerMessage.Message.PerceivedIP, ConnectionStore.PublicIpSource.Peer);

			this.networkingService.ConnectionStore.AddChainSettings(this.ClientConnection.NodeAddressInfoInfo, this.triggerMessage.Message.chainSettings);

			// ok, we are ready to send a positive answer

			// lets tell them our own information
			serverHandshake.Message.clientSoftwareVersion.SetVersion(GlobalSettings.SoftwareVersion);
			serverHandshake.Message.localTime = this.timeService.CurrentRealTime;

			// let's be nice and report it as we see it
			serverHandshake.Message.PerceivedIP = this.ClientConnection.NodeAddressInfoInfo.Ip;

			// generate a random nonce and send it to the server
			serverHandshake.Message.nonce = this.GenerateRandomHandshakeNonce();

			serverHandshake.Message.chainSettings = this.networkingService.ChainSettings;

			serverHandshake.Message.peerType = this.PeerType;

			return serverHandshake;
		}

		protected virtual bool? PerformCounterConnection() {
			// now we counterconnect to determine if they are truly listening on their port
			try {
				return this.ClientConnection.connection.PerformCounterConnection(this.ClientConnection.NodeAddressInfoInfo.RealPort);
			} catch {
				// nothing to do here, its just a fail and thus unconnectable
			}

			return false;
		}

		protected virtual bool CheckPeerValid() {

			return true;
		}

		protected virtual bool CheckStaturatedConnections(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {

			AppSettingsBase.WhitelistedNode whiteList = GlobalSettings.ApplicationSettings.Whitelist.SingleOrDefault(e => e.ip == this.ClientConnection.Ip);

			if(this.networkingService.ConnectionStore.ConnectionsSaturated) {

				// well, we have no more room
				if((whiteList != null) && (whiteList.AcceptanceType == AppSettingsBase.WhitelistedNode.AcceptanceTypes.Always)) {
					// ok, thats fine, we will take this peer anyways, it is absolutely whitelisted
				} else {
					// too bad, we are saturated, we must refuse
					serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ConnectionsSaturated;

					Log.Verbose("We received a connection but we already have too many. rejecting nicely");

					this.SendFinal(serverHandshake);

					return false;
				}
			} else {
				if((whiteList != null) && (whiteList.AcceptanceType == AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots)) {
					// here also, we accept this peer no matter what
				}
			}

			return true;
		}

		protected virtual bool CheckSupportedBlockchains(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {
			if(this.ClientConnection.NoSupportedChains || this.ClientConnection.NoValidChainVersion) {
				// ok, this is peer is just not usable, we have to disconnect
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

				Log.Verbose("Sending handshake negative response, the peer version is unacceptable");
				this.SendFinal(serverHandshake);

				return false;
			}

			return true;
		}

		protected virtual bool TestClientVersion(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {

			if(!GlobalSettings.SoftwareVersion.IsVersionAcceptable(this.triggerMessage.Message.clientSoftwareVersion)) {
				// we do not accept this version
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

				Log.Verbose("Sending handshake negative response");
				this.SendFinal(serverHandshake);

				return false;
			}

			return true;
		}

		protected virtual TargettedMessageSet<ServerHandshakeConfirm<R>, R> ProcessClientHandshakeConfirm(ClientHandshakeConfirm<R> handshakeConfirm) {
			var serverConfirm = this.MessageFactory.CreateServerConfirmSet(this.triggerMessage.Header);
			Log.Verbose($"Received again correlation id {this.CorrelationId}");

			// lets send the server our list of nodeAddressInfo IPs

			// now we decide what kind of list to share with our peer. power ones should get power peers
			ConnectionStore.PeerSelectionHeuristic heuristic = this.DetermineHeuristic();

			serverConfirm.Message.SetNodes(this.GetSharedPeerNodeList(heuristic));

			// generate a random nonce and send it to the server
			serverConfirm.Message.nonce = this.GenerateRandomConfirmNonce();

			return serverConfirm;
		}

		protected virtual ConnectionStore.PeerSelectionHeuristic DetermineHeuristic() {
			if(Enums.SimplePeerTypes.Contains(this.ClientConnection.PeerType)) {
				return ConnectionStore.PeerSelectionHeuristic.Simple;
			}

			return ConnectionStore.PeerSelectionHeuristic.Powers;
		}

		protected virtual Dictionary<Enums.PeerTypes, NodeAddressInfoList> GetSharedPeerNodeList(ConnectionStore.PeerSelectionHeuristic heuristic) {
			return this.networkingService.ConnectionStore.GetPeerNodeList(heuristic, new[] {this.ClientConnection.NodeAddressInfoInfo}.ToList(), 20);
		}

		protected virtual void AddValidConnection() {
			this.networkingService.ConnectionStore.ConfirmConnection(this.ClientConnection);
			Log.Verbose($"handshake with {this.ClientConnection.ScoppedAdjustedIp} is now confirmed");

		}

		protected virtual long GenerateRandomHandshakeNonce() {
			return GlobalRandom.GetNextLong();
		}

		protected virtual long GenerateRandomConfirmNonce() {
			return GlobalRandom.GetNextLong();
		}

		protected override HandshakeMessageFactory<R> CreateMessageFactory() {
			return new HandshakeMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ServerHandshakeWorkflow<R> clientHandshakeWorkflow) {
				return this.triggerMessage.Header.originatorId == clientHandshakeWorkflow.triggerMessage.Header.originatorId;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}