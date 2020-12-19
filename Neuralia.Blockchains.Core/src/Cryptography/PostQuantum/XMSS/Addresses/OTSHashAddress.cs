using System;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses {
	public class OtsHashAddress : CommonAddress {
		
		private int chainAddress;
		private int hashAddress;

		private long otsAddress;

		public OtsHashAddress(int layerAddress, long treeAddress, int keyAndMask, long otsAddress, int chainAddress, int hashAddress) : base(layerAddress, treeAddress, keyAndMask, AddressTypes.OTS) {
			this.OtsAddress = otsAddress;
			this.ChainAddress = chainAddress;
			this.HashAddress = hashAddress;
		}

		public OtsHashAddress() : base(AddressTypes.OTS) {

		}

		public long OtsAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.otsAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.otsAddress != value) {
					this.otsAddress = value;
					this.SetBytesField(value, 16);
				}
			}
		}

		public int ChainAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.chainAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.chainAddress != value) {
					this.chainAddress = value;
					this.SetBytesField((long)value, 24);
				}
			}
		}

		public int HashAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.hashAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.hashAddress != value) {
					this.hashAddress = value;
					this.SetBytesField((long)value, 32);
				}
			}
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), this.chainAddress, this.hashAddress, this.otsAddress);
		}

		public static implicit operator XMSSNodeId(OtsHashAddress d) {
			return (d.OtsAddress, (byte) d.TreeAddress);
		}

		public static implicit operator XMSSMTLeafId(OtsHashAddress d) {
			return (d.OtsAddress, (long) d.TreeAddress, d.LayerAddress);
		}

		public static implicit operator XMSSMTreeId(OtsHashAddress d) {
			return ((long) d.TreeAddress, d.LayerAddress);
		}

		protected bool Equals(OtsHashAddress other) {
			return base.Equals(other) && (this.otsAddress == other.otsAddress) && (this.chainAddress == other.chainAddress) && (this.hashAddress == other.hashAddress);
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

			return this.Equals((OtsHashAddress) obj);
		}
		
		public static bool operator ==(OtsHashAddress a, OtsHashAddress b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(OtsHashAddress a, OtsHashAddress b) {
			return !(a == b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Initialize(CommonAddress adrs) {

			this.Reset();
			base.Initialize(adrs);

			if(!(adrs is OtsHashAddress otherAdrs)) {
				return;
			}

			this.OtsAddress = otherAdrs.OtsAddress;
			this.ChainAddress = otherAdrs.ChainAddress;
			this.HashAddress = otherAdrs.HashAddress;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Reset() {
			base.Reset();

			this.OtsAddress = 0;
			this.ChainAddress = 0;
			this.HashAddress = 0;
		}
	}
}