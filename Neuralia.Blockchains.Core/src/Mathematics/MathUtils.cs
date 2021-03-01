using System;
using System.Linq;

namespace Neuralia.Blockchains.Core.Mathematics {
	public static class MathUtils {
		
		// determine a Z Score for a set of data to later determine if the variation is acceptable or not
		public static double ZScore(double[] data, double value, out double mean, out double std)
		{
			if(data == null || data.Length == 0)
			{
				mean = 0;
				std = 0;
				return 0;
			}
			if(data.Length == 1)
			{
				mean = data[0];
				std = 0;
				return value / data[0];
			}

			var avg = mean = data.Average();
			var variance = data.Aggregate(0d, (s, x) => s + Math.Pow(x - avg, 2)) / data.Length;
			std = Math.Sqrt(variance);

			if(avg == 0) {
				return 0;
			}

			if(std == 0) {
				return 0;
			}
			
			return (value - avg) / std;
		}

	}
}