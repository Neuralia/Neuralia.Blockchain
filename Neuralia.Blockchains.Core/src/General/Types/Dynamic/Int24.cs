using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Dynamic {
	/// <summary>
	///     A special integer that is stored on 3 bytes when dehydrated
	/// </summary>
	public struct Int24 : ITreeHashable, IBinarySerializable {
		private int id;

		private static void CheckOverflow(int value) {
			if(value > 0xFFFFFF) {
				throw new OverflowException();
			}
		}

		public Int24(int value) {
			CheckOverflow(value);
			this.id = value;
		}

		public Int24(Int24 other) {
			this.id = other.Id;
		}

		public static explicit operator Int24(int value) {
			return new Int24(value);
		}

		/// <summary>
		///     Number of seconds since chain inception
		/// </summary>
		public int Id {
			get => this.id;
			set {
				CheckOverflow(value);
				this.id = value;
			}
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Id);

			return nodeList;
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			Span<byte> data = stackalloc byte[3];
			TypeSerializer.SerializeBytes(data, this.Id);
			dehydrator.WriteRawArray(data);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			Span<byte> intbytes = stackalloc byte[4];

			rehydrator.ReadBytes(intbytes, 0, 3);

			TypeSerializer.DeserializeBytes(intbytes, out int buffer);

			this.Id = buffer;
		}

		public override bool Equals(object obj) {
			if(obj is Int24 int24) {
				return this.Id == int24.Id;
			}

			return base.Equals(obj);
		}

		public override int GetHashCode() {
			return this.Id.GetHashCode();
		}

		public override string ToString() {
			return this.Id.ToString();
		}
	}
}