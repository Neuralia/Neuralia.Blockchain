using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
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

		protected virtual NodeInfo PeerType => GlobalSettings.Instance.NodeInfo;

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
					throw new WorkflowException();
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

		private async Task<bool> PerformConnection() {
			this.CheckShouldCancel();

			TargettedMessageSet<ServerHandshake<R>, R> serverHandshake = await this.ProcessClientHandshake().ConfigureAwait(false);

			if(serverHandshake == null) {
				return false;
			}

			NLog.Default.Verbose("Sending handshake response");

			if(!await Send(serverHandshake).ConfigureAwait(false)) {
				NLog.Default.Verbose($"Connection with peer  {this.ClientConnection.ScopedAdjustedIp} was terminated");

				return false;
			}

			TargettedMessageSet<ClientHandshakeConfirm<R>, R> responseNetworkMessageSet = await WaitSingleNetworkMessage<ClientHandshakeConfirm<R>, TargettedMessageSet<ClientHandshakeConfirm<R>, R>, R>().ConfigureAwait(false);

			if(responseNetworkMessageSet.Message.Status != ServerHandshake<R>.HandshakeStatuses.Ok) {
				NLog.Default.Verbose($"Client returned an error: {responseNetworkMessageSet.Message.Status}. Sending handshake negative response");

				return false;
			}

			// take the peer nodes
			this.SetReceivedPeerNodes(responseNetworkMessageSet.Message);

			NLog.Default.Verbose("Sending server confirm response");

			TargettedMessageSet<ServerHandshakeConfirm<R>, R> serverConfirm = this.ProcessClientHandshakeConfirm(responseNetworkMessageSet.Message);

			if(serverConfirm == null) {
				return false;
			}

			// perform one last validation befoer we go further
			this.PerformFinalValidation(serverConfirm);

			if(serverConfirm.Message.Status == ServerHandshakeConfirm<R>.HandshakeConfirmationStatuses.Ok) {
				if(!await Send(serverConfirm).ConfigureAwait(false)) {
					NLog.Default.Verbose($"Connection with peer  {this.ClientConnection.ScopedAdjustedIp} was terminated");

					return false;
				}

				// handshake confirmed
				await this.AddValidConnection().ConfigureAwait(false);

				// now we wait for the final confirmation
				await WaitFinalClientReady().ConfigureAwait(false);

				return true;

				// done
			}

			// we end here.
			await SendFinal(serverConfirm).ConfigureAwait(false);

			return false;
		}

		protected virtual void PerformFinalValidation(TargettedMessageSet<ServerHandshakeConfirm<R>, R> serverConfirm) {
			// nothing to do
		}

		protected virtual async Task WaitFinalClientReady() {
			TargettedMessageSet<ClientReady<R>, R> finalNetworkMessageSet = await WaitSingleNetworkMessage<ClientReady<R>, TargettedMessageSet<ClientReady<R>, R>, R>().ConfigureAwait(false);

			await this.networkingService.ConnectionStore.FullyConfirmConnection(this.ClientConnection).ConfigureAwait(false);
		}

		protected virtual void SetReceivedPeerNodes(ClientHandshakeConfirm<R> message) {
			this.ClientConnection.SetPeerNodes(message.nodes);
		}

		protected virtual async Task<TargettedMessageSet<ServerHandshake<R>, R>> ProcessClientHandshake() {

			TargettedMessageSet<ServerHandshake<R>, R> serverHandshake = this.MessageFactory.CreateServerHandshakeSet(this.triggerMessage.Header);

			if(this.triggerMessage.Message.networkId != GlobalSettings.Instance.NetworkId) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.InvalidNetworkId;

				NLog.Default.Verbose("Sending handshake negative response, invalid network id");
				await SendFinal(serverHandshake).ConfigureAwait(false);

				return null;
			}

			// first, let's detect loopbacks
			if(ClientHandshakeWorkflow.ConnectingNonces.ContainsKey(serverHandshake.Message.nonce) || (ProtocolFactory.PROTOCOL_UUID == this.ClientId)) {
				// ok, we tried to connect to ourselves, lets cancel that
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.Loopback;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddLocalAddress(this.ClientConnection.NodeAddressInfo.Address);

				NLog.Default.Verbose("We received a connection from ourselves, let's cancel that. Sending handshake negative response; loopback connection");

				await SendFinal(serverHandshake).ConfigureAwait(false);

				return null;
			}

			if(!this.CheckPeerValid()) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.InvalidPeer;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfo);

				NLog.Default.Verbose("We received a connection from an invalid peer; rejecting connection");

				await SendFinal(serverHandshake).ConfigureAwait(false);

				return null;
			}

			// now determine if we are already connected to this peer
			if(this.networkingService.ConnectionStore.PeerConnected(this.ClientConnection.connection)) {
				// ok, we tried to connect to ourselves, lets cancel that
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.AlreadyConnected;

				// make sure we ignore it
				this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfo);

				NLog.Default.Verbose("We received a connection that we are already connected to. Sending handshake negative response; existing connection");

				await SendFinal(serverHandshake).ConfigureAwait(false);

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
					this.networkingService.ConnectionStore.AddIgnorePeerNode(this.ClientConnection.NodeAddressInfo);

					NLog.Default.Verbose("We received a connection that we are already connected to. closing");

					await SendFinal(serverHandshake).ConfigureAwait(false);

					return null;
				}
			}

			// ok, we just received a trigger, lets examine it
			NLog.Default.Verbose($"Received correlation id {this.CorrelationId}");

			// first, lets confirm their time definition is within acceptable range
			if(!this.timeService.WithinAcceptableRange(this.triggerMessage.Message.localTime, TimeSpan.FromSeconds(9))) {
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.TimeOutOfSync;

				NLog.Default.Verbose($"Sending handshake negative response, server time is out of sync with a delta of {DateTimeEx.CurrentTime - this.triggerMessage.Message.localTime.ToUniversalTime()}.");
				await SendFinal(serverHandshake).ConfigureAwait(false);

				return null;
			}

			// then we validate the client Scope, make sure its not too old
			if(!await TestClientVersion(serverHandshake).ConfigureAwait(false)) {
				NLog.Default.Verbose("The client has an invalid version");
				
				return null;
			}

			// ok, store its version
			this.ClientConnection.clientSoftwareVersion.SetVersion(this.triggerMessage.Message.clientSoftwareVersion);

			// lets take note of this peer's type
			this.ClientConnection.NodeInfo = this.triggerMessage.Message.nodeInfo;

			this.ClientConnection.SetGeneralSettings(this.triggerMessage.Message.generalSettings);

			// next thing, lets check if we still have room for more connections
			if(!await CheckStaturatedConnections(serverHandshake).ConfigureAwait(false)) {

				NLog.Default.Verbose("We are saturated. too many connections");

				return null;
			}
			
			// now we check the blockchains and the version they allow
			foreach(KeyValuePair<BlockchainType, ChainSettings> chainSetting in this.triggerMessage.Message.nodeInfo.GetChainSettings()) {

				// validate the blockchain valid minimum version
				this.ClientConnection.AddSupportedChain(chainSetting.Key, this.networkingService.IsChainVersionValid(chainSetting.Key, this.triggerMessage.Message.clientSoftwareVersion));

				// store the reported settings for later use
				this.ClientConnection.SetChainSettings(chainSetting.Key, chainSetting.Value);
			}
			
			if(!await CheckSupportedBlockchains(serverHandshake).ConfigureAwait(false)) {

				NLog.Default.Verbose("The client does not support the blockchains we want");

				return null;
			}

			// let's record what we got
			this.ClientConnection.NodeAddressInfo.RealPort = this.triggerMessage.Message.listeningPort;

			
			// and check if their connection port is true and available
			serverHandshake.Message.Connectable = await this.PerformCounterConnection().ConfigureAwait(false);

			NLog.Default.Verbose($"The client {this.ClientConnection.NodeAddressInfo.AdjustedAddress} is connectable: {serverHandshake.Message.Connectable}");
			
			this.networkingService.ConnectionStore.AddPeerReportedPublicIp(this.triggerMessage.Message.PerceivedIP, ConnectionStore.PublicIpSource.Peer);

			this.networkingService.ConnectionStore.AddChainSettings(this.ClientConnection.NodeAddressInfo, this.triggerMessage.Message.nodeInfo.GetChainSettings());

			// ok, we are ready to send a positive answer

			// lets tell them our own information
			serverHandshake.Message.clientSoftwareVersion.SetVersion(GlobalSettings.BlockchainCompatibilityVersion);
			
			// let's be nice and report it as we see it
			serverHandshake.Message.PerceivedIP = IPUtils.IPtoGuid(this.ClientConnection.NodeAddressInfo.Ip);

			// generate a random nonce and send it to the server
			serverHandshake.Message.nonce = this.GenerateRandomHandshakeNonce();

			serverHandshake.Message.nodeInfo = this.PeerType;

			serverHandshake.Message.generalSettings = this.networkingService.GeneralSettings;

			serverHandshake.Message.localTime = this.timeService.CurrentRealTime;
			
			return serverHandshake;
		}

		protected virtual async Task<bool?> PerformCounterConnection() {
			// now we counterconnect to determine if they are truly listening on their port
			try {
				this.ClientConnection.NodeAddressInfo.IsConnectable = await this.ClientConnection.connection.PerformCounterConnection(this.ClientConnection.NodeAddressInfo.RealPort).ConfigureAwait(false);

				return this.ClientConnection.NodeAddressInfo.IsConnectable;
			} catch {
				// nothing to do here, its just a fail and thus unconnectable
			}

			return false;
		}

		protected virtual bool CheckPeerValid() {

			return true;
		}

		protected virtual async Task<bool> CheckStaturatedConnections(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {

			AppSettingsBase.WhitelistedNode whiteList = GlobalSettings.ApplicationSettings.Whitelist.SingleOrDefault(e => e.Ip == this.ClientConnection.Ip);

			bool isSaturated = false;
			if (this.ClientConnection.NodeAddressInfo.PeerInfo.PeerType == Enums.PeerTypes.Mobile)
			{
				isSaturated = this.networkingService.ConnectionStore.MobileConnectionsSaturated;
				NLog.Default.Verbose($"{this.networkingService.ConnectionStore.ActiveMobileConnectionsCount} mobile connections already.");
			}
			
			if(isSaturated || this.networkingService.ConnectionStore.ConnectionsSaturated) {
				
				
				// well, we have no more room
				if((whiteList != null) && (whiteList.AcceptanceType == AppSettingsBase.WhitelistedNode.AcceptanceTypes.Always)) {
					// ok, thats fine, we will take this peer anyways, it is absolutely whitelisted
				} else {
					// too bad, we are saturated, we must refuse
					serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ConnectionsSaturated;

					NLog.Default.Verbose($"We received a connection {this.ClientConnection.NodeAddressInfo.PeerInfo.PeerType} but we already have too many ({this.networkingService.ConnectionStore.ActiveConnectionsCount} total, {this.networkingService.ConnectionStore.ActiveMobileConnectionsCount} mobile). rejecting nicely... ");

					await SendFinal(serverHandshake).ConfigureAwait(false);

					return false;
				}
			} else {
				if((whiteList != null) && (whiteList.AcceptanceType == AppSettingsBase.WhitelistedNode.AcceptanceTypes.WithRemainingSlots)) {
					// here also, we accept this peer no matter what
				}
			}

			return true;
		}

		protected virtual async Task<bool> CheckSupportedBlockchains(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {
			if(this.ClientConnection.NoSupportedChains || this.ClientConnection.NoValidChainVersion) {
				// ok, this is peer is just not usable, we have to disconnect
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ChainUnsupported;

				NLog.Default.Verbose("Sending handshake negative response, the peer does not support blockchains that interest us.");
				await SendFinal(serverHandshake).ConfigureAwait(false);

				return false;
			}

			return true;
		}

		protected virtual async Task<bool> TestClientVersion(TargettedMessageSet<ServerHandshake<R>, R> serverHandshake) {

			if(!GlobalSettings.BlockchainCompatibilityVersion.IsVersionAcceptable(this.triggerMessage.Message.clientSoftwareVersion)) {
				// we do not accept this version
				serverHandshake.Message.Status = ServerHandshake<R>.HandshakeStatuses.ClientVersionRefused;

				NLog.Default.Verbose("Sending handshake negative response");
				await SendFinal(serverHandshake).ConfigureAwait(false);

				return false;
			}

			return true;
		}

		protected virtual TargettedMessageSet<ServerHandshakeConfirm<R>, R> ProcessClientHandshakeConfirm(ClientHandshakeConfirm<R> handshakeConfirm) {
			TargettedMessageSet<ServerHandshakeConfirm<R>, R> serverConfirm = this.MessageFactory.CreateServerConfirmSet(this.triggerMessage.Header);
			NLog.Default.Verbose($"Received again correlation id {this.CorrelationId}");

			// lets send the server our list of nodeAddressInfo IPs

			// now we decide what kind of list to share with our peer. thser ones should get thser peers
			NodeSelectionHeuristicTools.NodeSelectionHeuristics heuristic = this.DetermineHeuristic();

			serverConfirm.Message.SetNodes(this.GetSharedPeerNodeList(this.ClientConnection.NodeInfo, heuristic));

			// generate a random nonce and send it to the server
			serverConfirm.Message.nonce = this.GenerateRandomConfirmNonce();

			return serverConfirm;
		}

		protected virtual NodeSelectionHeuristicTools.NodeSelectionHeuristics DetermineHeuristic() {
			return NodeSelectionHeuristicTools.NodeSelectionHeuristics.Default;
		}

		protected virtual NodeAddressInfoList GetSharedPeerNodeList(NodeInfo otherPeer, NodeSelectionHeuristicTools.NodeSelectionHeuristics heuristic = NodeSelectionHeuristicTools.NodeSelectionHeuristics.Default) {

			return this.networkingService.ConnectionStore.GetPeerNodeList(otherPeer, otherPeer.GetSupportedBlockchains(), heuristic, new[] {this.ClientConnection.NodeAddressInfo}.ToList(), true, 20);
		}

		protected virtual async Task AddValidConnection() {
			await networkingService.ConnectionStore.ConfirmConnection(ClientConnection).ConfigureAwait(false);
			NLog.Default.Verbose($"handshake with {this.ClientConnection.ScopedAdjustedIp} is now confirmed");
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
				return this.triggerMessage.Header.OriginatorId == clientHandshakeWorkflow.triggerMessage.Header.OriginatorId;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}