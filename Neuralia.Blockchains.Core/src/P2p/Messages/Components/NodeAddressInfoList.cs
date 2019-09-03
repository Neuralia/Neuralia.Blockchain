using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.Components {
	/// <summary>
	///     a class representing a list of peer nodes
	/// </summary>
	public class NodeAddressInfoList : IBinarySerializable {
		private readonly List<NodeAddressInfo> nodes = new List<NodeAddressInfo>();

		public NodeAddressInfoList(Enums.PeerTypes peerType) {
			this.PeerType = peerType;
		}

		public NodeAddressInfoList(Enums.PeerTypes peerType, IEnumerable<NodeAddressInfo> nodes) {
			this.PeerType = peerType;
			this.SetNodes(nodes);
		}

		public NodeAddressInfoList(Enums.PeerTypes peerType, NodeAddressInfoList nodes) : this(peerType, nodes.Nodes) {

		}

		public Enums.PeerTypes PeerType { get; }

		public ImmutableList<NodeAddressInfo> Nodes => this.nodes.ToImmutableList();

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write((ushort) this.nodes.Count);

			foreach(NodeAddressInfo node in this.nodes) {
				node.Dehydrate(dehydrator);

			}
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {
			try {
				this.nodes.Clear();
				int count = rehydrator.ReadUShort();

				for(int i = 0; i < count; i++) {
					NodeAddressInfo nodeAddressInfo = NodeAddressInfo.CreateEmpty();
					nodeAddressInfo.Rehydrate(rehydrator);

					this.nodes.Add(nodeAddressInfo);
				}
			} catch(Exception ex) {
				throw new ApplicationException("Failed to rehydrate list of peer nodes", ex);
			}
		}

		public void SetNodes(IEnumerable<NodeAddressInfo> nodes) {
			this.nodes.Clear();
			this.AddNodes(nodes);
		}

		public void AddNodes(IEnumerable<NodeAddressInfo> nodes) {

			this.nodes.AddRange(nodes.Where(n => !this.nodes.Contains(n)));
		}

		public void AddNode(NodeAddressInfo node) {

			if(!this.nodes.Contains(node)) {
				this.nodes.Add(node);
			}
		}
	}
}