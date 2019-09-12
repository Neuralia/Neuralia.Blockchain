using System.Text;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Specialized {
	public class Text : ITreeHashable, IBinarySerializable {

		public Text() {

		}

		public Text(string value) {
			this.Value = value;
		}

		public string Value { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {
			bool nodeEmtpy = rehydrator.ReadBool();

			if(!nodeEmtpy) {
				bool isCompressed = rehydrator.ReadBool();
				IByteArray bytes = rehydrator.ReadNonNullableArray();

				if(isCompressed) {
					DeflateCompression compressor = new DeflateCompression();
					IByteArray bytesCopy = bytes;
					bytes = compressor.Decompress(bytesCopy);
					bytesCopy.Return();
				}

				this.Value = Encoding.UTF8.GetString(bytes.ToExactByteArray());
				bytes.Return();
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			bool nodeEmtpy = string.IsNullOrWhiteSpace(this.Value);

			dehydrator.Write(nodeEmtpy);

			if(!nodeEmtpy) {

				// lets attempt to compress the note, see if the comrpessed version is more space efficient
				DeflateCompression compressor = new DeflateCompression();

				ByteArray bytes = Encoding.UTF8.GetBytes(this.Value);
				IByteArray compressed = compressor.Compress(bytes);

				if(compressed.Length < bytes.Length) {
					// ok, saved the compressed version
					dehydrator.Write(true);
					dehydrator.WriteNonNullable(compressed);
				} else {
					// the original is smaller, save that
					dehydrator.Write(false);
					dehydrator.WriteNonNullable(bytes);
				}

				compressed.Return();
				bytes.Return();
			}
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Value);

			return nodeList;
		}

		public override string ToString() {
			return this.Value;
		}

	#region opeartor overloads

		public static implicit operator Text(string value) {
			return new Text(value);
		}

		public static implicit operator string(Text value) {
			return value.Value;
		}

		public static bool operator ==(Text left, Text right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals(right);
		}

		public static bool operator ==(Text left, string right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Equals(right);
		}

		protected bool Equals(string other) {
			return string.Equals(this.Value, other);
		}

		protected bool Equals(Text other) {
			return string.Equals(this.Value, other.Value);
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

			return this.Equals((Text) obj);
		}

		public override int GetHashCode() {
			return this.Value != null ? this.Value.GetHashCode() : 0;
		}

		public static bool operator !=(Text left, Text right) {
			return !(left == right);
		}

		public static bool operator !=(Text left, string right) {
			return !(left == right);
		}

	#endregion

	}
}