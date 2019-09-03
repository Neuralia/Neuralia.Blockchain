namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public struct XMSSMTLeafId {
		public XMSSMTLeafId(long index, int tree, int layer) {
			this.Index = index;
			this.Tree = tree;
			this.Layer = layer;
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.Index.GetHashCode();
				hashCode = (hashCode * 397) ^ this.Tree;
				hashCode = (hashCode * 397) ^ this.Layer;

				return hashCode;
			}
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
		public int Tree { get; }
		public int Layer { get; }

		public static implicit operator XMSSMTLeafId((int index, int tree, int layer) d) {
			return new XMSSMTLeafId(d.index, d.tree, d.layer);
		}

		public static implicit operator XMSSMTreeId(XMSSMTLeafId d) {
			return new XMSSMTreeId(d.Tree, d.Layer);
		}

		public static implicit operator XMSSMTLeafId((int index, XMSSMTreeId id) d) {
			return new XMSSMTLeafId(d.index, d.id.Tree, d.id.Layer);
		}

		public static implicit operator XMSSMTLeafId((long index, XMSSMTreeId id) d) {
			return new XMSSMTLeafId(d.index, d.id.Tree, d.id.Layer);
		}
	}
}