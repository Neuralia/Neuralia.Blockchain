using System;

namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     In this case, we can have repeats, and a 0 counts for 0 adn repeats the same value
	/// </summary>
	public class RepeatableOffsetCalculator<T> : OffsetCalculator<T>
		where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>{

		public RepeatableOffsetCalculator(Func<T, T, T> add, Func<T, T, T> subtract) : this(default, add, subtract) {
		}
		
		public RepeatableOffsetCalculator(T baseline, Func<T, T, T> add, Func<T, T, T> subtract) : base(baseline, default, add, subtract) {
		}

		public void Reset() {
			this.Reset(this.Baseline);
		}
	}

	public class RepeatableLongOffsetCalculator : RepeatableOffsetCalculator<long> {

		public RepeatableLongOffsetCalculator() : this(0) {
		}

		public RepeatableLongOffsetCalculator(long baseline) : base(baseline, (a, b) => a + b, (a, b) => a - b) {
		}
	}
	
	public class RepeatableDecimalOffsetCalculator : SequentialOffsetCalculator<decimal> {

		public RepeatableDecimalOffsetCalculator() : base(0, (a, b) => a + b, (a, b) => a - b) {
		}
		
		public RepeatableDecimalOffsetCalculator(decimal baseline) : base(baseline, (a, b) => a + b, (a, b) => a - b) {
		}
	}
}