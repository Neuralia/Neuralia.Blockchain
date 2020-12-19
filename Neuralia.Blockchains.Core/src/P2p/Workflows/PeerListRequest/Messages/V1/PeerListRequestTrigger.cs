using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1 {
	public class PeerListRequestTrigger<R> : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public NodeInfo NodeInfo { get; set; } = new NodeInfo();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.NodeInfo.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.NodeInfo.Rehydrate(rehydrator);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (PeerListRequestMessageFactory<R>.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.PEER_LIST_REQUEST;
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			nodeList.Add(this.NodeInfo);

			return nodeList;
		}
	}
}