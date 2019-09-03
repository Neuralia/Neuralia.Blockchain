namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     here a 0 counts for +1, and thus values must be sequential by at least +1
	/// </summary>
	public class SequantialOffsetCalculator : OffsetCalculator {

		public SequantialOffsetCalculator() : this(0) {
		}

		public SequantialOffsetCalculator(long baseline) : base(baseline, 1) {
		}

		public void Reset() {
			this.Reset(this.Baseline);
		}
	}
}