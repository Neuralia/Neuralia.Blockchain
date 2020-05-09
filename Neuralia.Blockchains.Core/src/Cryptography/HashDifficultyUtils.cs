using System;
using System.Linq;
using System.Numerics;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class HashDifficultyUtils {

		public const uint DEFAULT_PRECISION = 2;

		public static readonly BigInteger BigintTwo = new BigInteger(2);
		public static readonly int Default256Difficulty = (int) Math.Pow(10, DEFAULT_PRECISION);
		public static readonly long Default512Difficulty = (long) Math.Pow(10, DEFAULT_PRECISION);

		public static BigInteger GetBigInteger(SafeArrayHandle array) {
			return GetBigInteger(array.ToExactByteArray());
		}

		public static BigInteger GetBigInteger(byte[] array) {
			// here we must concat "00" to make sure it is unsigned positive.
			return new BigInteger(array.Concat(new byte[] {0}).ToArray());
		}

	#region Hash Utility Functions 256 bits

		public static decimal ConvertIncremental256DifficultyToDecimal(int difficulty) {
			return (decimal) difficulty / DEFAULT_PRECISION;
		}

		public static BigInteger GetHash256TargetByIncrementalDifficulty(int difficulty) {
			uint factor = (uint) Math.Pow(10, DEFAULT_PRECISION);

			return (GetHash256TargetMaximum() * factor) / new BigInteger(difficulty);
		}

		public static BigInteger GetHash256Target(int difficulty) {
			int high = difficulty >> 24;
			long low = difficulty & 0xFFFFFF;

			if(low == 0) {
				low = 1;
			}

			BigInteger biglow = new BigInteger(low);

			return biglow * BigInteger.Pow(BigintTwo, 8 * high);
		}

		private const int MAX_HASH_256 = 0x1d_00ffff; // seems this is the ideal level. lets not touch it anymore

		public static BigInteger GetHash256TargetMaximum() {
			return GetHash256Target(MAX_HASH_256);
		}

		public static double GetPowDifficulty256(BigInteger currentHashTarget) {
			BigInteger goal = GetHash256TargetMaximum();

			BigInteger powdiff = goal / currentHashTarget;

			if(powdiff < new BigInteger(double.MaxValue)) {
				return (double) powdiff + ((double) (goal % currentHashTarget) / (double) currentHashTarget);
			}

			return (double) powdiff;
		}

		public static BigInteger GetHash256TargetByIncrementalDifficulty(int difficulty, int precision = 6) {
			uint factor = (uint) Math.Pow(10, DEFAULT_PRECISION);

			return (GetHash256TargetMaximum() * factor) / new BigInteger(difficulty);
		}

	#endregion

	#region Hash Utility Functions 512 bits

		static HashDifficultyUtils() {
			byte[] bytes = new byte[512 >> 3];

			for(int d = 0; d < bytes.Length; d++) {
				bytes[d] = 255;
			}

			MaxHash512 = GetBigInteger(bytes);
		}

		public static readonly BigInteger MaxHash512;

		public static decimal ConvertIncremental512DifficultyToDecimal(long difficulty) {
			return (decimal) difficulty / DEFAULT_PRECISION;
		}

		public static BigInteger GetHash512TargetByIncrementalDifficulty(long difficulty) {

			BigInteger diff = new BigInteger(difficulty / Default512Difficulty);

			if(diff == 0) {
				diff = 1;
			}

			return MaxHash512 / diff;
		}

	#endregion

	}
}