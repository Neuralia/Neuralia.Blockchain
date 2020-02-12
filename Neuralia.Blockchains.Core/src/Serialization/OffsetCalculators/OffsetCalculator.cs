using System;

namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     a simple utility to calculate the offsets relative to a baseline and last offset
	/// </summary>
	public abstract class OffsetCalculator {

		private readonly int zeroIncrement;
		private long currentOffsetSum;
		private bool first;
		private long previousOffset;

		public OffsetCalculator(int zeroIncrement) : this(0, zeroIncrement) {

		}

		public OffsetCalculator(long baseline, int zeroIncrement) {

			this.zeroIncrement = zeroIncrement;
			this.Reset(baseline);
		}

		public long Baseline { get; private set; }

		public void Reset(long baseline) {

			this.Baseline = baseline;
			this.currentOffsetSum = 0;
			this.first = true;
			this.previousOffset = 0;
		}

		public long CalculateOffset(long currentValue) {

			if(currentValue < this.Baseline) {
				throw new ArgumentOutOfRangeException(nameof(currentValue), $"value of {currentValue} is smaller than minimum acceptable baseline of {this.Baseline}.");
			}
			// a note here is that in the offset, a 0 counts as a spot. '0' is +1 relative to the previous one. (except for the first entry, which is the baseline)
			long relativeOffset = this.Baseline + this.currentOffsetSum;

			if(!this.first) {
				relativeOffset += this.zeroIncrement;
			}

			this.previousOffset = currentValue - relativeOffset;

			return this.previousOffset;
		}

		public long RebuildValue(long offset) {

			this.previousOffset = offset;

			if(this.first) {
				return this.Baseline + offset;
			}

			// a note here is that in the offset, a 0 counts as a spot. '0' is +1 relative to the previous one.
			long relativeOffset = this.Baseline + this.currentOffsetSum + this.zeroIncrement;

			return offset + relativeOffset;
		}

		public void AddLastOffset() {

			this.currentOffsetSum += this.previousOffset;

			if(!this.first) {
				this.currentOffsetSum += this.zeroIncrement;
			}

			this.first = false;
		}
	}
}