using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1 {
	public class PeerListRequestTrigger<R> : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public Enums.PeerTypeSupport? PeerTypeSupport { get; set; } = Enums.PeerTypeSupport.None;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.PeerTypeSupport == null);

			if(this.PeerTypeSupport != null) {
				dehydrator.Write((ushort) this.PeerTypeSupport.Value);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			bool isNull = rehydrator.ReadBool();

			if(!isNull) {
				this.PeerTypeSupport = (Enums.PeerTypeSupport) rehydrator.ReadUShort();
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (PeerListRequestMessageFactory<R>.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.PEER_LIST_REQUEST;
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());

			return nodesList;
		}
	}
}