using System;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses {
	public abstract class CommonAddress : IDisposableExtended {

		private readonly ByteArray bytes;
		private int keyAndMask;
		private int layerAddress;
		private long treeAddress;
		private AddressTypes type;

		public CommonAddress(AddressTypes type) {

			this.bytes = ByteArray.Create(48);
			this.Type = type;
		}

		public CommonAddress(int layerAddress, long treeAddress, int keyAndMask, AddressTypes type) : this(type) {

			this.LayerAddress = layerAddress;
			this.TreeAddress = treeAddress;
			this.KeyAndMask = keyAndMask;

		}

		public int LayerAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.layerAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.layerAddress != value) {
					this.layerAddress = value;
					this.SetBytesField(value, 0);
				}
			}
		}

		public long TreeAddress {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.treeAddress;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.treeAddress != value) {
					this.treeAddress = value;
					this.SetBytesField(value, 4);
				}
			}
		}

		public AddressTypes Type {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.type;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private set {
				if((this.type == value) || (value == 0)) {
					return;
				}

				this.type = value;
				this.SetBytesField((int) value, 12);
			}
		}

		public int KeyAndMask {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.keyAndMask;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				if(this.keyAndMask != value) {
					this.keyAndMask = value;
					this.SetBytesField(value, 40);
				}
			}
		}

		protected bool Equals(CommonAddress other) {
			return (this.layerAddress == other.layerAddress) && (this.treeAddress == other.treeAddress) && (this.keyAndMask == other.keyAndMask) && (this.type == other.type);
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

			return this.Equals((CommonAddress) obj);
		}

		public static bool operator ==(CommonAddress a, CommonAddress b) {

			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(CommonAddress a, CommonAddress b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), this.layerAddress, this.treeAddress, this.keyAndMask, (int) this.type);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void Initialize(CommonAddress adrs) {
			this.LayerAddress = adrs.LayerAddress;
			this.TreeAddress = adrs.TreeAddress;
			this.KeyAndMask = adrs.KeyAndMask;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void SetBytesField(int value, int offset) {

			TypeSerializerFlexible.Serialize(value, this.bytes.Span.Slice(offset, sizeof(int)), TypeSerializerFlexible.Direction.BigEndian);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void SetBytesField(long value, int offset) {

			TypeSerializerFlexible.Serialize(value, this.bytes.Span.Slice(offset, sizeof(long)), TypeSerializerFlexible.Direction.BigEndian);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ByteArray ToByteArray() {

			return this.bytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void Reset() {
			this.LayerAddress = 0;
			this.TreeAddress = 0;
			this.KeyAndMask = 0;
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.bytes?.Dispose();
			}

			this.IsDisposed = true;
		}

		~CommonAddress() {
			this.Dispose(false);
		}

	#endregion

	}
}