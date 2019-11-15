using System;
using LiteDB;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	public abstract class AdaptiveNumber<T> : ITreeHashable, IBinarySerializable, IEquatable<AdaptiveNumber<T>>, IComparable<T>, IComparable<AdaptiveNumber<T>>
		where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T> {

		private T value;

		public AdaptiveNumber() {

		}

		public AdaptiveNumber(T value) {

			this.Value = value;
		}

		public AdaptiveNumber(AdaptiveNumber<T> other) {
			this.Value = other.Value;
		}

		[BsonIgnore]
		[JsonIgnore]
		public abstract T MaxValue { get; }

		[BsonIgnore]
		[JsonIgnore]
		protected abstract int Offset { get; }

		[BsonIgnore]
		[JsonIgnore]
		protected abstract int MinimumByteCount { get; }

		[BsonIgnore]
		[JsonIgnore]
		protected abstract int MaximumByteCount { get; }

		[BsonIgnore]
		[JsonIgnore]
		protected int LowerMask => byte.MaxValue >> (8 - this.Offset);

		[BsonIgnore]
		[JsonIgnore]
		protected int HigherMask => byte.MaxValue >> this.Offset;

		/// <summary>
		///     Number of seconds since chain inception
		/// </summary>
		public T Value {
			get => this.value;
			set {
				this.TestMaxSize(value);
				this.value = value;
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			var data = this.GetShrunkBytes();
			dehydrator.WriteRawArray(data);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.ReadData(() => rehydrator.ReadByte(), (in Span<byte> longbytes, int start, int length) => rehydrator.ReadBytes(longbytes, start, length));
		}

		public int CompareTo(AdaptiveNumber<T> other) {
			if(ReferenceEquals(this, other)) {
				return 0;
			}

			if(ReferenceEquals(null, other)) {
				return 1;
			}

			return this.value.CompareTo(other.value);
		}

		public int CompareTo(T other) {
			return this.value.CompareTo(other);
		}

		public bool Equals(AdaptiveNumber<T> other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			if(ReferenceEquals(this, other)) {
				return true;
			}

			return this.value.Equals(other.value);
		}
		
		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Value);

			return nodeList;
		}

		protected void TestMaxSize(T value) {
			if(value.CompareTo(this.MaxValue) > 0) {
				throw new ApplicationException("Invalid value value. bit size is too big!");
			}
		}

		protected byte[] BuildShrunkBytes(T value) {
			ulong convertedValue = this.ConvertTypeFrom(value);

			int bitSize = BitUtilities.GetValueBitSize(convertedValue) + this.Offset;
			int serializationByteSize = 0;

			for(int i = this.MinimumByteCount; i <= this.MaximumByteCount; i++) {
				if(bitSize <= (8 * i)) {
					serializationByteSize = i;

					break;
				}
			}

			(int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) adjusted = this.AdjustSerializationByteSize(serializationByteSize);

			// ensure the important type bits are set too

			var shrunkBytes = new byte[adjusted.serializationByteSize];

			// serialize the first byte, combination of 4 bits for the serialization type, and the firs 4 bits of our value
			shrunkBytes[0] = (byte) ((byte) adjusted.bitValues & this.LowerMask);
			byte tempId = (byte) ((byte) convertedValue & this.HigherMask);
			shrunkBytes[0] |= (byte) (tempId << this.Offset);

			// now offset the rest of the ulong value
			ulong temp = convertedValue >> (8 - this.Offset);

			TypeSerializer.SerializeBytes(((Span<byte>) shrunkBytes).Slice(1, shrunkBytes.Length - 1), temp);

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

			(int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) specs = this.ReadByteSpecs(firstByte);

			Span<byte> longbytes = stackalloc byte[8];

			int readLength = specs.serializationByteSize - 1;

			copyBytes(longbytes, 0, readLength);

			TypeSerializer.DeserializeBytes(longbytes, out ulong buffer);

			buffer <<= 8 - this.Offset;
			buffer |= (byte) (firstByte >> this.Offset);

			buffer = this.prepareBuffer(buffer, firstByte);

			this.Value = this.ConvertTypeTo(buffer);

			return readLength + 1;
		}

		protected abstract T ConvertTypeTo(ulong buffer);
		protected abstract ulong ConvertTypeFrom(T value);

		protected virtual ulong prepareBuffer(ulong buffer, byte firstByte) {
			return buffer;
		}

		public virtual (int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) ReadByteSpecs(byte firstByte) {
			// set the buffer, so we can read the serialization type
			return this.AdjustSerializationByteSize((firstByte & this.LowerMask) + this.MinimumByteCount);
		}

		public int ReadByteSize(byte firstByte) {
			// set the buffer, so we can read the serialization type
			return this.ReadByteSpecs(firstByte).serializationByteSize;
		}

		protected virtual (int serializationByteSize, int adjustedSerializationByteExtraSize, int bitValues) AdjustSerializationByteSize(int value) {
			return (value, value - this.MinimumByteCount, value - this.MinimumByteCount);
		}

		public override bool Equals(object obj) {
			if(obj is AdaptiveNumber<T> adaptive) {
				return this.Equals(adaptive);
			}

			return base.Equals(obj);
		}

		public static bool operator ==(AdaptiveNumber<T> left, AdaptiveNumber<T> right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals(right);
		}

		public static bool operator !=(AdaptiveNumber<T> left, AdaptiveNumber<T> right) {
			return !Equals(left, right);
		}

		public override int GetHashCode() {
			return this.value.GetHashCode();
		}

		public override string ToString() {
			return this.Value.ToString();
		}

		private delegate void CopyDataDelegate(in Span<byte> longbytes, int start, int length);
	}
}