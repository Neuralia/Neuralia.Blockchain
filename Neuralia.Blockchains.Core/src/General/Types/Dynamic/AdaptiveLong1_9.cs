namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     a full long that will save from 1 to 9 bytes. This version uses an extra big (0x8) to indicate that we are in
	///     single byte mode.
	///     if set, we use all other bits of the single byte. This version can store up to 127 on a single byte.
	/// </summary>
	/// <remarks>This version is optimized for very small numbers, ideally below 128. </remarks>
	public class AdaptiveLong1_9 : AdaptiveNumber<long> {

		private const byte SPECIAL_FLAG = 0x8;
		private const byte MAXIMUM_SINGLE_BYTE_VALUE = 0x80;

		private const byte LOWER_BYTES = 0x7;
		private const byte HIGHER_BYTES = 0x78;

		private const byte REBUILD_HIGHER_BYTES = 0xF0;

		public static readonly int OFFSET = 4;
		public static readonly long MAX_VALUE = long.MaxValue;

		public AdaptiveLong1_9() {

		}

		public AdaptiveLong1_9(long value) : base(value) {

		}

		public AdaptiveLong1_9(AdaptiveNumber<long> other) : base(other) {

		}

		public override long MaxValue => MAX_VALUE;
		protected override int Offset => OFFSET;
		protected override int MinimumByteCount => 2;
		protected override int MaximumByteCount => 9;

		public static implicit operator long?(AdaptiveLong1_9 value) {
			if(value == null) {
				return null;
			}

			return value.Value;
		}

		public static implicit operator long(AdaptiveLong1_9 value) {

			if(value == null) {
				return 0;
			}

			return value.Value;
		}

		public static implicit operator AdaptiveLong1_9(byte value) {
			return new AdaptiveLong1_9(value);
		}

		public static implicit operator AdaptiveLong1_9(short value) {
			return new AdaptiveLong1_9(value);
		}

		public static implicit operator AdaptiveLong1_9(ushort value) {
			return new AdaptiveLong1_9(value);
		}

		public static implicit operator AdaptiveLong1_9(int value) {
			return new AdaptiveLong1_9(value);
		}

		public static implicit operator AdaptiveLong1_9(uint value) {
			return new AdaptiveLong1_9(value);
		}

		public static implicit operator AdaptiveLong1_9(long value) {
			return new AdaptiveLong1_9(value);
		}

		private bool HasSpecialFlag(byte entry) {
			return (entry & SPECIAL_FLAG) != 0;
		}

		private bool HasSpecialFlag(long entry) {
			return this.HasSpecialFlag(this.ToByte(entry));
		}

		private byte ToByte(long entry) {
			return (byte) (entry & 0xFF);
		}

		public override byte[] GetShrunkBytes() {
			long workingId = this.Value;

			byte[] shrunkBytes = null;

			if(workingId < MAXIMUM_SINGLE_BYTE_VALUE) {
				// it will fit on a single byte, lets perform the swaps
				shrunkBytes = new byte[1];

				// first set the magic flag
				shrunkBytes[0] |= SPECIAL_FLAG;

				// now fit the data around it. first the lower bytes
				byte lowers = (byte) (workingId & LOWER_BYTES);
				byte highers = (byte) (workingId & HIGHER_BYTES);

				shrunkBytes[0] |= lowers;
				shrunkBytes[0] |= (byte) (highers << 1);

			} else {
				// ok, it will take more bytes than one. lets perform it. first, we remove the maximum from the lot

				workingId -= MAXIMUM_SINGLE_BYTE_VALUE;
				shrunkBytes = this.BuildShrunkBytes(workingId);
			}

			return shrunkBytes;
		}

		public override (int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) ReadByteSpecs(byte firstByte) {
			// set the buffer, so we can read the serialization 
			if(this.HasSpecialFlag(firstByte)) {
				// its a single byte :)
				return this.AdjustSerializationByteSize(1);
			}

			return base.ReadByteSpecs(firstByte);
		}

		protected override ulong PrepareBuffer(ulong buffer, byte firstByte) {

			if(this.HasSpecialFlag(firstByte)) {
				// its a single byte, lets rebuild the number
				byte lowers = (byte) (firstByte & LOWER_BYTES);
				byte highers = (byte) (firstByte & REBUILD_HIGHER_BYTES);

				byte value = lowers;
				value |= (byte) (highers >> 1);

				return value;
			}

			buffer += MAXIMUM_SINGLE_BYTE_VALUE;

			return buffer;
		}

		protected override long ConvertTypeTo(ulong buffer) {
			return (long) buffer;
		}

		protected override ulong ConvertTypeFrom(long value) {
			return (ulong) value;
		}

        public long? ToNullable()
        {
            throw new System.NotImplementedException();
        }
    }
}