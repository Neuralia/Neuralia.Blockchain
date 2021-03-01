using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest {
	
	public interface IClientPeerListRequestWorkflow<R> : IClientWorkflow<NullMessageFactory<R>, R>
		where R : IRehydrationFactory{
		TimeSpan Latency { get; }
	}
	
	public class ClientPeerListRequestWorkflow<R> : ClientWorkflow<PeerListRequestMessageFactory<R>, R>, IClientPeerListRequestWorkflow<R>
		where R : IRehydrationFactory {
		public readonly PeerConnection peerConnection;
		public TimeSpan Latency { get; private set; } = TimeSpan.MinValue;

		public ClientPeerListRequestWorkflow(PeerConnection peerConnection, ServiceSet<R> serviceSet) : base(serviceSet) {
			this.peerConnection = peerConnection;

			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			this.PeerUnique = true;
		}

		protected override async Task<bool> PerformWork(LockContext lockContext) {
			this.CheckShouldCancel();

			TriggerMessageSet<PeerListRequestTrigger<R>, R> peerListRequestTrigger = this.MessageFactory.CreatePeerListRequestWorkflowTriggerSet(this.CorrelationId);

			NLog.Default.Verbose($"Sending peer list request to peer {this.peerConnection.ScopedAdjustedIp}");

			var startTime = DateTimeEx.CurrentTime;
			
			if(!await SendMessage(peerConnection, peerListRequestTrigger).ConfigureAwait(false)) {
				NLog.Default.Verbose($"Connection with peer  {this.peerConnection.ScopedAdjustedIp} was terminated");

				return false;
			}

			TargettedMessageSet<PeerListRequestServerReply<R>, R> serverPeerListRequest = await WaitSingleNetworkMessage<PeerListRequestServerReply<R>, TargettedMessageSet<PeerListRequestServerReply<R>, R>, R>().ConfigureAwait(false);

			this.peerConnection.connection.AddLatency(startTime);

			// take the peer nodes and update our system
			this.networkingService.ConnectionStore.UpdatePeerNodes(this.peerConnection, serverPeerListRequest.Message.nodes);

			NLog.Default.Verbose($"Received {serverPeerListRequest.Message.nodes.Nodes.Count} peers from peer {this.peerConnection.ScopedAdjustedIp}");

			return true;
		}

		protected override PeerListRequestMessageFactory<R> CreateMessageFactory() {
			return new PeerListRequestMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ClientPeerListRequestWorkflow<R> otherWorkflow) {
				return this.peerConnection.ClientUuid == otherWorkflow.peerConnection.ClientUuid;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}