using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {
	public interface ITHSPrng : IDisposableExtended {
		void InitializeSeed(ByteArray buffer);
		unsafe void FillFull(byte* buffer, int blocksCount);
		unsafe void FillMedium(byte* buffer, int blocksCount);
		unsafe void FillFast(byte* buffer, int blocksCount);
	}
}