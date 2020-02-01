using System;
using System.Runtime.CompilerServices;

namespace Neuralia.Blockchains.Core.General.Types.Simple {

	public class SimpleUShort<T> : ISimpleNumeric<T, ushort>, IEquatable<T>
		where T : class, ISimpleNumeric<T, ushort>, new() {

		public SimpleUShort() {

		}

		public SimpleUShort(ushort value) {
			this.Value = value;
		}

		public ushort Value { get; set; }

		public T Clone() {
			return new T {Value = this.Value};
		}

		public void Copy(T other) {
			this.Value = other.Value;
		}

		public bool Equals(ushort other) {
			return this.Value == other;
		}

		public bool Equals(SimpleUShort<T> other) {
			return this.Value == other.Value;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			return obj is T other && this.Equals(other);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

	#region Methods

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Add(T b) {
			return new T {Value = (ushort) (this.Value + b.Value)};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Increment() {
			this.Value++;

			return this as T;

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Subtract(T b) {
			return new T {Value = (ushort) (this.Value - b.Value)};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Decrement() {
			this.Value--;

			return this as T;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Multiply(T b) {
			return new T {Value = (ushort) (this.Value * b.Value)};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Divide(T b) {
			return new T {Value = (ushort) (this.Value / b.Value)};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T PlusEqual(T b) {
			this.Value += b.Value;

			return this as T;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T MinusEqual(T b) {
			this.Value -= b.Value;

			return this as T;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T MultiplyEqual(T b) {
			this.Value *= b.Value;

			return this as T;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T DivideEqual(T b) {
			this.Value /= b.Value;

			return this as T;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public virtual bool Equals(T b) {
			if(ReferenceEquals(null, b)) {
				return false;
			}

			return this.Value == b.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool NotEqual(T b) {
			return !this.Equals(b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool SmallerThan(T b) {
			return this.Value < b.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool SmallerEqualThan(T b) {
			return this.Value <= b.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool GreaterThan(T b) {
			return this.Value > b.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool GreaterEqualThan(T b) {
			return this.Value >= b.Value;
		}

	#endregion

	}

	public class SimpleUShort : SimpleUShort<SimpleUShort> {

		public SimpleUShort() {
		}

		public SimpleUShort(ushort value) : base(value) {
		}

	#region Operator Overloads

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator SimpleUShort(ushort d) {
			return new SimpleUShort(d);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ushort(SimpleUShort d) {
			return d.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator +(SimpleUShort c1, SimpleUShort c2) {
			return c1.Add(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator -(SimpleUShort c1, SimpleUShort c2) {
			return c1.Subtract(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator *(SimpleUShort c1, SimpleUShort c2) {
			return c1.Multiply(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator /(SimpleUShort c1, SimpleUShort c2) {
			return c1.Divide(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator ++(SimpleUShort c1) {
			return c1.Add(1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SimpleUShort operator --(SimpleUShort c1) {
			return c1.Subtract(1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(SimpleUShort c1, SimpleUShort c2) {
			return c1.Equals(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(SimpleUShort c1, SimpleUShort c2) {
			return !c1.Equals(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(SimpleUShort c1, SimpleUShort c2) {
			return c1.GreaterThan(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(SimpleUShort c1, SimpleUShort c2) {
			return c1.GreaterEqualThan(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(SimpleUShort c1, SimpleUShort c2) {
			return c1.SmallerThan(c2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(SimpleUShort c1, SimpleUShort c2) {
			return c1.SmallerEqualThan(c2);
		}

	#endregion

	}
}