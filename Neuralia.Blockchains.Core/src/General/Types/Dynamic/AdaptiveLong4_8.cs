namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	public class AdaptiveLong4_8 : AdaptiveNumber<long> {
		public static readonly int OFFSET = 2;
		public static readonly long MAX_VALUE = (long) (ulong.MaxValue >> OFFSET);

		public AdaptiveLong4_8() {
		}

		public AdaptiveLong4_8(long value) : base(value) {
		}

		public AdaptiveLong4_8(AdaptiveNumber<long> other) : base(other) {
		}

		public override long MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 4;
		protected override int MaximumByteCount => 8;

		protected override long ConvertTypeTo(ulong buffer) {
			return (long) buffer;
		}

		protected override ulong ConvertTypeFrom(long value) {
			return (ulong) value;
		}

		public static explicit operator AdaptiveLong4_8(long value) {
			return new AdaptiveLong4_8(value);
		}

		protected override (int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) AdjustSerializationByteSize(int value) {
			(int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) = base.AdjustSerializationByteSize(value);

			// we combine bytes 7 and 8, so both take 8 bytes.
			if(serializationByteSize == 7) {
				serializationByteSize = 8;
			}

			if(adjustedSerializationByteExtraSize == 3) {
				adjustedSerializationByteExtraSize = 4;
			}

			if(bitValues == 4) {
				bitValues = 3;
			}

			return (serializationByteSize, adjustedSerializationByteExtraSize, bitValues);
		}
	}
}