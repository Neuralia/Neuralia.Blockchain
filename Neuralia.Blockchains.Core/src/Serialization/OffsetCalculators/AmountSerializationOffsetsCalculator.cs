namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	public class AmountSerializationOffsetsCalculator {
		private decimal currentOffsetSum;
		private bool first;
		private decimal previousOffset;

		public AmountSerializationOffsetsCalculator() {
			this.Reset(0);
		}

		public AmountSerializationOffsetsCalculator(decimal baseline) {
			this.Reset(baseline);
		}

		public decimal Baseline { get; private set; }

		public void Reset(decimal baseline) {

			this.Baseline = baseline;
			this.currentOffsetSum = 0;
			this.first = true;
			this.previousOffset = 0;
		}

		public decimal CalculateOffset(decimal currentValue) {

			decimal relativeOffset = this.Baseline + this.currentOffsetSum;

			this.previousOffset = currentValue - relativeOffset;

			return this.previousOffset;
		}

		public decimal RebuildValue(decimal offset) {

			this.previousOffset = offset;

			if(this.first) {
				return this.Baseline + offset;
			}

			decimal relativeOffset = this.Baseline + this.currentOffsetSum;

			return offset + relativeOffset;
		}

		public void AddLastOffset() {

			this.currentOffsetSum += this.previousOffset;

			this.first = false;
		}
	}
}