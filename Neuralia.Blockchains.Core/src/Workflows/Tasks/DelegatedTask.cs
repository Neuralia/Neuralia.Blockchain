using System;

namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public interface IDelegatedTask {
		Guid Id { get; }
	}

	public interface IDelegatedTaskIn<in T> : IDelegatedTask {
	}

	public interface IDelegatedTaskOut<out T> : IDelegatedTask {
	}

	public interface IDelegatedTask<T> : IDelegatedTaskOut<T>, IDelegatedTaskIn<T> {
	}

	public class DelegatedTask<T> : IDelegatedTask<T> {

		public Guid Id { get; } = Guid.NewGuid(); // for correlation

		protected bool Equals(DelegatedTask<T> other) {
			if(other == null) {
				return false;
			}

			return this.Id.Equals(other.Id);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((DelegatedTask<T>) obj);
		}

		public override int GetHashCode() {
			return this.Id.GetHashCode();
		}

		public static bool operator ==(DelegatedTask<T> a, DelegatedTask<T> b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(DelegatedTask<T> a, DelegatedTask<T> b) {
			return !(a == b);
		}
	}
}