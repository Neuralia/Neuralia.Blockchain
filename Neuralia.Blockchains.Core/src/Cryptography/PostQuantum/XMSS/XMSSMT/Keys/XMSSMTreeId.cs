namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public struct XMSSMTreeId {
		public XMSSMTreeId(int tree, int layer) {
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
			unchecked {
				int hashCode = this.Tree;
				hashCode = (hashCode * 397) ^ this.Layer;

				return hashCode;
			}
		}

		public static bool operator ==(XMSSMTreeId a, XMSSMTreeId b) {
			return a.Equals(b);
		}

		public static bool operator !=(XMSSMTreeId a, XMSSMTreeId b) {
			return !(a == b);
		}

		public int Tree { get; }
		public int Layer { get; }

		public static implicit operator XMSSMTreeId((int tree, int layer) d) {
			return new XMSSMTreeId(d.tree, d.layer);
		}

	}
}