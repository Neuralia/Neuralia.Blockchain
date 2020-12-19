using System;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Utils {
	public class KeyUseIndexSet : ISerializableCombo, IComparable<KeyUseIndexSet> {
		
		public KeyUseIndexSet() {
			
		}


		public KeyUseIndexSet(long? keyUseSequenceId, long keyUseIndex) {
			this.KeyUseSequenceId = keyUseSequenceId;
			this.KeyUseIndex = keyUseIndex;
		}

		public KeyUseIndexSet(string version) : this(version?.Replace("[", "")?.Replace("]", "")?.Split(',')?.Select(e => e?.Trim())?.ToArray()) {

		}

		public KeyUseIndexSet(string[] version) {
			if(version == null) {
				return;
			}

			if(version.Length >= 1) {
				if(!string.IsNullOrWhiteSpace(version[0])) {
					if(version[0].Trim() == "*") {
						this.KeyUseSequenceId = null;
					}
					else if(long.TryParse(version[0], out long sequenceId)) {
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
		}

		public KeyUseIndexSet(int? keyUseSequenceId, int keyUseIndex) : this(keyUseSequenceId, (long) keyUseIndex) {
		}

		public KeyUseIndexSet(KeyUseIndexSet other) : this(other.KeyUseSequenceId, other.KeyUseIndex) {

		}

		public virtual KeyUseIndexSet Clone() => new KeyUseIndexSet(this);

		/// <summary>
		///     The key sequence Id, to know which key we are using
		/// </summary>
		public long? KeyUseSequenceId { get; set; }

		/// <summary>
		///     The key use index inside the current key sequence.
		/// </summary>
		public long KeyUseIndex { get; set; }
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public virtual bool IsSet => (this.KeyUseSequenceId != 0) || ((this.KeyUseIndex != 0));
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
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

			bool exists = rehydrator.ReadBool();
			
			AdaptiveLong1_9 tool  = new AdaptiveLong1_9();

			this.KeyUseSequenceId = null;
			if(exists) {
				tool.Rehydrate(rehydrator);
				this.KeyUseSequenceId = tool.Value;
			}

			tool.Rehydrate(rehydrator);
			this.KeyUseIndex = tool.Value;
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.KeyUseSequenceId.HasValue);

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			if(this.KeyUseSequenceId.HasValue) {
				tool.Value = this.KeyUseSequenceId.Value;
				tool.Dehydrate(dehydrator);
			}

			tool.Value = this.KeyUseIndex;
			tool.Dehydrate(dehydrator);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.KeyUseSequenceId);
			hashNodeList.Add(this.KeyUseIndex);

			return hashNodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("KeyUseSequenceId", this.KeyUseSequenceId);
			jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex);
		}

		public static implicit operator KeyUseIndexSet((long keyUseSequenceId, long keyUseIndex) d) {
			return new KeyUseIndexSet(d.keyUseSequenceId, d.keyUseIndex);
		}
		
		public static implicit operator KeyUseIndexSet((long? keyUseSequenceId, long keyUseIndex) d) {
			return new KeyUseIndexSet(d.keyUseSequenceId, d.keyUseIndex);
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
			return Equals(this.KeyUseSequenceId, other.KeyUseSequenceId) && Equals(this.KeyUseIndex, other.KeyUseIndex);
		}

		public override string ToString() {
			return $"[{(this.KeyUseSequenceId.HasValue?this.KeyUseSequenceId.Value.ToString():"*")},{this.KeyUseIndex}]";
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

				return hashCode;
			}
		}

		public static bool operator ==(KeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndex) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.KeyUseSequenceId == c2.keyUseSequenceId) && (c1.KeyUseIndex == c2.keyUseIndex);
		}

        public static bool operator !=(KeyUseIndexSet c1, (int keyUseSequenceId, int keyUseIndex) c2)
        {
            return !(c1 == c2);
        }

        public static bool operator ==(KeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.KeyUseSequenceId == c2.keyUseSequenceId) && (c1.KeyUseIndex == c2.keyUseIndex);
		}

		public static bool operator !=(KeyUseIndexSet c1, (long keyUseSequenceId, long keyUseIndex) c2) {
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

			
			if(a.KeyUseSequenceId < b.KeyUseSequenceId) {
				return true;
			}

			if(a.KeyUseSequenceId.HasValue && b.KeyUseSequenceId.HasValue) {
				if(a.KeyUseSequenceId > b.KeyUseSequenceId) {
					return false;
				}
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

		public static bool operator <=(KeyUseIndexSet a, KeyUseIndexSet b) {
			return (a == b) || (a < b);
		}

		public static bool operator >(KeyUseIndexSet a, KeyUseIndexSet b) {
			if(a.KeyUseSequenceId < b.KeyUseSequenceId) {
				return false;
			}

			if(a.KeyUseSequenceId.HasValue && b.KeyUseSequenceId.HasValue) {
				if(a.KeyUseSequenceId > b.KeyUseSequenceId) {
					return false;
				}
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

		public static bool operator >=(KeyUseIndexSet a, KeyUseIndexSet b) {
			return (a == b) || (a > b);
		}

		public void IncrementSequence() {
			this.KeyUseSequenceId += 1;
			this.KeyUseIndex = 0;
		}
		
		public void IncrementKeyUseIndex() {
			this.KeyUseIndex += 1;
		}
	}
}