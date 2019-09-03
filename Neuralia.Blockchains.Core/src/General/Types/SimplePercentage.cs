using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types {

	// a simple percentage that serializes to a byte value. represents the value on a simple 1/255 value set.
	public class SimplePercentage : IBinarySerializable, ITreeHashable {

		public const int DECIMALS_LIMIT = 6;

		public SimplePercentage() {

		}

		public SimplePercentage(byte value) {
			this.Value = value;
		}

		public SimplePercentage(int value) {
			this.Value = (byte) value;
		}

		public SimplePercentage(double value) {
			this.Percentage = (decimal) value;
		}

		public SimplePercentage(decimal value) {
			this.Percentage = value;
		}

		public decimal Percentage {
			get => Math.Round((decimal) this.Value / byte.MaxValue, DECIMALS_LIMIT);
			set {
				// find the closest matching value
				if((value > 1) || (value < 0)) {
					throw new InvalidOperationException("Value must be between 0 to 1");
				}

				this.Value = (byte) Math.Round(value * byte.MaxValue);
			}
		}

		public byte Value { get; set; }

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Value);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.Value = rehydrator.ReadByte();
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Value);

			return nodeList;
		}

		public static implicit operator SimplePercentage(byte d) {
			return new SimplePercentage(d);
		}

		public static implicit operator SimplePercentage(int d) {
			return new SimplePercentage(d);
		}

		public static implicit operator SimplePercentage(double d) {
			return new SimplePercentage(d);
		}
	}
}