using System;
using System.Linq;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Utils {
	/// <summary>
	///     Account IDs on the blockchain. the first 2 bits tell us if we save it on 4, 6 or 8 bytes.
	/// </summary>
	[JsonConverter(typeof(IdKeyUseIndexSetJsonConverter))]
	public class IdKeyUseIndexSet : KeyUseIndexSet, IComparable<IdKeyUseIndexSet> {

		public IdKeyUseIndexSet() {
			this.KeyUseSequenceId = new AdaptiveLong1_9();
			this.KeyUseIndex = new AdaptiveLong1_9();
			this.Ordinal = 0;
		}

		public IdKeyUseIndexSet(AdaptiveLong1_9 keyUseSequenceId, AdaptiveLong1_9 keyUseIndex, byte ordinal) {
			this.KeyUseSequenceId = keyUseSequenceId;
			this.KeyUseIndex = keyUseIndex;
			this.Ordinal = ordinal;
		}

		public IdKeyUseIndexSet(long keyUseSequenceId, long keyUseIndex, byte ordinal) {
			this.KeyUseSequenceId = keyUseSequenceId;
			this.KeyUseIndex = keyUseIndex;
			this.Ordinal = ordinal;
		}

		public IdKeyUseIndexSet(string version) : this(version?.Replace("[", "")?.Replace("]", "")?.Split(',')?.Select(e => e?.Trim())?.ToArray()) {

		}

		public IdKeyUseIndexSet(string[] version): base(version) {
			
			if(version.Length >= 3) {
				if(!string.IsNullOrWhiteSpace(version[2])) {
					if(byte.TryParse(version[2], out byte ordinalId)) {
						this.Ordinal = ordinalId;
					}
				}
			}
		}

		public IdKeyUseIndexSet(int keyUseSequenceId, int keyUseIndex, byte ordinal) : this(keyUseSequenceId, (long) keyUseIndex, ordinal) {
		}

		public IdKeyUseIndexSet(IdKeyUseIndexSet other) : this(other.KeyUseSequenceId, other.KeyUseIndex, other.Ordinal) {

		}
		
		public IdKeyUseIndexSet(KeyUseIndexSet other, byte ordinal) : this(other.KeyUseSequenceId, other.KeyUseIndex, ordinal) {

		}

		public KeyUseIndexSet KeyUseIndexSet => new KeyUseIndexSet(this);
		public byte Ordinal { get; set; }

		public override bool IsSet => base.IsSet && (this.Ordinal != 0);
		
		public IdKeyUseIndexSet Clone2() => new IdKeyUseIndexSet(this);

		public override KeyUseIndexSet Clone() => new IdKeyUseIndexSet(this);

		public int CompareTo(IdKeyUseIndexSet other) {
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

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);
			this.Ordinal = rehydrator.ReadByte();
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			dehydrator.Write(this.Ordinal);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();
			
			hashNodeList.Add(this.Ordinal);

			return hashNodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Ordinal", this.Ordinal);
		}

		public static implicit operator IdKeyUseIndexSet((long keyUseSequenceId, long keyUseIndex, byte ordinal) d) {
			return new IdKeyUseIndexSet(d.keyUseSequenceId, d.keyUseIndex, d.ordinal);
		}

		public virtual void EnsureEqual(IdKeyUseIndexSet other) {

			if(!Equals(this.KeyUseSequenceId, other.KeyUseSequenceId)) {
				throw new ApplicationException("Invalid keyUseSequenceId value");
			}

			if(!Equals(this.KeyUseIndex, other.KeyUseIndex)) {
				throw new ApplicationException("Invalid keyUseIndex value");
			}
		}

		protected bool Equals(IdKeyUseIndexSet other) {
			return base.Equals(other) && (this.Ordinal == other.Ordinal);
		}

		public override string ToString() {
			return $"[{this.KeyUseSequenceId},{this.KeyUseIndex},{this.Ordinal}]";
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

			return this.Equals((IdKeyUseIndexSet) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ this.Ordinal.GetHashCode();

				return hashCode;
			}
		}

		public static bool operator ==(IdKeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndex, byte ordinal) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.KeyUseSequenceId == c2.keyUseSequenceId) && (c1.KeyUseIndex == c2.keyUseIndex) && (c1.Ordinal == c2.ordinal);
		}

		public static bool operator !=(IdKeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndexx, byte ordinal) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(IdKeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex, byte ordinal) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.KeyUseSequenceId == c2.keyUseSequenceId) && (c1.KeyUseIndex == c2.keyUseIndex) && (c1.Ordinal == c2.ordinal);
		}

		public static bool operator !=(IdKeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex, byte ordinal) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(IdKeyUseIndexSet c1, IdKeyUseIndexSet c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(IdKeyUseIndexSet c1, IdKeyUseIndexSet c2) {
			return !(c1 == c2);
		}

		public static bool operator <(IdKeyUseIndexSet a, IdKeyUseIndexSet b) {

			if(a.Ordinal != b.Ordinal) {
				throw new InvalidOperationException("Different ordinal ids");
			}

			if(a.KeyUseSequenceId < b.KeyUseSequenceId) {
				return true;
			}

			if(a.KeyUseSequenceId > b.KeyUseSequenceId) {
				return false;
			}

			// -1 key indices are basically ignored and are thus equal.
			if((a.KeyUseIndex == -1) || (b.KeyUseIndex == -1)) {
				return false;
			}

			if(a.KeyUseIndex < b.KeyUseIndex) {
				return true;
			}

			return false;
		}

		public static bool operator <=(IdKeyUseIndexSet a, IdKeyUseIndexSet b) {
			return (a == b) || (a < b);
		}

		public static bool operator >(IdKeyUseIndexSet a, IdKeyUseIndexSet b) {
			if(a.Ordinal != b.Ordinal) {
				throw new InvalidOperationException("Different ordinal ids");
			}

			if(a.KeyUseSequenceId < b.KeyUseSequenceId) {
				return false;
			}

			if(a.KeyUseSequenceId > b.KeyUseSequenceId) {
				return true;
			}

			// -1 key indices are basically ignored and are thus equal.
			if((a.KeyUseIndex == -1) || (b.KeyUseIndex == -1)) {
				return false;
			}

			if(a.KeyUseIndex > b.KeyUseIndex) {
				return true;
			}

			return false;
		}

		public static bool operator >=(IdKeyUseIndexSet a, IdKeyUseIndexSet b) {
			return (a == b) || (a > b);
		}
	}

}