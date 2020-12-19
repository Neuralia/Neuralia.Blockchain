using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Versions {

	public interface IComponentType : IBinarySerializable {
		ushort Value { get; set; }
	}

	public interface IComponentType<T> : IComponentType
		where T : SimpleUShort<T>, new() {
	}

	public class ComponentType<T> : AdaptiveShort1_2, IComponentType<T>
		where T : SimpleUShort<T>, new() {

		public ComponentType() {
		}

		public ComponentType(T value) : base(value.Value) {
		}

		public ComponentType(ushort value) : base(value) {
		}

		public ComponentType(AdaptiveNumber<ushort> other) : base(other) {
		}

		public new T Value {
			get => new T {Value = base.Value};
			set => base.Value = value.Value;
		}

		public bool Equals(ComponentType<T> other) {
			return this.Equals(other.Value);
		}

		public bool Equals(T other) {
			return this.Value.Equals(other);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj is ComponentType<T> compoent) {
				return this.Equals(compoent);
			}

			if(obj is T tcomponent) {
				return this.Equals(tcomponent);
			}

			return false;
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

		public static implicit operator ComponentType<T>(T d) {
			return (ComponentType<T>) d.Value;
		}

		public static explicit operator ComponentType<T>(byte value) {
			return new ComponentType<T>(value);
		}

		public static explicit operator ComponentType<T>(short value) {
			return new ComponentType<T>((ushort) value);
		}

		public static explicit operator ComponentType<T>(ushort value) {
			return new ComponentType<T>(value);
		}

		public static bool operator ==(ComponentType<T> c1, ComponentType<T> c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			return c1.Equals(c2);
		}

		public static bool operator !=(ComponentType<T> c1, ComponentType<T> c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentType<T> c1, T c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			return c1.Equals(c2);
		}

		public static bool operator !=(ComponentType<T> c1, T c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentType<T> c1, ushort c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return c1.Value.Equals(c2);
		}

		public static bool operator !=(ComponentType<T> c1, ushort c2) {
			return !(c1 == c2);
		}
	}
}