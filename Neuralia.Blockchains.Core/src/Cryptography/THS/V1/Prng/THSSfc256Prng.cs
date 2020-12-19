using System;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {

	/// <summary>
	/// </summary>
	/// <remarks>The 2nd fastest of the lot</remarks>
	public class THSSfc256Prng : THSPrngBase {

		private const int X = 0x18;
		private const int Y = 0xB;
		private const int Z = 3;

		private ulong seed1;
		private ulong seed2;
		private ulong seed3;
		private ulong seed4;

		public override void InitializeSeed(ByteArray buffer) {
			Memory<byte> memory = buffer.Memory;
			TypeSerializer.Deserialize(memory.Slice(0, sizeof(ulong)), out this.seed1);
			TypeSerializer.Deserialize(memory.Slice(sizeof(ulong), sizeof(ulong)), out this.seed2);
			TypeSerializer.Deserialize(memory.Slice(sizeof(ulong) * 2, sizeof(ulong)), out this.seed3);
			TypeSerializer.Deserialize(memory.Slice(sizeof(ulong) * 3, sizeof(ulong)), out this.seed4);
		}

		protected override ulong GetEntry() {
			ulong temp = this.seed1 + this.seed2;
			this.seed4++;
			temp += this.seed4;

			this.seed1 = this.seed2 ^ (this.seed2 >> Y);
			this.seed4 = this.seed3 + (this.seed3 << Z);
			this.seed3 = ((this.seed3 << X) | (this.seed3 >> (64 - X))) + temp;

			return temp;
		}
	}
}