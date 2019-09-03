using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1 {
	public class PeerListRequestServerReply<R> : PeerListHandlingMessage<R>
		where R : IRehydrationFactory {

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (PeerListRequestMessageFactory<R>.SERVER_REPLY_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.PEER_LIST_REQUEST;
		}
	}
}