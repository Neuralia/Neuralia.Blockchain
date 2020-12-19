using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {

	/// <summary>
	/// </summary>
	/// <remarks>The fastest of the lot</remarks>
	public class THSSplitmix64Prng : THSPrngBase {

		private ulong seed;

		public override void InitializeSeed(ByteArray buffer) {
			TypeSerializer.Deserialize(buffer.Memory.Slice(0, sizeof(ulong)), out this.seed);
		}

		protected override ulong GetEntry() {
			this.seed += 0x9E3779B97F4A7C15UL;

			ulong temp = this.seed;
			temp = (temp ^ (temp >> 0x1E)) * 0xBF58476D1CE4E5B9UL;
			temp = (temp ^ (temp >> 0x1B)) * 0x94D049BB133111EBUL;
			temp = temp ^ (temp >> 0x1F);

			return temp;
		}
	}
}