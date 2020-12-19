using System;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {

	/// <summary>
	/// </summary>
	/// <remarks>The slowest of the lot</remarks>
	public class THSJsf256Prng : THSPrngBase {

		private const int X = 7;
		private const int Y = 13;
		private const int Z = 37;

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
			ulong temp = this.seed1 - Rotate(this.seed2, X);
			this.seed1 = this.seed2 ^ Rotate(this.seed3, Y);
			this.seed2 = this.seed3 + Rotate(this.seed4, Z);
			this.seed3 = this.seed4 + temp;
			this.seed4 = temp + this.seed1;

			return temp;
		}
	}
}