using System.Collections.Generic;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.Components;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public abstract class PeerListHandlingMessage<R> : NetworkMessage<R>
		where R : IRehydrationFactory {

		public readonly Dictionary<Enums.PeerTypes, NodeAddressInfoList> nodes = new Dictionary<Enums.PeerTypes, NodeAddressInfoList>();

		public PeerListHandlingMessage() {
			this.nodes.Add(Enums.PeerTypes.FullNode, new NodeAddressInfoList(Enums.PeerTypes.FullNode));
			this.nodes.Add(Enums.PeerTypes.SimpleMobile, new NodeAddressInfoList(Enums.PeerTypes.SimpleMobile));
			this.nodes.Add(Enums.PeerTypes.PowerMobile, new NodeAddressInfoList(Enums.PeerTypes.PowerMobile));
			this.nodes.Add(Enums.PeerTypes.SimpleSdk, new NodeAddressInfoList(Enums.PeerTypes.SimpleSdk));
			this.nodes.Add(Enums.PeerTypes.PowerSdk, new NodeAddressInfoList(Enums.PeerTypes.PowerSdk));
		}

		public void SetNodes(Dictionary<Enums.PeerTypes, NodeAddressInfoList> other) {
			this.nodes.Clear();

			foreach(var entry in other) {
				this.nodes[entry.Key] = entry.Value;
			}
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.nodes.Count);

			foreach(var entry in this.nodes) {
				dehydrator.Write((byte) entry.Key);

				entry.Value.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.nodes.Clear();
			int count = rehydrator.ReadByte();

			for(int i = 0; i < count; i++) {

				Enums.PeerTypes key = (Enums.PeerTypes) rehydrator.ReadByte();

				NodeAddressInfoList addressInfoList = new NodeAddressInfoList(key);
				addressInfoList.Rehydrate(rehydrator);

				this.nodes[key] = addressInfoList;
			}

		}
	}
}