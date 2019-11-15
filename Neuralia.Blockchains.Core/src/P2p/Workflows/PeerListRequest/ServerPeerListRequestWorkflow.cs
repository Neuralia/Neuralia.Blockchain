using System.Linq;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest {
	public class ServerPeerListRequestWorkflow<R> : ServerWorkflow<PeerListRequestTrigger<R>, PeerListRequestMessageFactory<R>, R>
		where R : IRehydrationFactory {
		public ServerPeerListRequestWorkflow(TriggerMessageSet<PeerListRequestTrigger<R>, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(triggerMessage, clientConnection, serviceSet) {
			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			this.PeerUnique = true;
		}

		protected override void PerformWork() {
			this.CheckShouldCancel();

			// ok, we just received a trigger, lets examine it

			var serverPeerListReply = this.MessageFactory.CreateServerPeerListRequestSet(this.triggerMessage.Header);

			Log.Verbose($"Received peer list request from peer {this.ClientConnection.ScoppedAdjustedIp}");

			ConnectionStore.PeerSelectionHeuristic heuristic = ConnectionStore.PeerSelectionHeuristic.Any;

			//TODO: review this to ensure proper selection
			if(this.triggerMessage.Message.PeerTypeSupport.HasValue) {
				if(this.triggerMessage.Message.PeerTypeSupport.Value.HasFlag(Enums.PeerTypeSupport.GossipBasic)) {
					heuristic = ConnectionStore.PeerSelectionHeuristic.Powers;
				}
			}

			// lets send the server our list of nodeAddressInfo IPs
			serverPeerListReply.Message.SetNodes(this.networkingService.ConnectionStore.GetPeerNodeList(heuristic, new[] {this.ClientConnection.NodeAddressInfoInfo}.ToList(), 20));

			if(!this.Send(serverPeerListReply)) {
				Log.Verbose($"Connection with peer  {this.ClientConnection.ScoppedAdjustedIp} was terminated");

				return;
			}

			Log.Verbose($"We sent {serverPeerListReply.Message.nodes.Select(n => n.Value.Nodes.Count).Sum()} other peers to peer {this.ClientConnection.ScoppedAdjustedIp} request");
		}

		protected override PeerListRequestMessageFactory<R> CreateMessageFactory() {
			return new PeerListRequestMessageFactory<R>(this.serviceSet);
		}

		protected override bool CompareOtherPeerId(IWorkflow other) {
			if(other is ServerPeerListRequestWorkflow<R> otherWorkflow) {
				return this.triggerMessage.Header.OriginatorId == otherWorkflow.triggerMessage.Header.OriginatorId;
			}

			return base.CompareOtherPeerId(other);
		}
	}
}