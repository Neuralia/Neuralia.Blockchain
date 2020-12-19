namespace Neuralia.Blockchains.Core.General.Types.Dynamic {

	/// <summary>
	///     A special integer that can be saved on 1 or 2 bytes.
	/// </summary>
	public class AdaptiveShort1_2 : AdaptiveNumber<ushort> {

		public static readonly int OFFSET = 1;
		public static readonly ushort MAX_VALUE = (ushort) (ushort.MaxValue >> OFFSET);

		public AdaptiveShort1_2() {
		}

		public AdaptiveShort1_2(ushort value) : base(value) {
		}

		public AdaptiveShort1_2(AdaptiveNumber<ushort> other) : base(other) {
		}

		public override ushort MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;

		protected override int MinimumByteCount => 1;
		protected override int MaximumByteCount => 2;

		protected override ushort ConvertTypeTo(ulong buffer) {
			return (ushort) buffer;
		}

		protected override ulong ConvertTypeFrom(ushort value) {
			return value;
		}

		public static explicit operator AdaptiveShort1_2(byte value) {
			return new AdaptiveShort1_2(value);
		}

		public static explicit operator AdaptiveShort1_2(short value) {
			return new AdaptiveShort1_2((ushort) value);
		}

		public static implicit operator AdaptiveShort1_2(ushort value) {
			return new AdaptiveShort1_2(value);
		}

		public static implicit operator ushort(AdaptiveShort1_2 d) {
			return d.Value;
		}

		public static implicit operator int(AdaptiveShort1_2 d) {
			return d.Value;
		}
	}
}