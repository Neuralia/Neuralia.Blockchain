using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.General.Json.Converters;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {

	/// <summary>
	///     The unique id of a transaction on the chain
	/// </summary>
	[JsonConverter(typeof(TransactionIdJsonConverter))]
	public class TransactionId : ISerializableCombo, IComparable<TransactionId> {

		public const char SEPARATOR = ':';
		public const char COMPACT_SEPARATOR = ' ';

		static TransactionId() {
			LiteDBMappers.RegisterTransactionId();
		}

		public TransactionId() : this(0, 0) {

		}

		public TransactionId(long accountSequenceId, Enums.AccountTypes accountType, long timestamp, byte scope) {
			this.Account = new AccountId(accountSequenceId, accountType);
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = scope;
		}

		public TransactionId(AccountId accountId, long timestamp, byte scope) {
			this.Account = new AccountId(accountId);
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = scope;
		}

		public TransactionId(AccountId accountId, long timestamp) : this(accountId, timestamp, 0) {

		}

		public TransactionId(long timestamp, byte scope) {
			this.Account = new AccountId();
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = scope;

		}

		public TransactionId(string transactionId) {
			this.Parse(transactionId);
		}

		public TransactionId(TransactionId other) : this(other.Account, other.Timestamp.Value, other.Scope) {

		}

		[JsonIgnore]
		public TransactionId Clone => new TransactionId(this);

		public AccountId Account { get; set; }

		public TransactionTimestamp Timestamp { get; set; }

		public byte Scope { get; set; }

		public int CompareTo(TransactionId other) {
			if(ReferenceEquals(this, other)) {
				return 0;
			}

			if(ReferenceEquals(null, other)) {
				return 1;
			}

			int accountComparison = Comparer<AccountId>.Default.Compare(this.Account, other.Account);

			if(accountComparison != 0) {
				return accountComparison;
			}

			int timestampComparison = Comparer<TransactionTimestamp>.Default.Compare(this.Timestamp, other.Timestamp);

			if(timestampComparison != 0) {
				return timestampComparison;
			}

			return this.Scope.CompareTo(other.Scope);
		}
		
		public void Dehydrateheader(IDataDehydrator dehydrator) {

			this.Account.Dehydrate(dehydrator);
			this.Timestamp.Dehydrate(dehydrator);

		}

		public void RehydrateHeader(IDataRehydrator rehydrator) {
			this.Account.Rehydrate(rehydrator);
			this.Timestamp.Rehydrate(rehydrator);

		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			this.Dehydrateheader(dehydrator);
			this.DehydrateTail(dehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.RehydrateHeader(rehydrator);
			this.RehydrateTail(rehydrator);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Account);
			nodeList.Add(this.Timestamp);
			nodeList.Add(this.Scope);

			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetValue(this.ToString());
		}

		protected virtual string[] GetTransactionIdComponents(string transactionId) {

			return transactionId.Split(new[] {SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);
		}

		public void Parse(string transactionId) {

			var items = this.GetTransactionIdComponents(transactionId);

			this.Account = new AccountId(items[0]);
			this.Timestamp = new TransactionTimestamp(items[1]);

			this.Scope = 0;

			if((items.Length == 3) && !string.IsNullOrWhiteSpace(items[2])) {
				this.Scope = byte.Parse(items[2]);
			}
		}

		public Tuple<long, long, byte> ToTuple() {
			return new Tuple<long, long, byte>(this.Account.ToLongRepresentation(), this.Timestamp.Value, this.Scope);
		}

		protected virtual void DehydrateTail(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Scope);
		}

		protected virtual void RehydrateTail(IDataRehydrator rehydrator) {
			this.Scope = rehydrator.ReadByte();
		}

		public void DehydrateRelative(IDataDehydrator dehydrator) {

			this.DehydrateTail(dehydrator);
		}

		public void RehydrateRelative(IDataRehydrator rehydrator) {

			this.RehydrateTail(rehydrator);
		}

		public static explicit operator TransactionId(string transactionId) {
			return new TransactionId(transactionId);
		}

		public static bool operator ==(TransactionId a, TransactionId b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			return !ReferenceEquals(b, null) && a.Equals(b);
		}

		public static bool operator !=(TransactionId a, TransactionId b) {
			return !(a == b);
		}

		protected bool Equals(TransactionId other) {
			return Equals(this.Account, other.Account) && Equals(this.Timestamp, other.Timestamp) && (this.Scope == other.Scope);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(!this.GetType().IsInstanceOfType(obj) && !this.GetType().IsSubclassOf(obj.GetType())) {
				return false;
			}

			return this.Equals((TransactionId) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.Account != null ? this.Account.GetHashCode() : 0;
				hashCode = (hashCode * 397) ^ (this.Timestamp != null ? this.Timestamp.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ this.Scope.GetHashCode();

				return hashCode;
			}
		}

		public override string ToString() {

			string transactionId = $"{this.Account}{SEPARATOR}{this.Timestamp}";

			// we only display the scope if it is noy zero. otherwise it is ont put, and thus assumed to be 0
			if(this.Scope != 0) {
				transactionId += $"{SEPARATOR}{SEPARATOR}{this.Scope}";
			}

			return transactionId;
		}

		/// <summary>
		///     a compact representation of the transaction Id. not meant for humans but for machines only
		/// </summary>
		/// <remarks>since this is used for Id only, we do not include the extended components</remarks>
		/// <returns></returns>
		public string ToCompactString() {
			//	this.Account.ToString()

			string accountId = this.Account.ToCompactString();

			Span<byte> buffer = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(this.Timestamp.Value, buffer);
			string timeStamp = new ByteArray(buffer.TrimEnd().ToArray()).ToBase94();

			string transactionId = $"{accountId}{COMPACT_SEPARATOR}{timeStamp}";

			// we only display the scope if it is noy zero. otherwise it is ont put, and thus assumed to be 0
			if(this.Scope != 0) {

				transactionId += $"{COMPACT_SEPARATOR}{new ByteArray(new[] {this.Scope}).ToBase94()}";
			}

			return transactionId;
		}

		/// <summary>
		///     Parse a compact string representation
		/// </summary>
		/// <param name="compact"></param>
		/// <returns></returns>
		public static TransactionId FromCompactString(string compact) {

			var items = compact.Split(new[] {COMPACT_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			AccountId accountId = AccountId.FromCompactString(items[0]);

			IByteArray buffer = ByteArray.FromBase94(items[1]);
			Span<byte> fullbuffer = stackalloc byte[sizeof(long)];
			buffer.CopyTo(fullbuffer);

			TypeSerializer.Deserialize(fullbuffer, out long timestamp);

			byte scope = 0;

			if((items.Length == 3) && !string.IsNullOrWhiteSpace(items[2])) {
				scope = ByteArray.FromBase94(items[2])[0];
			}

			return new TransactionId(accountId, timestamp, scope);
		}
	}
}