using System;
using System.Collections.Generic;
using System.Globalization;
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
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {

	/// <summary>
	///     The unique id of a transaction on the chain
	/// </summary>
	[JsonConverter(typeof(TransactionIdJsonConverter))]
	public class TransactionId : IBinarySerializable, ITreeHashable, IComparable<TransactionId> {

		public const char SEPARATOR = ':';
		public const char COMPACT_SEPARATOR = ' ';

		static TransactionId() {
			LiteDBMappers.RegisterTransactionId();
		}

		public TransactionId() : this(0, 0) {

		}

		public TransactionId(long accountSequenceId, Enums.AccountTypes accountType, long timestamp, short scope) {
			this.Account = new AccountId(accountSequenceId, accountType);
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = new TransactionScope(scope);
		}

		public TransactionId(AccountId accountId, long timestamp, short scope) {
			this.Account = new AccountId(accountId);
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = new TransactionScope(scope);
		}

		public TransactionId(AccountId accountId, long timestamp) : this(accountId, timestamp, 0) {

		}

		public TransactionId(long timestamp, short scope) {
			this.Account = new AccountId();
			this.Timestamp = new TransactionTimestamp(timestamp);
			this.Scope = new TransactionScope(scope);

		}

		public TransactionId(string transactionId) {
			this.Parse(transactionId);
		}

		public TransactionId(TransactionId other) : this(other.Account, other.Timestamp.Value, other.Scope.Value) {

		}

		[JsonIgnore]
		public TransactionId Clone => new TransactionId(this);

		public AccountId Account { get; set; }

		public TransactionTimestamp Timestamp { get; set; }

		public TransactionScope Scope { get; set; }
		
		public static TransactionId Empty => new TransactionId();

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

			int scopeComparison = Comparer<TransactionScope>.Default.Compare(this.Scope, other.Scope);

			return scopeComparison;
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
		protected virtual string[] GetTransactionIdComponents(string transactionId) {

			return transactionId.Split(new[] {SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);
		}

		public void Parse(string transactionId) {

			var items = this.GetTransactionIdComponents(transactionId);

			this.Account = new AccountId(items[0]);
			this.Timestamp = new TransactionTimestamp(items[1]);

			this.Scope = new TransactionScope();

			if(items.Length == 3 && !string.IsNullOrWhiteSpace(items[2])) {
				this.Scope = new TransactionScope(items[2]);
			}
		}

		public Tuple<long, long, short> ToTuple() {
			return new Tuple<long, long, short>(this.Account.ToLongRepresentation(), this.Timestamp.Value, this.Scope.Value);
		}

		public string ToCompositeKey() {
			return this.Account.ToLongRepresentation().ToString(CultureInfo.InvariantCulture) + "_" +  this.Timestamp + "_" + this.Scope;
		}
		
		protected virtual void DehydrateTail(IDataDehydrator dehydrator) {
			this.Scope.Dehydrate(dehydrator);
		}

		protected virtual void RehydrateTail(IDataRehydrator rehydrator) {
			this.Scope.Rehydrate(rehydrator);
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

		private bool Equals(TransactionId other) {
			return Equals(this.Account, other.Account) && Equals(this.Timestamp, other.Timestamp) && Equals(this.Scope, other.Scope);
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
				hashCode = (hashCode * 397) ^ (this.Scope != null ? this.Scope.GetHashCode() : 0);

				return hashCode;
			}
		}

		public override string ToString() {

			string transactionId = $"{this.Account}{SEPARATOR}{this.Timestamp}";

			// we only display the scope if it is noy zero. otherwise it is ont put, and thus assumed to be 0
			if(this.Scope.IsNotZero) {
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
			string timeStamp = ByteArray.Wrap(buffer.TrimEnd().ToArray()).ToBase94();

			string transactionId = $"{accountId}{COMPACT_SEPARATOR}{timeStamp}";

			// we only display the scope if it is noy zero. otherwise it is ont put, and thus assumed to be 0
			if(this.Scope.IsNotZero) {
				buffer = stackalloc byte[sizeof(short)];
				TypeSerializer.Serialize(this.Scope.Value, buffer);
				
				string scope = ByteArray.Wrap(buffer.TrimEnd().ToArray()).ToBase94();
				transactionId += $"{COMPACT_SEPARATOR}{scope}";
			}

			return transactionId;
		}

		/// <summary>
		///     Parse a compact string representation
		/// </summary>
		/// <param name="compact"></param>
		/// <returns></returns>
		public static TransactionId FromCompactString(string compact) {

			if(string.IsNullOrWhiteSpace(compact)) {
				return null;
			}

			var items = compact.Split(new[] {COMPACT_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			AccountId accountId = AccountId.FromCompactString(items[0]);

			SafeArrayHandle buffer = ByteArray.FromBase94(items[1]);
			Span<byte> fullbuffer = stackalloc byte[sizeof(long)];
			buffer.Entry.CopyTo(fullbuffer);

			TypeSerializer.Deserialize(fullbuffer, out long timestamp);

			short scope = 0;

			if(items.Length == 3 && !string.IsNullOrWhiteSpace(items[2])) {
				buffer = ByteArray.FromBase94(items[2]);
				fullbuffer = stackalloc byte[sizeof(short)];
				buffer.Entry.CopyTo(fullbuffer);

				TypeSerializer.Deserialize(fullbuffer, out scope);
			}

			return new TransactionId(accountId, timestamp, scope);
		}


		/// <summary>
		///     Parse a transction Guid and return a transaction Scope object
		/// </summary>
		/// <param name="transactionGuid"></param>
		/// <returns></returns>
		public static TransactionId FromGuid(Guid transactionGuid) {
			Span<byte> guidSpan = stackalloc byte[16];
			
			transactionGuid.TryWriteBytes(guidSpan);
			Span<byte> span = stackalloc byte[8];
			guidSpan.Slice(0, 8).CopyTo(span);
			TypeSerializer.Deserialize(span, out long accountSequenceId);
			
			span = stackalloc byte[8];
			guidSpan.Slice(8, 6).CopyTo(span);
			TypeSerializer.Deserialize(span, out long timestamp);

			span = stackalloc byte[2];
			guidSpan.Slice(14, 2).CopyTo(span);
			TypeSerializer.Deserialize(span, out short scope);

			return new TransactionId(accountSequenceId.ToAccountId(), timestamp, scope);
		}

		/// <summary>
		///     Here we create a guid from our transaction information
		/// </summary>
		/// <param name="transactionId"></param>
		/// <returns></returns>
		public static Guid TransactionIdToGuid(TransactionId transactionId) {
			
			if(transactionId == null) {
				return Guid.Empty;
			}
			
			Span<byte> guidSpan = stackalloc byte[16];

			Span<byte> span = stackalloc byte[8];
			TypeSerializer.Serialize(transactionId.Account.ToLongRepresentation(), span);
			span.CopyTo(guidSpan.Slice(0, 8));
			
			span = stackalloc byte[8];
			TypeSerializer.Serialize(transactionId.Timestamp.Value, span);
			span.Slice(0, 6).CopyTo(guidSpan.Slice(8, 6));

			span = stackalloc byte[2];
			TypeSerializer.Serialize(transactionId.Scope.Value, span);
			span.CopyTo(guidSpan.Slice(14, 2));
			
			return new Guid(guidSpan);

		}

		public Guid ToGuid() {
			return TransactionIdToGuid(this);
		}
	}
}