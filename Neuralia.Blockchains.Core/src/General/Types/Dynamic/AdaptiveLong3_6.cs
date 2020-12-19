namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	public class AdaptiveLong3_6 : AdaptiveNumber<long> {
		public static readonly int OFFSET = 2;
		public static readonly long MAX_VALUE = (long) (0xFFFF_FFFF_FFFFUL >> OFFSET);

		public AdaptiveLong3_6() {
		}

		public AdaptiveLong3_6(long value) : base(value) {
		}

		public AdaptiveLong3_6(AdaptiveNumber<long> other) : base(other) {
		}

		public override long MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 3;
		protected override int MaximumByteCount => 6;

		public static explicit operator AdaptiveLong3_6(long value) {
			return new AdaptiveLong3_6(value);
		}

		protected override long ConvertTypeTo(ulong buffer) {
			return (long) buffer;
		}

		protected override ulong ConvertTypeFrom(long value) {
			return (ulong) value;
		}
	}
}