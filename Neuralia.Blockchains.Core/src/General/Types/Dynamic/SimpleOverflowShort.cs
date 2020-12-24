using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	/// a class designed to allow overflow when a byte is full. this is useful to extend data types that were previously limited to a single byte.
	/// </summary>
	public class SimpleOverflowShort : ITreeHashable, IBinarySerializable, IJsonValue {

		public const byte OVERFLOW_FLAG = byte.MaxValue;
		public const byte BYTE_ENTRY = OVERFLOW_FLAG-1;
		public static readonly ushort MAX_VALUE = AdaptiveShort1_2.MAX_VALUE;

		public ushort Value { get; set; }

		public SimpleOverflowShort() {
			
		}
		
		public SimpleOverflowShort(ushort value) {
			this.Value = value;
		}
		
		public void Rehydrate(IDataRehydrator rehydrator) {
			var firstByte = rehydrator.ReadByte();

			if(firstByte < OVERFLOW_FLAG) {
				this.Value = firstByte;
			} else {
				AdaptiveShort1_2 tool = new AdaptiveShort1_2();
				tool.Rehydrate(rehydrator);
				
				this.Value = (ushort)(tool.Value + BYTE_ENTRY);
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			if(this.Value < OVERFLOW_FLAG) {
				dehydrator.Write((byte)this.Value);
			} else {
				dehydrator.Write(OVERFLOW_FLAG);
				
				AdaptiveShort1_2 tool = new AdaptiveShort1_2((ushort)(this.Value - BYTE_ENTRY));
				tool.Dehydrate(dehydrator);
			}
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			if(this.Value < OVERFLOW_FLAG) {
				hashNodeList.Add((byte)this.Value);
			} else {
				hashNodeList.Add(this.Value);
			}

			return hashNodeList;
		}

		public void JsonWriteValue(string name, JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty(name, this.Value);
		}

		public override string ToString() {
			return this.Value.ToString();
		}
		
		public override bool Equals(object obj) {
			if(obj is SimpleOverflowShort adaptive) {
				return this.Equals(adaptive);
			}

			return base.Equals(obj);
		}

		public static bool operator ==(SimpleOverflowShort left, SimpleOverflowShort right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals(right);
		}

		public static bool operator !=(SimpleOverflowShort left, SimpleOverflowShort right) {
			return !Equals(left, right);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

	}
}