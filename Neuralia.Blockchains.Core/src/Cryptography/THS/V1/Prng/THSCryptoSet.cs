using System;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {
	public class THSPrngSet : THSSetBase<THSRulesSet.Prngs, ITHSPrng> {

		public unsafe void FillFull(byte* buffer, int blocksCount, ByteArray seed) {
			ITHSPrng entry = this.GetRollingEntry();
			entry.InitializeSeed(seed);
			entry.FillFull(buffer, blocksCount);
		}
		
		public unsafe void FillMedium(byte* buffer, int blocksCount, ByteArray seed) {
			ITHSPrng entry = this.GetRollingEntry();
			entry.InitializeSeed(seed);
			entry.FillMedium(buffer, blocksCount);
		}
		
		public unsafe void FillFast(byte* buffer, int blocksCount, ByteArray seed) {
			ITHSPrng entry = this.GetRollingEntry();
			entry.InitializeSeed(seed);
			entry.FillFast(buffer, blocksCount);
		}
		
		
		protected override ITHSPrng CreateEntry(THSRulesSet.Prngs tag) {
			ITHSPrng thsPrng = null;

			switch(tag) {
				case THSRulesSet.Prngs.SFC_256:
					thsPrng = new THSSfc256Prng();

					break;
				case THSRulesSet.Prngs.JSF_256:
					thsPrng = new THSJsf256Prng();

					break;
				case THSRulesSet.Prngs.MULBERRY_32:
					thsPrng = new THSMulberry32Prng();

					break;
				case THSRulesSet.Prngs.SPLITMIX_64:
					thsPrng = new THSSplitmix64Prng();

					break;
				case THSRulesSet.Prngs.XOSHIRO_256_SS:
					thsPrng = new THSXoroshiro256ssPrng();

					break;
				default:
					throw new ArgumentException();
			}

			return thsPrng;
		}
	}
}