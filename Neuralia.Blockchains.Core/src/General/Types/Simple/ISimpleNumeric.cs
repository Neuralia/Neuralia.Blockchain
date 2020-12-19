using System;

namespace Neuralia.Blockchains.Core.General.Types.Simple {

	public interface ISimpleNumeric<T>
		where T : ISimpleNumeric<T> {

		T Clone();

		void Copy(T other);

		T Add(T b);

		T Increment();

		T Subtract(T b);

		T Decrement();

		T Multiply(T b);

		T Divide(T b);

		T PlusEqual(T b);

		T MinusEqual(T b);

		T MultiplyEqual(T b);

		T DivideEqual(T b);

		bool Equals(T b);

		bool NotEqual(T b);

		bool SmallerThan(T b);

		bool SmallerEqualThan(T b);

		bool GreaterThan(T b);

		bool GreaterEqualThan(T b);
	}

	public interface ISimpleNumericValue<U>
		where U : struct, IComparable, IConvertible, IFormattable, IComparable<U>, IEquatable<U> {
		U Value { get; set; }
	}

	public interface ISimpleNumeric<T, U> : ISimpleNumeric<T>, ISimpleNumericValue<U>
		where T : ISimpleNumeric<T, U>
		where U : struct, IComparable, IConvertible, IFormattable, IComparable<U>, IEquatable<U> {
	}
}