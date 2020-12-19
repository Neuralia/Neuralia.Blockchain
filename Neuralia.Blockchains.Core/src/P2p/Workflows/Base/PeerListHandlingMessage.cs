using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public abstract class PeerListHandlingMessage<R> : NetworkMessage<R>
		where R : IRehydrationFactory {

		public readonly NodeAddressInfoList nodes = new NodeAddressInfoList();

		public void SetNodes(NodeAddressInfoList other) {
			this.nodes.Nodes.Clear();

			this.nodes.SetNodes(other.Nodes);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.nodes.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.nodes.Rehydrate(rehydrator);
		}
	}
}