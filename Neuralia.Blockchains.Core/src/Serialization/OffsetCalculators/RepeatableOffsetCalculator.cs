namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     In this case, we can have repeats, and a 0 counts for 0 adn repeats the same value
	/// </summary>
	public class RepeatableOffsetCalculator : OffsetCalculator {

		public RepeatableOffsetCalculator() : this(0) {
		}

		public RepeatableOffsetCalculator(long baseline) : base(baseline, 0) {
		}
	}
}