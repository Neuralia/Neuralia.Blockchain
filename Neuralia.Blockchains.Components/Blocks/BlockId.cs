using System;
using System.Text.Json.Serialization;
using LiteDB;
using Neuralia.Blockchains.Components.Converters;
using Neuralia.Blockchains.Core.General.Types.Dynamic;

namespace Neuralia.Blockchains.Components.Blocks {
	/// <summary>
	///     this is a block Scope (number). In reality, we will never use more than can be store in a uint (since the timestamp
	///     will expire before the block id pool).
	///     so even if we use a long in ram to represent it, we can save it on a 4 bytes uint.
	/// </summary>
	[JsonConverter(typeof(BlockIdJsonConverter))]
	public class BlockId : AdaptiveLong1_8, IComparable<BlockId> {

		static BlockId() {
			RegisterBlockId();
		}
		
		public static void RegisterBlockId() {

			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new BlockId(bson.AsInt64));
		}

		public BlockId(long value) : base(value) {
		}

		public BlockId(BlockId other) : this(other.Value) {
		}

		public BlockId(string other) : this(FromString(other)) {
		}

		public BlockId() {
		}

		[JsonIgnore]
		public BlockId Clone => new BlockId(this);

		public static BlockId NullBlockId => new BlockId();

		public int CompareTo(BlockId other) {
			return this.Value.CompareTo(other.Value);
		}

		public override string ToString() {
			return this.Value.ToString();
		}

		public static string ToString(BlockId value) {
			if(value == null) {
				value = new BlockId();
			}

			return value.ToString();
		}

		public static BlockId FromString(string value) {
			if(string.IsNullOrWhiteSpace(value)) {
				return new BlockId();
			}

			return new BlockId(long.Parse(value));
		}

		public static implicit operator BlockId(long blockId) {
			return new BlockId(blockId);
		}

		public static implicit operator long(BlockId blockId) {
			return blockId.Value;
		}

		public static implicit operator BlockId(uint blockId) {
			return new BlockId((long) blockId);
		}

		public static explicit operator uint(BlockId blockId) {
			return (uint) blockId.Value;
		}

		public bool Equals(BlockId other) {
			return this.Value == other.Value;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((BlockId) obj);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

		public static implicit operator BlockId(int blockId) {
			return new BlockId((long) blockId);
		}

		public static bool operator ==(BlockId c1, BlockId c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(BlockId c1, BlockId c2) {
			return !(c1 == c2);
		}

		public static bool operator <(BlockId a, BlockId b) {

			if(ReferenceEquals(null, a)) {
				a = new BlockId();
			}

			if(ReferenceEquals(null, b)) {
				b = new BlockId();
			}

			return a.Value < b.Value;
		}

		public static bool operator <=(BlockId a, BlockId b) {
			return (a == b) || (a < b);
		}

		public static bool operator >(BlockId a, BlockId b) {

			if(ReferenceEquals(null, a)) {
				a = new BlockId();
			}

			if(ReferenceEquals(null, b)) {
				b = new BlockId();
			}

			return a.Value > b.Value;
		}

		public static bool operator >=(BlockId a, BlockId b) {
			return (a == b) || (a > b);
		}

		/// <summary>
		///     This one must be explicit, because there is a risk of loss of data.
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public static explicit operator int(BlockId blockId) {
			if(blockId.Value > int.MaxValue) {
				throw new ApplicationException("Impossible to convert to an int32.");
			}

			return (int) blockId.Value;
		}
	}

}