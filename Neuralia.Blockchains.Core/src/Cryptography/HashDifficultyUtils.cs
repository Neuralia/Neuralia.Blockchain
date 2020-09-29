using System;
using System.Linq;
using System.Numerics;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class HashDifficultyUtils {
		
		public static readonly int PRECISION = 1000;
		public static readonly long Default512Difficulty = 1*PRECISION;

		public static BigInteger GetBigInteger(SafeArrayHandle array) {
			return GetBigInteger(array.ToExactByteArray());
		}

		public static BigInteger GetBigInteger(byte[] array) {
			// here we must concat "00" to make sure it is unsigned positive.
			return new BigInteger(array.Concat(new byte[] {0}).ToArray());
		}
		

	#region Hash Utility Functions 512 bits

		static HashDifficultyUtils() {
			byte[] bytes = new byte[512 >> 3];

			Array.Fill(bytes, byte.MaxValue);

			MaxHash512 = GetBigInteger(bytes);
		}

		public static readonly BigInteger MaxHash512;

		public static decimal ConvertIncremental512DifficultyToDecimal(long difficulty) {
			return  1M / difficulty;
		}

		public static BigInteger GetHash512TargetByIncrementalDifficulty(long difficulty) {
			
			return (MaxHash512 * PRECISION) / difficulty;
		}

	#endregion

	}
}