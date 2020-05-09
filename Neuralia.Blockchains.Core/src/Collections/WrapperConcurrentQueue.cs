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

			result = worked ? wrapper.Retreive() : default;

			return worked;
		}

		public new T[] ToArray() {
			return base.ToArray().Select(e => e.Entry).ToArray();
		}

		public class Wrapper<T> {
			public Wrapper(T entry) {
				this.Entry = entry;
			}

			public T Entry { get; private set; }

			public T Retreive() {
				T element = this.Entry;
				this.Entry = default;

				return element;
			}
		}
	}
}