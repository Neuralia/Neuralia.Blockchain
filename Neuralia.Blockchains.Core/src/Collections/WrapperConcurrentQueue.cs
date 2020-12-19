using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Neuralia.Blockchains.Core.Collections {
	/// <summary>
	///     A special version of the ConcurrentQueue which fixes the memory leak issue
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class WrapperConcurrentQueue<T> : ConcurrentQueue<WrapperConcurrentQueue<T>.Wrapper<T>> {

		public void Enqueue(T item) {
			base.Enqueue(new Wrapper<T>(item));
		}

		public bool TryDequeue([MaybeNullWhen(false)] out T result) {

			bool worked = base.TryDequeue(out Wrapper<T> wrapper);

			result = worked ? wrapper.Retrieve() : default;

			return worked;
		}

		public new T[] ToArray() {
			return base.ToArray().Select(e => e.Entry).ToArray();
		}

		public struct Wrapper<T> {
			public Wrapper(T entry) {
				this.Entry = entry;
			}

			public T Entry { get; private set; }

			public T Retrieve() {
				T element = this.Entry;
				this.Entry = default;

				return element;
			}

			public bool Equals(Wrapper<T> obj) {

				if(obj.Entry == null && this.Entry == null) {
					return true;
				}
				return this.Entry.Equals(obj.Entry);
			}

			public override bool Equals(object obj)
            {
	            if(obj is Wrapper<T> wrapper) {
		            return this.Equals(wrapper);
	            }

	            return false;
            }

            public override int GetHashCode() {
	            return this.Entry?.GetHashCode()??0;
            }

            public static bool operator ==(Wrapper<T> left, Wrapper<T> right)
            {
                return left is { } && left.Equals(right);
            }

            public static bool operator !=(Wrapper<T> left, Wrapper<T> right)
            {
                return !(left == right);
            }
        }
	}
}