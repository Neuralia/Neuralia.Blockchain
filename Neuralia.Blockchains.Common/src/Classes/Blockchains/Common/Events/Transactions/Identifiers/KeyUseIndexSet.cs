using System;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.General.Json.Converters;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {
	/// <summary>
	///     Account IDs on the blockchain. the first 2 bits tell us if we save it on 4, 6 or 8 bytes.
	/// </summary>
	[JsonConverter(typeof(KeyUseIndexSetJsonConverter))]
	public class KeyUseIndexSet : ISerializableCombo, IComparable<KeyUseIndexSet> {

		public KeyUseIndexSet() {
			this.KeyUseSequenceId = new AdaptiveLong1_9();
			this.KeyUseIndex = new AdaptiveLong1_9();
			this.Ordinal = 0;
		}

		public KeyUseIndexSet(AdaptiveLong1_9 keyUseSequenceId, AdaptiveLong1_9 keyUseIndex, byte ordinal) {
			this.KeyUseSequenceId = keyUseSequenceId;
			this.KeyUseIndex = keyUseIndex;
			this.Ordinal = ordinal;
		}

		public KeyUseIndexSet(long keyUseSequenceId, long keyUseIndex, byte ordinal) {
			this.KeyUseSequenceId = keyUseSequenceId;
			this.KeyUseIndex = keyUseIndex;
			this.Ordinal = ordinal;
		}

		public KeyUseIndexSet(string version) : this(version?.Replace("[", "")?.Replace("]", "")?.Split(',')?.Select(e => e?.Trim())?.ToArray()) {

		}

		public KeyUseIndexSet(string[] version) {
			if(version == null) {
				return;
			}

			if(version.Length >= 1) {
				if(!string.IsNullOrWhiteSpace(version[0])) {
					if(long.TryParse(version[0], out long sequenceId)) {
						this.KeyUseSequenceId = sequenceId;
					}
				}
			}
			if(version.Length >= 2) {
				if(!string.IsNullOrWhiteSpace(version[1])) {
					if(long.TryParse(version[1], out long useIndex)) {
						this.KeyUseIndex = useIndex;
					}
				}
			}
			if(version.Length >= 3) {
				if(!string.IsNullOrWhiteSpace(version[2])) {
					if(byte.TryParse(version[2], out byte ordinalId)) {
						this.Ordinal = ordinalId;
					}
				}
			}
		}

		public KeyUseIndexSet(int keyUseSequenceId, int keyUseIndex, byte ordinal) : this(keyUseSequenceId, (long) keyUseIndex, ordinal) {
		}

		public KeyUseIndexSet(KeyUseIndexSet other) : this(other.KeyUseSequenceId, other.KeyUseIndex, other.Ordinal) {

		}
		
		[JsonIgnore]
		public KeyUseIndexSet Clone => new KeyUseIndexSet(this);

		/// <summary>
		///     The key sequence Id, to know which key we are using
		/// </summary>
		public AdaptiveLong1_9 KeyUseSequenceId { get; set; }

		/// <summary>
		///     The key use index inside the current key sequence.
		/// </summary>
		public AdaptiveLong1_9 KeyUseIndex { get; set; }

		public byte Ordinal { get; set; }

		public bool IsSet => this.KeyUseSequenceId.Value != 0 || this.KeyUseIndex.Value != 0 && this.Ordinal != 0;

		public virtual bool IsNull => !this.IsSet;

		public int CompareTo(KeyUseIndexSet other) {
			if(other == null) {
				return 1;
			}

			if(this == other) {
				return 0;
			}

			if(this < other) {
				return -1;
			}

			if(this > other) {
				return 1;
			}

			throw new ArgumentException();
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.KeyUseSequenceId.Rehydrate(rehydrator);
			this.KeyUseIndex.Rehydrate(rehydrator);
			this.Ordinal = rehydrator.ReadByte();
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			this.KeyUseSequenceId.Dehydrate(dehydrator);
			this.KeyUseIndex.Dehydrate(dehydrator);
			dehydrator.Write(this.Ordinal);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.KeyUseSequenceId.Value);
			hashNodeList.Add(this.KeyUseIndex.Value);
			hashNodeList.Add(this.Ordinal);

			return hashNodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("KeyUseSequenceId", this.KeyUseSequenceId.Value);
			jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex.Value);
			jsonDeserializer.SetProperty("Ordinal", this.Ordinal);
		}

		public static implicit operator KeyUseIndexSet((long keyUseSequenceId, long keyUseIndex, byte ordinal) d) {
			return new KeyUseIndexSet(d.keyUseSequenceId, d.keyUseIndex, d.ordinal);
		}

		public virtual void EnsureEqual(KeyUseIndexSet other) {

			if(!Equals(this.KeyUseSequenceId, other.KeyUseSequenceId)) {
				throw new ApplicationException("Invalid keyUseSequenceId value");
			}

			if(!Equals(this.KeyUseIndex, other.KeyUseIndex)) {
				throw new ApplicationException("Invalid keyUseIndex value");
			}
		}

		protected bool Equals(KeyUseIndexSet other) {
			return Equals(this.KeyUseSequenceId, other.KeyUseSequenceId) && Equals(this.KeyUseIndex, other.KeyUseIndex) && this.Ordinal == other.Ordinal;
		}

		public override string ToString() {
			return $"[{this.KeyUseSequenceId.Value},{this.KeyUseIndex.Value},{this.Ordinal}]";
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

			return this.Equals((KeyUseIndexSet) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.KeyUseSequenceId != null ? this.KeyUseSequenceId.GetHashCode() : 0;
				hashCode = (hashCode * 397) ^ (this.KeyUseIndex != null ? this.KeyUseIndex.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ this.Ordinal.GetHashCode();

				return hashCode;
			}
		}

		public static bool operator ==(KeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndex, byte ordinal) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.KeyUseSequenceId.Value == c2.keyUseSequenceId && c1.KeyUseIndex.Value == c2.keyUseIndex && c1.Ordinal == c2.ordinal;
		}

		public static bool operator !=(KeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndexx, byte ordinal) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(KeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex, byte ordinal) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.KeyUseSequenceId.Value == c2.keyUseSequenceId && c1.KeyUseIndex.Value == c2.keyUseIndex && c1.Ordinal == c2.ordinal;
		}

		public static bool operator !=(KeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex, byte ordinal) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(KeyUseIndexSet c1, KeyUseIndexSet c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(KeyUseIndexSet c1, KeyUseIndexSet c2) {
			return !(c1 == c2);
		}

		public static bool operator <(KeyUseIndexSet a, KeyUseIndexSet b) {

			if(a.Ordinal != b.Ordinal) {
				throw new InvalidOperationException("Different ordinal ids");
			}

			if(a.KeyUseSequenceId.Value < b.KeyUseSequenceId.Value) {
				return true;
			}

			if(a.KeyUseSequenceId.Value > b.KeyUseSequenceId.Value) {
				return false;
			}

			// -1 key indices are basically ignored and are thus equal.
			if(a.KeyUseIndex.Value == -1 || b.KeyUseIndex.Value == -1) {
				return false;
			}

			if(a.KeyUseIndex.Value < b.KeyUseIndex.Value) {
				return true;
			}

			return false;
		}

		public static bool operator <=(KeyUseIndexSet a, KeyUseIndexSet b) {
			return a == b || a < b;
		}

		public static bool operator >(KeyUseIndexSet a, KeyUseIndexSet b) {
			if(a.Ordinal != b.Ordinal) {
				throw new InvalidOperationException("Different ordinal ids");
			}

			if(a.KeyUseSequenceId.Value < b.KeyUseSequenceId.Value) {
				return false;
			}

			if(a.KeyUseSequenceId.Value > b.KeyUseSequenceId.Value) {
				return true;
			}

			// -1 key indices are basically ignored and are thus equal.
			if(a.KeyUseIndex.Value == -1 || b.KeyUseIndex.Value == -1) {
				return false;
			}

			if(a.KeyUseIndex.Value > b.KeyUseIndex.Value) {
				return true;
			}

			return false;
		}

		public static bool operator >=(KeyUseIndexSet a, KeyUseIndexSet b) {
			return a == b || a > b;
		}
	}

}