namespace Neuralia.Blockchains.Core.General.Types.Dynamic {

	/// <summary>
	///     A special integer that can be saved on 1,2,3,4 bytes.
	/// </summary>
	public class AdaptiveInteger1_4 : AdaptiveNumber<uint> {
		public static readonly int OFFSET = 2;
		public static readonly uint MAX_VALUE = uint.MaxValue >> OFFSET;

		public AdaptiveInteger1_4() {
		}

		public AdaptiveInteger1_4(uint value) : base(value) {
		}

		public AdaptiveInteger1_4(AdaptiveNumber<uint> other) : base(other) {
		}

		public override uint MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 1;
		protected override int MaximumByteCount => 4;

		public static explicit operator AdaptiveInteger1_4(uint value) {
			return new AdaptiveInteger1_4(value);
		}

		protected override uint ConvertTypeTo(ulong buffer) {
			return (uint) buffer;
		}

		protected override ulong ConvertTypeFrom(uint value) {
			return value;
		}
	}
}