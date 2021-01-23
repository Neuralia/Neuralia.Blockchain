using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest {
	public class ServerPeerListRequestWorkflow<R> : ServerWorkflow<PeerListRequestTrigger<R>, PeerListRequestMessageFactory<R>, R>
		where R : IRehydrationFactory {
		public ServerPeerListRequestWorkflow(TriggerMessageSet<PeerListRequestTrigger<R>, R> triggerMessage, PeerConnection clientConnection, ServiceSet<R> serviceSet) : base(triggerMessage, clientConnection, serviceSet) {
			// allow only one per peer at a time
			this.ExecutionMode = Workflow.ExecutingMode.Single;

			this.PeerUnique = true;
		}

		protected override async Task PerformWork(LockContext lockContext) {
			this.CheckShouldCancel();

			// ok, we just received a trigger, lets examine it

			TargettedMessageSet<PeerListRequestServerReply<R>, R> serverPeerListReply = this.MessageFactory.CreateServerPeerListRequestSet(this.triggerMessage.Header);

			NLog.Default.Verbose($"Received peer list request from peer {this.ClientConnection.ScopedAdjustedIp}");

			// lets send the server our list of nodeAddressInfo IPs
			serverPeerListReply.Message.SetNodes(this.networkingService.ConnectionStore.GetPeerNodeList(this.triggerMessage.Message.NodeInfo
				, this.triggerMessage.Message.NodeInfo.GetSupportedBlockchains()
				, NodeSelectionHeuristicTools.NodeSelectionHeuristics.Default, new[] {this.ClientConnection.NodeAddressInfo}.ToList()
				, true, 20));

			if(!this.Send(serverPeerListReply)) {
				NLog.Default.Verbose($"Connection with peer  {this.ClientConnection.ScopedAdjustedIp} was terminated");

				return;
			}

			NLog.Default.Verbose($"We sent {serverPeerListReply.Message.nodes.Nodes.Count} other peers to peer {this.ClientConnection.ScopedAdjustedIp} request");
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