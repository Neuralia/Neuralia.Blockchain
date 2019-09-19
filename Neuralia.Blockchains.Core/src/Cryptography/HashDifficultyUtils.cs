using System;
using System.Numerics;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class HashDifficultyUtils {

		public const uint DEFAULT_PRECISION = 2;

		public static readonly BigInteger BIGINT_TWO = new BigInteger(2);
		public static readonly int DEFAULT_256_DIFFICULTY = (int) Math.Pow(10, DEFAULT_PRECISION);
		public static readonly long DEFAULT_512_DIFFICULTY = (long) Math.Pow(10, DEFAULT_PRECISION);

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

			return biglow * BigInteger.Pow(BIGINT_TWO, 8 * high);
		}

		private const int MAX_HASH_256 = 0x1d_00ffff; // seems this is the ideal level. lets not touch it anymore

		public static BigInteger GetHash256TargetMaximum() {
			return GetHash256Target(MAX_HASH_256);
		}

		public static double GetPOWDifficulty256(BigInteger currentHashTarget) {
			BigInteger goal = GetHash256TargetMaximum();

			BigInteger POWDIFF = goal / currentHashTarget;

			if(POWDIFF < new BigInteger(double.MaxValue)) {
				return (double) POWDIFF + ((double) (goal % currentHashTarget) / (double) currentHashTarget);
			}

			return (double) POWDIFF;
		}

		public static BigInteger GetHash256TargetByIncrementalDifficulty(int difficulty, int precision = 6) {
			uint factor = (uint) Math.Pow(10, DEFAULT_PRECISION);

			return (GetHash256TargetMaximum() * factor) / new BigInteger(difficulty);
		}

	#endregion

	#region Hash Utility Functions 512 bits

		private const ulong MAX_HASH_512 = 0x3A_00_FFFFFF_FFFF0AUL; // seems this is the ideal level. lets not touch it anymore	

		public static decimal ConvertIncremental512DifficultyToDecimal(long difficulty) {
			return (decimal) difficulty / DEFAULT_PRECISION;
		}

		public static BigInteger GetHash512TargetByIncrementalDifficulty(long difficulty) {
			uint factor = (uint) Math.Pow(10, DEFAULT_PRECISION);

			return (GetHash512TargetMaximum() * factor) / new BigInteger(difficulty);
		}

		public static BigInteger GetHash512Target(ulong difficulty) {
			int high = (int) (difficulty >> 56);
			ulong low = difficulty & 0xFF_FFFFFF_FFFFFFUL;

			if(low == 0) {
				low = 1;
			}

			BigInteger biglow = new BigInteger(low);

			return biglow * BigInteger.Pow(BIGINT_TWO, 8 * high);
		}

		public static BigInteger GetHash512TargetMaximum() {
			return GetHash512Target(MAX_HASH_512);
		}

	#endregion

	}
}