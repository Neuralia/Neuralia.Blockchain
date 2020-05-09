using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Specialized {

	/// <summary>
	///     a dual value from 0 to 15 each stored in a single byte
	/// </summary>
	public class DualByte : ITreeHashable, IBinarySerializable, IEquatable<DualByte> {

		private const uint MASK = 0xF;
		private const int MASK_OFFSET = 4;

		public DualByte() {

		}

		public DualByte(byte high, byte low) {
			this.High = high;
			this.Low = low;
		}

		public byte Data { get; private set; }

		public byte Low {
			get => (byte) (this.Data & MASK);
			set {
				if(value > MASK) {
					throw new ArgumentException($"Value must be smaller or equal to max value {MASK}", nameof(value));
				}

				this.Data = (byte) ((this.Data & ~MASK) | value);
			}
		}

		public byte High {
			get => (byte) ((this.Data >> MASK_OFFSET) & MASK);
			set {
				if(value > MASK) {
					throw new ArgumentException($"Value must be smaller or equal to max value {MASK}", nameof(value));
				}

				this.Data = (byte) ((this.Data & ~(MASK << MASK_OFFSET)) | (value << MASK_OFFSET));
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.Data = rehydrator.ReadByte();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Data);
		}

		public bool Equals(DualByte other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.Data == other.Data;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Low);
			nodeList.Add(this.High);

			return nodeList;
		}

		public static bool operator ==(DualByte left, DualByte right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals(right);
		}

		public static bool operator !=(DualByte left, DualByte right) {
			return !(left == right);
		}
	}
}