using System;
using System.Runtime.CompilerServices;

namespace Neuralia.Blockchains.Core.General.Types.Simple {

	public abstract class NamedSimpleUShort<T> : SimpleUShort<T>, IEquatable<T>
		where T : class, ISimpleNumeric<T, ushort>, new() {

		public NamedSimpleUShort() {

		}

		public NamedSimpleUShort(ushort value) {
			this.Value = value;
		}
		public abstract string ErrorPrefix { get; }
		public string ErrorName { get; set; }

		public static bool operator ==(NamedSimpleUShort<T> a, NamedSimpleUShort<T> b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(NamedSimpleUShort<T> a, NamedSimpleUShort<T> b) {
			return !(a == b);
		}

		public override string ToString() {
			return $"{this.ErrorPrefix}-{this.Value} ({this.ErrorName})";
		}

	}
}