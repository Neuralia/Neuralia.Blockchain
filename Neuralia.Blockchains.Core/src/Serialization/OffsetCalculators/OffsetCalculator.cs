using System;

namespace Neuralia.Blockchains.Core.Serialization.OffsetCalculators {

	/// <summary>
	///     a simple utility to calculate the offsets relative to a baseline and last offset
	/// </summary>
	public abstract class OffsetCalculator<T> 
		where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>{

		private readonly T zeroIncrement;
		private T currentOffsetSum;
		private bool first;
		private T previousOffset;
		private readonly Func<T, T, T> add;
		private readonly Func<T, T, T> subtract;

		public OffsetCalculator(T zeroIncrement, Func<T, T, T> add, Func<T, T, T> subtract) : this(default, zeroIncrement, add, subtract) {

		}

		public OffsetCalculator(T baseline, T zeroIncrement, Func<T, T, T> add, Func<T, T, T> subtract) {

			this.add = add;
			this.subtract = subtract;
			this.zeroIncrement = zeroIncrement;
			this.Reset(baseline);
		}

		public T Baseline { get; private set; }

		public void Reset(T baseline) {

			this.Baseline = baseline;
			this.currentOffsetSum = default;
			this.first = true;
			this.previousOffset = default;
		}

		public T CalculateOffset(T currentValue) {

			if(currentValue.CompareTo(this.Baseline) < 0) {
				throw new ArgumentOutOfRangeException(nameof(currentValue), $"value of {currentValue} is smaller than minimum acceptable baseline of {this.Baseline}.");
			}

			// a note here is that in the offset, a 0 counts as a spot. '0' is +1 relative to the previous one. (except for the first entry, which is the baseline)
			T relativeOffset = this.add(this.Baseline, this.currentOffsetSum);

			if(!this.first) {
				relativeOffset = this.add(relativeOffset, this.zeroIncrement);
			}

			this.previousOffset = this.subtract(currentValue, relativeOffset);

			return this.previousOffset;
		}

		public T RebuildValue(T offset) {

			this.previousOffset = offset;

			if(this.first) {
				return this.add( this.Baseline, offset);
			}

			// a note here is that in the offset, a 0 counts as a spot. '0' is +1 relative to the previous one.

			T relativeOffset = this.add(this.add(this.Baseline, this.currentOffsetSum), this.zeroIncrement);

			return this.add(offset, relativeOffset);
		}

		public void AddLastOffset() {

			this.currentOffsetSum = this.add(this.currentOffsetSum, this.previousOffset);

			if(!this.first) {
				this.currentOffsetSum = this.add(this.currentOffsetSum, this.zeroIncrement);
			}

			this.first = false;
		}
	}
}