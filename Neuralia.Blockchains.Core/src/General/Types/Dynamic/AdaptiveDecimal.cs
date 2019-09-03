using System;
using System.Runtime.CompilerServices;
using LiteDB;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     a dynamic decimal that can take up to 8 bytes above the point and 8 bytes below the point. +one metadata byte. so
	///     the maximum size it will take is 17 bytes.
	/// </summary>
	public class AdaptiveDecimal : ITreeHashable, IBinarySerializable, IJsonSerializable, IEquatable<AdaptiveDecimal>, IComparable<decimal>, IComparable<AdaptiveDecimal> {

		private const byte INTEGRAL_MASK = 0x7;
		private const byte FRACTION_MASK = 0x38;
		private const byte X_MASK = 0x40;
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
		public virtual long MaxValue { get; } = long.MaxValue;

		[BsonIgnore]
		[JsonIgnore]
		public virtual long MinValue { get; } = long.MinValue;

		[BsonIgnore]
		[JsonIgnore]
		public virtual ulong MaxDecimalValue { get; } = ulong.MaxValue;

		/// <summary>
		///     Number of seconds since chain inception
		/// </summary>
		public decimal Value {
			get => this.value;
			set {
				decimal adjustedValue = value.Normalize();
				this.TestMaxSize(adjustedValue);
				this.value = adjustedValue;
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			var data = this.GetShrunkBytes();
			dehydrator.WriteRawArray(data);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.ReadData(rehydrator.ReadByte, (in Span<byte> longbytes, int start, int length) => rehydrator.ReadBytes(longbytes, start, length));
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

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetValue(this.Value);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Value);

			return nodeList;
		}

		public bool Equals(decimal other) {

			return this.Value == other;
		}

		protected virtual void TestMaxSize(decimal value) {

			if(value > this.MaxValue) {
				throw new ApplicationException("Invalid value. bit size is too big!");
			}

			if(value < this.MinValue) {
				throw new ApplicationException("Invalid value. bit size is too small!");
			}

			(long integral, ulong fraction) components = this.GetComponents(value);

			if(components.fraction > this.MaxDecimalValue) {
				throw new ApplicationException("Invalid value. fractions size is too big!");
			}
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
			long integral = (long) value;
			decimal fractions = value % 1.0M;
			ulong fraction = this.InvertDecimalPlaces(fractions);

			return (integral, fraction);
		}

		protected byte[] BuildShrunkBytes(decimal value) {

			(long integral, ulong fraction) = this.GetComponents(value);

			// take the sign
			int sign = Math.Sign(integral);

			// and now remove it
			integral = Math.Abs(integral);

			int integralSerializationByteSize = 0;
			int fractionSerializationByteSize = 0;

			if(integral != 0) {
				int integralBitSize = BitUtilities.GetValueBitSize((ulong) integral);

				for(int i = 1; i <= 8; i++) {
					if(integralBitSize <= (8 * i)) {
						integralSerializationByteSize = i;

						break;
					}
				}
			}

			if(fraction != 0) {
				int fractionBitSize = BitUtilities.GetValueBitSize(fraction);

				for(int i = 1; i <= 8; i++) {
					if(fractionBitSize <= (8 * i)) {
						fractionSerializationByteSize = i;

						break;
					}
				}
			}

			// ensure the important type bits are set too

			var shrunkBytes = new byte[1 + integralSerializationByteSize + fractionSerializationByteSize];

			// serialize the first byte, combination of 4 bits for the serialization type, and the firs 4 bits of our value
			shrunkBytes[0] = (byte) (((byte) integralSerializationByteSize & INTEGRAL_MASK) | ((byte) (fractionSerializationByteSize << 3) & FRACTION_MASK) | (byte) (sign == -1 ? NEGATIVE_MASK : 0));

			if(integralSerializationByteSize != 0) {
				TypeSerializer.SerializeBytes(((Span<byte>) shrunkBytes).Slice(1, integralSerializationByteSize), integral);
			}

			if(fractionSerializationByteSize != 0) {
				TypeSerializer.SerializeBytes(((Span<byte>) shrunkBytes).Slice(1 + integralSerializationByteSize, fractionSerializationByteSize), fraction);
			}

			return shrunkBytes;
		}

		public virtual byte[] GetShrunkBytes() {
			// determine the size it will take when serialized
			return this.BuildShrunkBytes(this.Value);

		}

		public int ReadBytes(ITcpReadingContext readContext) {

			return this.ReadData(() => readContext[0], (in Span<byte> longbytes, int start, int length) => readContext.CopyTo(longbytes, 1, start, length));
		}

		private int ReadData(Func<byte> readFirstByte, CopyDataDelegate copyBytes) {
			byte firstByte = readFirstByte();

			int integralSerializationByteSize = firstByte & INTEGRAL_MASK;
			int fractionSerializationByteSize = (firstByte & FRACTION_MASK) >> 3;
			int sign = (firstByte & NEGATIVE_MASK) != 0 ? -1 : 1;

			//			(int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) specs = this.ReadByteSpecs(firstByte);

			long integral = 0;
			ulong fraction = 0;

			if(integralSerializationByteSize != 0) {
				Span<byte> longbytes = stackalloc byte[8];
				copyBytes(longbytes, 0, integralSerializationByteSize);

				TypeSerializer.DeserializeBytes(longbytes, out integral);
			}

			if(fractionSerializationByteSize != 0) {
				Span<byte> longbytes = stackalloc byte[8];
				copyBytes(longbytes, 0, fractionSerializationByteSize);

				TypeSerializer.DeserializeBytes(longbytes, out fraction);
			}

			// restore the sign
			integral *= sign;

			this.Value = this.RestoreDecimalPlaces(fraction) + integral;

			return integralSerializationByteSize + fractionSerializationByteSize + 1;
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

		private delegate void CopyDataDelegate(in Span<byte> longbytes, int start, int length);

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