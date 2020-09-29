using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses {
	public class HashTreeAddress : CommonAddress {

		private int treeHeight;
		private int treeIndex;

		public HashTreeAddress(int layerAddress, long treeAddress, int keyAndMask, byte treeHeight, int treeIndex) : base(layerAddress, treeAddress, keyAndMask, AddressTypes.HashTree) {
			this.TreeHeight = treeHeight;
			this.TreeIndex = treeIndex;
		}

		public HashTreeAddress() : base(AddressTypes.HashTree) {

		}

		internal int Padding => 0;

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

		public static implicit operator XMSSNodeId(HashTreeAddress d) {
			return (d.TreeIndex, d.TreeHeight);
		}

		protected bool Equals(HashTreeAddress other) {
			return base.Equals(other) && (this.treeHeight == other.treeHeight) && (this.treeIndex == other.treeIndex);
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

			return this.Equals((HashTreeAddress) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ this.treeHeight;
				hashCode = (hashCode * 397) ^ this.treeIndex;

				return hashCode;
			}
		}

		public static bool operator ==(HashTreeAddress a, HashTreeAddress b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(HashTreeAddress a, HashTreeAddress b) {
			return !(a == b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Initialize(CommonAddress adrs) {

			this.Reset();
			base.Initialize(adrs);

			if(!(adrs is HashTreeAddress otherAdrs)) {
				return;
			}

			this.TreeHeight = otherAdrs.TreeHeight;
			this.TreeIndex = otherAdrs.TreeIndex;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Reset() {
			base.Reset();

			this.TreeHeight = 0;
			this.TreeIndex = 0;
		}
	}
}