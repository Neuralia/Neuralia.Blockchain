using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.General.Json.Converters;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {
	[JsonConverter(typeof(TransactionIdExtendedJsonConverter))]
	public class TransactionIdExtended : TransactionId, IComparable<TransactionIdExtended> {

		private const char EXTENDED_SEPARATOR = '/';

		static TransactionIdExtended() {
			LiteDBMappers.RegisterTransactionIdExtended();
		}

		// the extended fields
		public TransactionIdExtended() {
		}

		public TransactionIdExtended(long accountSequenceId, Enums.AccountTypes accountType, long timestamp, byte scope) : base(accountSequenceId, accountType, timestamp, scope) {

		}

		public TransactionIdExtended(AccountId accountId, long timestamp, byte scope) : base(accountId, timestamp, scope) {
		}

		public TransactionIdExtended(TransactionId other) : this(other.Account, other.Timestamp.Value, other.Scope) {
		}

		public TransactionIdExtended(AccountId accountId, long timestamp, byte scope, long? keySequenceId, long? keyUseIndex, byte ordinal) : base(accountId, timestamp, scope) {

			if(keySequenceId.HasValue && keyUseIndex.HasValue) {
				this.KeyUseIndex = new KeyUseIndexSet(keySequenceId.Value, keyUseIndex.Value, ordinal);
			}
		}

		public TransactionIdExtended(string transactionId) : base(transactionId) {

			string extended = this.GetExtendedComponents(transactionId);

			if(!string.IsNullOrWhiteSpace(extended)) {
				var extendedComponents = extended.Replace("[", "").Replace("]", "").Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries);

				if(extendedComponents.Length != 2) {
					throw new ApplicationException("Invalid extended format");
				}

				this.KeyUseIndex = new KeyUseIndexSet(extendedComponents[1]);
			}
		}

		public TransactionIdExtended(TransactionIdExtended other) : this(other.Account, other.Timestamp.Value, other.Scope, other.KeyUseIndex?.KeyUseSequenceId?.Value, other.KeyUseIndex?.KeyUseIndex?.Value, other.KeyUseIndex?.Ordinal ?? 0) {
		}

		/// <summary>
		///     The key sequence Id, to know which key we are using
		/// </summary>
		public KeyUseIndexSet KeyUseIndex { get; set; }

		/// <summary>
		///     are the extended components set?
		/// </summary>
		public bool ContainsExtended => this.KeyUseIndex != null;

		public int CompareTo(TransactionIdExtended other) {
			if(ReferenceEquals(this, other)) {
				return 0;
			}

			if(ReferenceEquals(null, other)) {
				return 1;
			}

			int transactionIdComparison = base.CompareTo(other);

			if(transactionIdComparison != 0) {
				return transactionIdComparison;
			}

			return Comparer<KeyUseIndexSet>.Default.Compare(this.KeyUseIndex, other.KeyUseIndex);
		}

		public TransactionId ToTransactionId() {
			return new TransactionId(this);
		}

		protected override void DehydrateTail(IDataDehydrator dehydrator) {
			base.DehydrateTail(dehydrator);

			dehydrator.Write(this.KeyUseIndex == null);

			this.KeyUseIndex?.Dehydrate(dehydrator);
		}

		protected override void RehydrateTail(IDataRehydrator rehydrator) {
			base.RehydrateTail(rehydrator);

			bool isNull = rehydrator.ReadBool();

			if(!isNull) {
				this.KeyUseIndex = new KeyUseIndexSet();
				this.KeyUseIndex.Rehydrate(rehydrator);
			}
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetValue(this.ToExtendedString());
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.KeyUseIndex);

			return nodeList;
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		protected override string[] GetTransactionIdComponents(string transactionId) {

			var essentials = transactionId.Split(new[] {EXTENDED_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			string basic = essentials[0];
			string extended = essentials.Length == 2 ? essentials[1] : null;

			return transactionId.Split(new[] {SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);
		}

		protected string GetExtendedComponents(string transactionId) {

			var essentials = transactionId.Split(new[] {EXTENDED_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			return essentials.Length == 2 ? essentials[1] : null;
		}

		public override string ToString() {

			return this.ToEssentialString();
		}

		public string ToEssentialString() {
			return base.ToString();
		}

		public virtual string ToExtendedString() {
			string transactionId = this.ToEssentialString();

			if(this.ContainsExtended) {
				transactionId += $"{EXTENDED_SEPARATOR}{this.KeyUseIndex}";
			}

			return transactionId;
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

			if(obj is TransactionIdExtended other) {
				return this.Equals((TransactionId) obj);
			}

			return base.Equals(obj);
		}

		public bool Equals(TransactionIdExtended other) {
			if(ReferenceEquals(other, null)) {
				return false;
			}

			return base.Equals(other);
		}

		public static bool operator ==(TransactionIdExtended a, TransactionIdExtended b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			if(ReferenceEquals(b, null)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(TransactionIdExtended a, TransactionIdExtended b) {
			return !(a == b);
		}

		public static bool operator ==(TransactionIdExtended a, TransactionId b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			if(ReferenceEquals(b, null)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(TransactionIdExtended a, TransactionId b) {
			return !(a == b);
		}
	}
}