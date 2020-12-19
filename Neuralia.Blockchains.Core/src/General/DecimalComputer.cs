using System;

namespace Neuralia.Blockchains.Core.General {
	/// <summary>
	///     A utility class to ensure that we perform our double calculations with decimal precision
	/// </summary>
	public static class DecimalComputer {

		public static double PerformComputation(double a, double b, Func<decimal, decimal, decimal> operation) {
			return (double) operation((decimal) a, (decimal) b);
		}

		public static double PerformComputation(double a, double b, double c, Func<decimal, decimal, decimal, decimal> operation) {
			return (double) operation((decimal) a, (decimal) b, (decimal) c);
		}

		public static double PerformComputation(double a, double b, double c, double d, Func<decimal, decimal, decimal, decimal, decimal> operation) {
			return (double) operation((decimal) a, (decimal) b, (decimal) c, (decimal) d);
		}

		public static bool PerformComparison(double a, double b, Func<decimal, decimal, bool> operation) {
			return operation((decimal) a, (decimal) b);
		}

		public static bool PerformComparison(double a, double b, double c, Func<decimal, decimal, decimal, bool> operation) {
			return operation((decimal) a, (decimal) b, (decimal) c);
		}

		public static bool PerformComparison(double a, double b, double c, double d, Func<decimal, decimal, decimal, decimal, bool> operation) {
			return operation((decimal) a, (decimal) b, (decimal) c, (decimal) d);
		}
	}
}