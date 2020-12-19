using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {

	/// <summary>
	/// </summary>
	/// <remarks>The 3rd fastest of the lot</remarks>
	public class THSMulberry32Prng : THSPrngBase {

		private uint seed;

		public override void InitializeSeed(ByteArray buffer) {
			TypeSerializer.Deserialize(buffer.Memory.Slice(0, sizeof(int)), out this.seed);
		}

		protected override ulong GetEntry() {
			ulong result = this.Round();
			result |= this.Round() << 0x20;

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ulong Round() {
			this.seed += 0x6D2B79F5U;
			ulong temp = this.seed;
			temp = (temp ^ (temp >> 0xF)) * (1 | temp);
			temp ^= temp + ((temp ^ (temp >> 0x7)) * (temp | 0x3DU));

			return temp ^ (temp >> 0xE);
		}
	}
}