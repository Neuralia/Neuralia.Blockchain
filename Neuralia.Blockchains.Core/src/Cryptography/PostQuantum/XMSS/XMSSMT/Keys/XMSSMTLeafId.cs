using System;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public struct XMSSMTLeafId {
		public XMSSMTLeafId(long index, long tree, int layer) {
			this.Index = index;
			this.Tree = tree;
			this.Layer = layer;
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), this.Index, this.Tree, this.Layer);
		}

		public override string ToString() {
			return $"(index: {this.Index}, tree: {this.Tree}, layer: {this.Layer})";
		}

		public bool Equals(XMSSMTLeafId other) {
			return (this.Index == other.Index) && (this.Tree == other.Tree) && (this.Layer == other.Layer);
		}

		public override bool Equals(object obj) {
			return obj is XMSSMTLeafId other && this.Equals(other);
		}

		public static bool operator ==(XMSSMTLeafId a, XMSSMTLeafId b) {
			return a.Equals(b);
		}

		public static bool operator !=(XMSSMTLeafId a, XMSSMTLeafId b) {
			return !(a == b);
		}

		public long Index { get; }
		public long Tree { get; }
		public int Layer { get; }

		public static implicit operator XMSSMTLeafId((long index, long tree, int layer) d) {
			(long index, long tree, int layer) = d;

			return new XMSSMTLeafId(index, tree, layer);
		}

		public static implicit operator XMSSMTreeId(XMSSMTLeafId d) {
			return new XMSSMTreeId(d.Tree, d.Layer);
		}

		public static implicit operator XMSSMTLeafId((long index, XMSSMTreeId id) d) {
			(long index, XMSSMTreeId id) = d;

			return new XMSSMTLeafId(index, id.Tree, id.Layer);
		}
	}
}