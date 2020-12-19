namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     a full long that will save from 2 to 9 bytes. the 1 to 9 version is preferable. delete this one?
	/// </summary>
	public class AdaptiveLong2_9 : AdaptiveNumber<long> {

		public static readonly int OFFSET = 3;
		public static readonly long MAX_VALUE = long.MaxValue;

		public AdaptiveLong2_9() {
		}

		public AdaptiveLong2_9(long value) : base(value) {
		}

		public AdaptiveLong2_9(AdaptiveNumber<long> other) : base(other) {
		}

		public override long MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 2;
		protected override int MaximumByteCount => 9;

		protected override long ConvertTypeTo(ulong buffer) {
			return (long) buffer;
		}

		protected override ulong ConvertTypeFrom(long value) {
			return (ulong) value;
		}

		public static explicit operator AdaptiveLong2_9(long value) {
			return new AdaptiveLong2_9(value);
		}
	}
}