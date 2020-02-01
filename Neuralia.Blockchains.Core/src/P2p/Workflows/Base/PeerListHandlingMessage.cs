using System.Collections.Generic;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public abstract class PeerListHandlingMessage<R> : NetworkMessage<R>
		where R : IRehydrationFactory {

		public readonly NodeAddressInfoList nodes = new NodeAddressInfoList();

		public PeerListHandlingMessage() {

		}

		public void SetNodes(NodeAddressInfoList other) {
			this.nodes.Nodes.Clear();

			this.nodes.SetNodes(other.Nodes);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			nodes.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			nodes.Rehydrate(rehydrator);
		}
	}
}