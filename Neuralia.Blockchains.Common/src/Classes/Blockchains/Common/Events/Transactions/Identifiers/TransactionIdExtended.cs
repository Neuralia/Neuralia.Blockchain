using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.General.Json.Converters;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {
	
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>we do not inherit from transactionId to prevent accidental equality. better make them explicit</remarks>
	[JsonConverter(typeof(TransactionIdExtendedJsonConverter))]
	public class TransactionIdExtended : ISerializableCombo, IComparable<TransactionIdExtended> {

		private const char EXTENDED_SEPARATOR = '/';

		static TransactionIdExtended() {
			LiteDBMappers.RegisterTransactionIdExtended();
		}
		
		private readonly TransactionId simpleTransactionId = new TransactionId();

		// the extended fields
		public TransactionIdExtended() {
		}


		public TransactionIdExtended(long accountSequenceId, Enums.AccountTypes accountType, long timestamp, byte scope) {
			this.simpleTransactionId.Account = new AccountId(accountSequenceId, accountType);
			this.simpleTransactionId.Timestamp = new TransactionTimestamp(timestamp);
			this.simpleTransactionId.Scope = scope;
		}

		public TransactionIdExtended(AccountId accountId, long timestamp, byte scope) {
			this.simpleTransactionId.Account = accountId;
			this.simpleTransactionId.Timestamp = timestamp;
			this.simpleTransactionId.Scope = scope;
		}

		public TransactionIdExtended(TransactionId other) : this(other.Account, other.Timestamp.Value, other.Scope) {
		}

		public TransactionIdExtended(AccountId accountId, long timestamp, byte scope, long? keySequenceId, long? keyUseIndex, byte ordinal) : this(accountId, timestamp, scope) {

			if(keySequenceId.HasValue && keyUseIndex.HasValue) {
				this.KeyUseIndex = new KeyUseIndexSet(keySequenceId.Value, keyUseIndex.Value, ordinal);
			}
		}
		
		public TransactionIdExtended(string transactionId) {

			this.simpleTransactionId.Parse(transactionId);
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

		public AccountId Account {
			get => this.simpleTransactionId.Account;
			set => this.simpleTransactionId.Account = value;
		}
		
		public TransactionTimestamp Timestamp {
			get => this.simpleTransactionId.Timestamp;
			set => this.simpleTransactionId.Timestamp = value;
		}
		
		public TransactionId SimpleTransactionId => this.simpleTransactionId;

		public byte Scope {
			get => this.simpleTransactionId.Scope;
			set => this.simpleTransactionId.Scope = value;
		}
		
		public int CompareTo(TransactionIdExtended other) {
			if(ReferenceEquals(this, other)) {
				return 0;
			}

			if(ReferenceEquals(null, other)) {
				return 1;
			}

			int transactionIdComparison = this.simpleTransactionId.CompareTo(other.simpleTransactionId);

			if(transactionIdComparison != 0) {
				return transactionIdComparison;
			}

			return Comparer<KeyUseIndexSet>.Default.Compare(this.KeyUseIndex, other.KeyUseIndex);
		}

		public TransactionId ToTransactionId() {
			return new TransactionId(this.simpleTransactionId);
		}
		
		public void Dehydrate(IDataDehydrator dehydrator) {

			this.SimpleTransactionId.Dehydrate(dehydrator);

			this.DehydrateTail(dehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.SimpleTransactionId.Rehydrate(rehydrator);

			this.RehydrateTail(rehydrator);
		}

		protected void DehydrateTail(IDataDehydrator dehydrator) {

			dehydrator.Write(this.KeyUseIndex == null);

			this.KeyUseIndex?.Dehydrate(dehydrator);
		}

		protected void RehydrateTail(IDataRehydrator rehydrator) {

			bool isNull = rehydrator.ReadBool();

			if(!isNull) {
				this.KeyUseIndex = new KeyUseIndexSet();
				this.KeyUseIndex.Rehydrate(rehydrator);
			}
		}
		
		public void DehydrateRelative(IDataDehydrator dehydrator) {

			this.SimpleTransactionId.DehydrateRelative(dehydrator);
			this.DehydrateTail(dehydrator);
		}

		public void RehydrateRelative(IDataRehydrator rehydrator) {
			
			this.SimpleTransactionId.RehydrateRelative(rehydrator);
			this.RehydrateTail(rehydrator);
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			this.simpleTransactionId.JsonDehydrate(jsonDeserializer);
			jsonDeserializer.SetValue(this.ToExtendedString());
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.simpleTransactionId);
			nodeList.Add(this.KeyUseIndex);

			return nodeList;
		}

		public override int GetHashCode() {
			return this.SimpleTransactionId.GetHashCode();
		}

		protected string[] GetTransactionIdComponents(string transactionId) {

			var essentials = transactionId.Split(new[] {EXTENDED_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			string basic = essentials[0];
			string extended = essentials.Length == 2 ? essentials[1] : null;

			return transactionId.Split(new[] {TransactionId.SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);
		}

		protected string GetExtendedComponents(string transactionId) {

			var essentials = transactionId.Split(new[] {EXTENDED_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);

			return essentials.Length == 2 ? essentials[1] : null;
		}

		public override string ToString() {

			return this.ToEssentialString();
		}

		public string ToEssentialString() {
			return this.SimpleTransactionId.ToString();
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
				return this.Equals(other);
			}

			if(obj is TransactionId other2) {
				return this.Equals(other2);
			}
			
			return base.Equals(obj);
		}

		public bool Equals(TransactionIdExtended other) {
			return this.Equals(other.SimpleTransactionId);
		}
		
		public bool Equals(TransactionId other) {
			if(ReferenceEquals(other, null)) {
				return false;
			}

			// we always compare on the inner transaction id
			return this.SimpleTransactionId.Equals(other);
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