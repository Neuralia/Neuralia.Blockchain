using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LiteDB;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Network.ReadingContexts;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     a dynamic decimal that can take up to 8 bytes above the point and 8 bytes below the point. +one metadata byte and
	///     one possible extra byte if there are leading zeros either in the integral or the fraction.
	///     . so the maximum size it will take is 18 bytes.
	/// </summary>
	public class AdaptiveDecimal : ITreeHashable, IBinarySerializable, IEquatable<AdaptiveDecimal>, IComparable<decimal>, IComparable<AdaptiveDecimal> {

		private const long BYTES_7 = 0xFFFFFFFFFFFFFF;
		private const int MAX_DECIMALS = 16;

		private const byte INTEGRAL_MASK = 0x7;
		private const byte FRACTION_MASK = 0x38;
		private const byte ZEROS_MASK = 0x40;
		private const byte NEGATIVE_MASK = 0x80;
		private decimal value;

		public AdaptiveDecimal() {

		}

		public AdaptiveDecimal(decimal value) {

			this.Value = value;
		}

		public AdaptiveDecimal(AdaptiveDecimal other) {
			this.Value = other.Value;
		}

		[BsonIgnore]
		[JsonIgnore]
		public static long MaxValue { get; } = BYTES_7;

		[BsonIgnore]
		[JsonIgnore]
		public static long MinValue { get; } = -BYTES_7;

		[BsonIgnore]
		[JsonIgnore]
		public static ulong MaxDecimalValue { get; } = BYTES_7;

		/// <summary>
		///     Number of seconds since chain inception
		/// </summary>
		public decimal Value {
			get => this.value;
			set {
				decimal adjustedValue = value.Normalize();
				this.TestMaxSize(ref adjustedValue);
				this.value = adjustedValue;
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			byte[] data = this.GetShrunkBytes();
			dehydrator.WriteRawArray(data);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.ReadData(rehydrator.ReadByte, rehydrator.ReadByte, (in Span<byte> longbytes, int srcOffset, int start, int length) => rehydrator.ReadBytes(longbytes, start, length));
		}

		public int CompareTo(AdaptiveDecimal other) {
			if(ReferenceEquals(this, other)) {
				return 0;
			}

			if(ReferenceEquals(null, other)) {
				return 1;
			}

			return this.Value.CompareTo(other.Value);
		}

		public int CompareTo(decimal other) {
			return this.Value.CompareTo(other);
		}

		public bool Equals(AdaptiveDecimal other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.Value == other.Value;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Value);

			return nodeList;
		}

		public bool Equals(decimal other) {

			return this.Value == other;
		}

		protected virtual void TestMaxSize(ref decimal entry) {

			(long integral, ulong fraction) components = this.GetComponents(entry);

			entry = this.RebuildFromPonents(components.integral, components.fraction);
		}

		/// <summary>
		///     this tells us how many decimal places in our decimal number.
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		protected int CountDecimalPlaces(decimal value) {
			int decimalPlaces = 0;

			while(value > 0) {
				decimalPlaces++;
				value *= 10;
				value -= (ulong) value;
			}

			return decimalPlaces;
		}

		/// <summary>
		///     take the decimal places and build an integer with the inverted values to keep positioning.  ex: 0.001 => 100
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		protected ulong InvertDecimalPlaces(decimal value) {
			ulong invertedValue = 0;
			int decimalPlaces = 0;

			while(value > 0) {
				decimalPlaces++;
				value *= 10;
				invertedValue += (ulong) value * (ulong) Math.Pow(10, decimalPlaces - 1);
				value -= (ulong) value;
			}

			return invertedValue;
		}

		/// <summary>
		///     Get the first digit in an int. in 321, we get 1.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		protected int GetFirstDigit(ulong value) {
			decimal temp = Math.Round((decimal) value / 10, 1);

			return (int) ((temp % 1.0M) * 10);
		}

		/// <summary>
		///     REstore the decimal places from an inverted int. ex: 100 => 0.001
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		protected decimal RestoreDecimalPlaces(ulong value) {
			decimal restored = 0;
			int decimalPlaces = 0;

			while(value > 0) {
				decimalPlaces++;
				restored += this.GetFirstDigit(value) / (decimal) Math.Pow(10, decimalPlaces);
				value /= 10;
			}

			return restored;
		}

		/// <summary>
		///     take a decimal and extract the integral and fraction components into ulongs
		/// </summary>
		/// <returns></returns>
		protected (long integral, ulong fraction) GetComponents(decimal value) {

			long integral = value > MaxValue ? MaxValue : (long) value;

			decimal fractions = value % 1.0M;

			fractions = Math.Round(fractions, MAX_DECIMALS);

			ulong fraction = this.InvertDecimalPlaces(fractions);

			if(fraction > MaxDecimalValue) {
				fraction = MaxDecimalValue;
			}

			return (integral, fraction);
		}

		protected (ulong adjustedValue, byte zeros) GetZeroSet(ulong value) {
			byte zeros = 0;

			if(value != 0) {
				int digit = 0;

				do {
					digit = this.GetFirstDigit(value);

					if(digit == 0) {
						value /= 10;
						zeros += 1;
					}
				} while((digit == 0) && (zeros < 15));
			}

			return (value, zeros);
		}

		protected byte[] BuildShrunkBytes(decimal value) {

			(long integral, ulong fraction) = this.GetComponents(value);

			// take the sign
			int sign = Math.Sign(integral);

			// and now remove it
			integral = Math.Abs(integral);

			(ulong adjustedIntegral, byte integralZeros) = this.GetZeroSet((ulong) integral);
			(ulong adjustedfraction, byte fractionZeros) = this.GetZeroSet(fraction);

			// pack the zeros in a byte:
			byte zeros = (byte) (((integralZeros & 0xF) << 4) | (fractionZeros & 0xF));

			int integralSerializationByteSize = 0;
			int fractionSerializationByteSize = 0;

			if(adjustedIntegral != 0) {
				int integralBitSize = BitUtilities.GetValueBitSize(adjustedIntegral);

				integralSerializationByteSize = (int) Math.Ceiling((double) integralBitSize / 8);

				if(integralSerializationByteSize >= 8) {
					throw new ArgumentException("integral byte size can not be larger than 7 bytes");
				}
			}

			if(adjustedfraction != 0) {
				int fractionBitSize = BitUtilities.GetValueBitSize(adjustedfraction);

				fractionSerializationByteSize = (int) Math.Ceiling((double) fractionBitSize / 8);

				if(fractionSerializationByteSize >= 8) {
					throw new ArgumentException("fraction byte size can not be larger than 7 bytes");
				}
			}

			// ensure the important type bits are set too

			bool hasZeros = zeros != 0;

			byte[] shrunkBytes = new byte[1 + (hasZeros ? 1 : 0) + integralSerializationByteSize + fractionSerializationByteSize];

			// serialize the first byte, combination of 4 bits for the serialization type, and the firs 4 bits of our value
			shrunkBytes[0] = (byte) (((byte) integralSerializationByteSize & INTEGRAL_MASK) | ((byte) (fractionSerializationByteSize << 3) & FRACTION_MASK) | (byte) (hasZeros ? ZEROS_MASK : 0) | (byte) (sign == -1 ? NEGATIVE_MASK : 0));
			int offset = 1;

			if(hasZeros) {
				shrunkBytes[1] = zeros;
				offset++;
			}

			if(integralSerializationByteSize != 0) {
				TypeSerializer.SerializeBytes(((Span<byte>) shrunkBytes).Slice(offset, integralSerializationByteSize), adjustedIntegral);
			}

			if(fractionSerializationByteSize != 0) {
				TypeSerializer.SerializeBytes(((Span<byte>) shrunkBytes).Slice(offset + integralSerializationByteSize, fractionSerializationByteSize), adjustedfraction);
			}

			return shrunkBytes;
		}

		public virtual byte[] GetShrunkBytes() {
			// determine the size it will take when serialized
			return this.BuildShrunkBytes(this.Value);

		}

		public int ReadBytes(ITcpReadingContext readContext) {

			return this.ReadData(() => readContext[0], () => readContext[1], (in Span<byte> longbytes, int srcOffset, int start, int length) => readContext.CopyTo(longbytes, srcOffset, start, length));
		}

		private int ReadData(Func<byte> readFirstByte, Func<byte> readZerosByte, CopyDataDelegate copyBytes) {
			byte firstByte = readFirstByte();

			int integralSerializationByteSize = firstByte & INTEGRAL_MASK;
			int fractionSerializationByteSize = (firstByte & FRACTION_MASK) >> 3;
			bool hasZeros = (firstByte & ZEROS_MASK) != 0;
			int sign = (firstByte & NEGATIVE_MASK) != 0 ? -1 : 1;

			int offset = 1;
			byte zeros = 0;

			if(hasZeros) {
				zeros = readZerosByte();
				offset++;
			}

			//			(int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) specs = this.ReadByteSpecs(firstByte);

			long integral = 0;
			ulong fraction = 0;

			if(integralSerializationByteSize != 0) {
				Span<byte> longbytes = stackalloc byte[8];
				copyBytes(longbytes, offset, 0, integralSerializationByteSize);

				TypeSerializer.DeserializeBytes(longbytes, out integral);
			}

			if(fractionSerializationByteSize != 0) {
				Span<byte> longbytes = stackalloc byte[8];
				copyBytes(longbytes, offset, 0, fractionSerializationByteSize);

				TypeSerializer.DeserializeBytes(longbytes, out fraction);
			}

			if(hasZeros) {
				int integralZeros = (zeros >> 4) & 0xF;
				int fractionZeros = zeros & 0xF;

				if(integralZeros != 0) {
					integral *= (long) Math.Pow(10, integralZeros);
				}

				if(fractionZeros != 0) {
					fraction *= (ulong) Math.Pow(10, fractionZeros);
				}
			}

			// restore the sign
			integral *= sign;

			this.Value = this.RebuildFromPonents(integral, fraction);

			return integralSerializationByteSize + fractionSerializationByteSize + 1;
		}

		private decimal RebuildFromPonents(long integral, ulong fraction) {
			return this.RestoreDecimalPlaces(fraction) + integral;
		}

		protected virtual ulong prepareBuffer(ulong buffer, byte firstByte) {
			return buffer;
		}

		public virtual (int integralSerializationByteSize, int fractionSerializationByteSize) ReadByteSpecs(byte firstByte) {
			int integralSerializationByteSize = firstByte & INTEGRAL_MASK;
			int fractionSerializationByteSize = (firstByte & (FRACTION_MASK << 3)) >> 3;

			return (integralSerializationByteSize, fractionSerializationByteSize);
		}

		public int ReadByteSize(byte firstByte) {
			// set the buffer, so we can read the serialization type
			(int integralSerializationByteSize, int fractionSerializationByteSize) specs = this.ReadByteSpecs(firstByte);

			return specs.integralSerializationByteSize + specs.fractionSerializationByteSize + 1;
		}

		public override bool Equals(object obj) {
			if(obj is AdaptiveDecimal adaptive) {
				return this.Equals(adaptive);
			}

			return base.Equals(obj);
		}

		public static bool operator ==(AdaptiveDecimal left, AdaptiveDecimal right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals(right);
		}

		public static bool operator ==(AdaptiveDecimal left, decimal right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Equals(right);
		}

		public static bool operator !=(AdaptiveDecimal left, AdaptiveDecimal right) {
			return !(left == right);
		}

		public static bool operator !=(AdaptiveDecimal left, decimal right) {
			return !(left == right);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

		public override string ToString() {
			return this.Value.ToString();
		}

		private delegate void CopyDataDelegate(in Span<byte> longbytes, int srcOffset, int start, int length);

	#region Operator Overloads

		public static explicit operator AdaptiveDecimal(decimal value) {
			return new AdaptiveDecimal(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator +(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			if(ReferenceEquals(null, c2)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value + c2.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator +(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value + c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator +(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value + c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator -(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			if(ReferenceEquals(null, c2)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value - c2.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator -(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value - c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator -(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value - c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator *(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			if(ReferenceEquals(null, c2)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value * c2.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator *(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value * c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator *(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value * c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator /(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			if(ReferenceEquals(null, c2)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value / c2.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator /(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value / c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator /(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			return (AdaptiveDecimal) (c1.Value / c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator ++(AdaptiveDecimal c1) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			c1.Value++;

			return c1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AdaptiveDecimal operator --(AdaptiveDecimal c1) {
			if(ReferenceEquals(null, c1)) {
				return null;
			}

			c1.Value--;

			return c1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Value > c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value > c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value > c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Value >= c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value >= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value >= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Value < c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value < c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value < c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(AdaptiveDecimal c1, AdaptiveDecimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Value <= c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(AdaptiveDecimal c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value <= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(AdaptiveDecimal c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value <= c2;
		}

	#endregion

	}
}