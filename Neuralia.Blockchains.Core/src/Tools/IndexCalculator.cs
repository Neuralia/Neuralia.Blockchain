using System;

namespace Neuralia.Blockchains.Core.Tools {
	public static class IndexCalculator {

		/// <summary>
		///     determine where a value falls within a goruping set.
		/// </summary>
		/// <param name="id">1 based</param>
		/// <param name="groupingSize"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public static (long groupIndex, long startingBlockId, long endBlockId) ComputeIndex(long id, int groupingSize) {

			if(id <= 0) {
				throw new ApplicationException("Id cannot be 0.");
			}

			long index = (long) Math.Ceiling((decimal) id / groupingSize);
			long startingBlockId = ((index - 1) * groupingSize) + 1;

			return (index, startingBlockId, (startingBlockId + groupingSize) - 1);
		}
	}
}