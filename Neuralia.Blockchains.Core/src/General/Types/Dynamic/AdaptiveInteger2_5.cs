namespace Neuralia.Blockchains.Core.General.Types.Dynamic {

	/// <summary>
	///     A special integer that can be saved on 2,3,4 or 5 bytes.
	/// </summary>
	public class AdaptiveInteger2_5 : AdaptiveNumber<uint> {
		public static readonly int OFFSET = 2;
		public static readonly uint MAX_VALUE = uint.MaxValue;

		public AdaptiveInteger2_5() {
		}

		public AdaptiveInteger2_5(uint value) : base(value) {
		}

		public AdaptiveInteger2_5(AdaptiveNumber<uint> other) : base(other) {
		}

		public override uint MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 2;
		protected override int MaximumByteCount => 5;

		protected override uint ConvertTypeTo(ulong buffer) {
			return (uint) buffer;
		}

		protected override ulong ConvertTypeFrom(uint value) {
			return value;
		}

		public static explicit operator AdaptiveInteger2_5(uint value) {
			return new AdaptiveInteger2_5(value);
		}
	}
}