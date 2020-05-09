using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LiteDB;
using Neuralia.Blockchains.Components.Converters.old;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Encodings;

namespace Neuralia.Blockchains.Components.Transactions.Identifiers {
	/// <summary>
	///     Number of seconds since the chain inception
	/// </summary>
	/// <remarks>
	///     to encode 1 year (31,536,000), we need 25 bits (4 bytes), so there is no point in encoding smaller sizes. we
	///     will always need 4 or more bytes to store a time offset.  we use 6 bytes >> 2 bits for a maximum of 2,229,850
	///     years.
	/// </remarks>
	[JsonConverter(typeof(TransactionTimestampJsonConverter))]
	public class TransactionTimestamp : AdaptiveLong3_6, IComparable<TransactionTimestamp> {

		public const string REGEX_VALID_CORE = @"[0-9A-Z]{1,10}";
		public const string REGEX_VALID = "^"+ REGEX_VALID_CORE + "$";
		
		/// <summary>
		///     we use a maximum of 7 bytes.
		/// </summary>
		static TransactionTimestamp() {
			RegisterTransactionTimestamp();
		}
		
		public static void RegisterTransactionTimestamp() {
			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new TransactionTimestamp(bson.AsInt64));
		}

		public TransactionTimestamp() {

		}

		public TransactionTimestamp(long timestamp) : base(timestamp) {

		}

		public TransactionTimestamp(string timestamp) : this(FromString(timestamp)) {

		}

		public TransactionTimestamp(TransactionTimestamp other) : this(other.Value) {

		}

		[JsonIgnore]
		public TransactionTimestamp Clone => new TransactionTimestamp(this);

		public int CompareTo(TransactionTimestamp other) {

			if(other == null) {
				return -1;
			}

			return this.Value.CompareTo(other.Value);
		}

		public override bool Equals(object obj) {
			if(obj is TransactionTimestamp other) {
				return this.Value == other.Value;
			}

			return base.Equals(obj);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

		public static TransactionTimestamp FromString(string value) {
			return NumberBaser.FromBase32ToLong(value);
		}

		public override string ToString() {
			return NumberBaser.ToBase32(this.Value);
		}

		public static bool IsValid(string value) {
			if (string.IsNullOrEmpty(value)) 
				return false;
			
			try {
				var regex = new Regex(REGEX_VALID, RegexOptions.IgnoreCase);
				return regex.IsMatch(Base32.Prepare(value));
			}
			catch {
				// nothing to do
			}
			return false;
		}
		
		public static implicit operator TransactionTimestamp(int value) {
			return new TransactionTimestamp(value);
		}

		public static implicit operator TransactionTimestamp(long value) {
			return new TransactionTimestamp(value);
		}

		public TransactionTimestamp ToTransactionTimestamp() {
			return this.Clone;
		}
	}
}