using System;

namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     here a 0 counts for +1, and thus values must be sequential by at least +1
	/// </summary>
	public class SequentialOffsetCalculator<T> : OffsetCalculator<T>
		where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>{

		public SequentialOffsetCalculator(Func<T, T, T> add, Func<T, T, T> subtract) : this(default, add, subtract) {
		}
		
		public SequentialOffsetCalculator(T zeroIncrement, Func<T, T, T> add, Func<T, T, T> subtract) : base(zeroIncrement, add, subtract) {
		}

		public SequentialOffsetCalculator(T baseline, T zeroIncrement, Func<T, T, T> add, Func<T, T, T> subtract) : base(baseline, zeroIncrement, add, subtract) {
		}

		public void Reset() {
			this.Reset(this.Baseline);
		}
	}

	public class SequentialLongOffsetCalculator : SequentialOffsetCalculator<long> {

		public SequentialLongOffsetCalculator() : this(0) {
		}

		public SequentialLongOffsetCalculator(long baseline) : base(baseline, 1, (a, b) => a + b, (a, b) => a - b) {
		}
	}
}