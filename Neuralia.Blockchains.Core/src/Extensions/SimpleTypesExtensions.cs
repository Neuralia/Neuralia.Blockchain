namespace Neuralia.Blockchains.Core.Extensions {
	public static class SimpleTypesExtensions {
		/// <summary>
		///     here we want to ensure that we remove the trailing zeros. 0 and 0.00000 should be equal to the same thing, but they
		///     are not if we dont normalize.
		/// </summary>
		/// <param name="entry"></param>
		/// <returns></returns>
		public static decimal Normalize(this decimal entry) {
			int[] bits = decimal.GetBits(entry);

			uint lo = (uint) bits[0];
			uint mid = (uint) bits[1];
			uint hi = (uint) bits[2];
			uint flags = (uint) bits[3];

			long sign = flags & (1 << 31);
			uint exponent = (flags >> 16) & 0x1f;

			uint MoveDigitIndex(uint higherBits, uint lowerBits, out uint remainder) {
				ulong total = ((ulong) higherBits << 32) | lowerBits;

				remainder = (uint) (total % 10L);

				return (uint) (total / 10L);
			}

			while((((((hi % 5) * 6) + ((mid % 5) * 6) + lo) % 10) == 0) && (exponent > 0)) {
				uint remainder = 0;

				if(hi != 0) {
					hi = MoveDigitIndex(0, hi, out remainder);
				}

				if(mid != 0) {
					mid = MoveDigitIndex(remainder, mid, out remainder);
				}

				lo = MoveDigitIndex(remainder, lo, out remainder);
				--exponent;
			}

			return new decimal(new[] {(int) lo, (int) mid, (int) hi, (int) ((exponent << 16) | sign)});
		}
	}
}