using System;
using System.Runtime.CompilerServices;
using LiteDB;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Core.General.Types.Specialized {
	/// <summary>
	///     A special class to represent a very precise decimal in a space efficient manner. does not support the entire range
	///     of a decimal, but enough range for our needs.
	/// </summary>
	public class Amount : AdaptiveDecimal {

		public Amount() {
		}

		public Amount(decimal value) : base(value) {
		}

		public Amount(int value) : base(value) {
		}

		public Amount(long value) : base(value) {
		}

		public Amount(float value) : base((decimal) value) {
		}

		public Amount(double value) : base((decimal) value) {
		}

		public Amount(Amount other) : base((AdaptiveDecimal) other) {
		}
		
	#region opeartor overloads

		public static implicit operator Amount(int value) {
			return new Amount(value);
		}

		public static implicit operator Amount(long value) {
			return new Amount(value);
		}

		public static implicit operator Amount(float value) {
			return new Amount(value);
		}

		public static implicit operator Amount(double value) {
			return new Amount(value);
		}

		public static implicit operator Amount(decimal value) {
			return new Amount(value);
		}

		public static implicit operator decimal(Amount value) {
			return value.Value;
		}

		public static explicit operator double(Amount value) {
			return (double) value.Value;
		}

		public static bool operator ==(Amount left, Amount right) {
			if(ReferenceEquals(null, left)) {
				return ReferenceEquals(null, right);
			}

			return left.Equals((AdaptiveDecimal) right);
		}

		public static bool operator ==(Amount left, decimal right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Equals(right);
		}

		public static bool operator !=(Amount left, Amount right) {
			return !(left == right);
		}

		public static bool operator !=(Amount left, decimal right) {
			return !(left == right);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator +(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value + c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator +(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value + c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator +(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value + c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator -(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value - c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator -(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value - c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator -(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value - c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator *(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value * c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator *(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value * c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator *(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value * c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator /(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				throw new DivideByZeroException();
			}

			return c1.Value / c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator /(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value / c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator /(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value / c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator ++(Amount c1) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			c1.Value++;

			return c1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Amount operator --(Amount c1) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			c1.Value--;

			return c1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value > c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value > c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value > c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value >= c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value >= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value >= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value < c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value < c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value < c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(Amount c1, Amount c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			if(ReferenceEquals(null, c2)) {
				c2 = new Amount();
			}

			return c1.Value <= c2.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(Amount c1, decimal c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value <= c2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(Amount c1, int c2) {
			if(ReferenceEquals(null, c1)) {
				c1 = new Amount();
			}

			return c1.Value <= c2;
		}

	#endregion

	}
}