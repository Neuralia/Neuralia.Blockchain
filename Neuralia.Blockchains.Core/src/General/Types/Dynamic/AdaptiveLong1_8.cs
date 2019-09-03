namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     a long that will save from 1 to 8 bytes
	/// </summary>
	public class AdaptiveLong1_8 : AdaptiveNumber<long> {

		public static readonly int OFFSET = 3;
		public static readonly long MAX_VALUE = (long) (ulong.MaxValue >> OFFSET);

		public AdaptiveLong1_8() {
		}

		public AdaptiveLong1_8(long value) : base(value) {
		}

		public AdaptiveLong1_8(AdaptiveNumber<long> other) : base(other) {
		}

		public override long MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 1;
		protected override int MaximumByteCount => 8;

		public static explicit operator AdaptiveLong1_8(long value) {
			return new AdaptiveLong1_8(value);
		}

		protected override long ConvertTypeTo(ulong buffer) {
			return (long) buffer;
		}

		protected override ulong ConvertTypeFrom(long value) {
			return (ulong) value;
		}
	}
}