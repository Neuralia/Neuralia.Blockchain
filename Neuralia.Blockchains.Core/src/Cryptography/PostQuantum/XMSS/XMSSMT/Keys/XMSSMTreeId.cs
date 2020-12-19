using System;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public struct XMSSMTreeId {
		public XMSSMTreeId(long tree, int layer) {
			this.Tree = tree;
			this.Layer = layer;
		}

		public override string ToString() {
			return $"(tree: {this.Tree}, layer: {this.Layer})";
		}

		public bool Equals(XMSSMTreeId other) {
			return (this.Tree == other.Tree) && (this.Layer == other.Layer);
		}

		public override bool Equals(object obj) {
			return obj is XMSSMTreeId other && this.Equals(other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), this.Tree, this.Layer);
		}

		public static bool operator ==(XMSSMTreeId a, XMSSMTreeId b) {
			return a.Equals(b);
		}

		public static bool operator !=(XMSSMTreeId a, XMSSMTreeId b) {
			return !(a == b);
		}

		public long Tree { get; }
		public int Layer { get; }

		public static implicit operator XMSSMTreeId((long tree, int layer) d) {
			return new XMSSMTreeId(d.tree, d.layer);
		}

	}
}