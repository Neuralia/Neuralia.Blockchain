namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	public struct XMSSNodeId {
		public XMSSNodeId(int index, int height) {
			this.Index = index;
			this.Height = height;
		}

		public override string ToString() {
			return $"(index: {this.Index}, height: {this.Height})";
		}

		public bool Equals(XMSSNodeId other) {
			return (this.Index == other.Index) && (this.Height == other.Height);
		}

		public override bool Equals(object obj) {
			return obj is XMSSNodeId other && this.Equals(other);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.Index;
				hashCode = (hashCode * 397) ^ this.Height;

				return hashCode;
			}
		}

		public static bool operator ==(XMSSNodeId a, XMSSNodeId b) {
			return a.Equals(b);
		}

		public static bool operator !=(XMSSNodeId a, XMSSNodeId b) {
			return !(a == b);
		}

		public int Index { get; }
		public int Height { get; }

		public static implicit operator XMSSNodeId((int index, int height) d) {
			return new XMSSNodeId(d.index, d.height);
		}

	}
}