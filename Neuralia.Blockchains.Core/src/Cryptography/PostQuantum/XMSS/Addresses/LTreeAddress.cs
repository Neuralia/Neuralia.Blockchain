using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses {
	public class LTreeAddress : CommonAddress {

		private int ltreeAddress;
		private int treeHeight;
		private int treeIndex;

		public LTreeAddress(int layerAddress, long treeAddress, int keyAndMask, int lTreeAddress, byte treeHeight, int treeIndex) : base(layerAddress, treeAddress, keyAndMask, AddressTypes.LTree) {
			this.LtreeAddress = lTreeAddress;
			this.TreeHeight = treeHeight;
			this.TreeIndex = treeIndex;
		}

		public LTreeAddress() : base(AddressTypes.LTree) {

		}

		public int LtreeAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.ltreeAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.ltreeAddress != value) {
					this.ltreeAddress = value;
					this.SetBytesField(value, 16);
				}
			}
		}

		public int TreeHeight {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.treeHeight;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.treeHeight != value) {
					this.treeHeight = value;
					this.SetBytesField(value, 20);
				}
			}
		}

		public int TreeIndex {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.treeIndex;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.treeIndex != value) {
					this.treeIndex = value;
					this.SetBytesField(value, 24);
				}
			}
		}

		public static implicit operator XMSSNodeId(LTreeAddress d) {
			return (d.TreeIndex, d.TreeHeight);
		}

		protected bool Equals(LTreeAddress other) {
			return base.Equals(other) && (this.ltreeAddress == other.ltreeAddress) && (this.treeHeight == other.treeHeight) && (this.treeIndex == other.treeIndex);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((LTreeAddress) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ this.ltreeAddress;
				hashCode = (hashCode * 397) ^ this.treeHeight;
				hashCode = (hashCode * 397) ^ this.treeIndex;

				return hashCode;
			}
		}

		public static bool operator ==(LTreeAddress a, LTreeAddress b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(LTreeAddress a, LTreeAddress b) {
			return !(a == b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Initialize(CommonAddress adrs) {

			this.Reset();
			base.Initialize(adrs);

			if(!(adrs is LTreeAddress otherAdrs)) {
				return;
			}

			this.LtreeAddress = otherAdrs.LtreeAddress;
			this.TreeHeight = otherAdrs.TreeHeight;
			this.TreeIndex = otherAdrs.TreeIndex;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Reset() {
			base.Reset();

			this.LtreeAddress = 0;
			this.TreeHeight = 0;
			this.TreeIndex = 0;
		}
	}
}