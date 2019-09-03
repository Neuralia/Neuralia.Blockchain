using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class BlockingCollectionExtensions {

		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		private static readonly object locker3 = new object();

		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		private static readonly object locker2 = new object();

		private static void Attempt<T>(Func<T, bool> process, T item, bool throwOnError = true, int maxAttemp = 10, int maxImmediateAttempt = 3) {
			bool succeeded = false;
			int counter = 0;

			do {
				// give it a try
				succeeded = process(item);

				if(!succeeded) {
					if(counter > maxImmediateAttempt) {
						Thread.Sleep(10);
					}

					counter++;
				}
			} while(!succeeded && (counter < maxAttemp));

			if(!succeeded && throwOnError) {
				throw new ApplicationException("Failed to insert item into the transactioning collection");
			}
		}

		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		public static T Take<T>(this BlockingCollection<T> collection) {

			T result = default;

			Attempt(c => c.TryTake(out result), collection, false, 5);

			return result;
		}

		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		public static void AddSafe<T>(this BlockingCollection<T> collection, T item) {

			Attempt(c => c.TryAdd(item), collection);

		}

		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		public static T Take<T>(this ConcurrentBag<T> collection) {

			T result = default;

			Attempt(c => c.TryTake(out result), collection, false, 5);

			return result;
		}

		public static K RemoveSafe<T, K>(this ConcurrentDictionary<T, K> collection, T key) {

			K result = default;

			lock(locker3) {
				if(collection.ContainsKey(key)) {

					Attempt(c => c.TryRemove(key, out result) && !collection.ContainsKey(key), collection);

					if(result == null) {
						throw new ArgumentNullException(nameof(result));
					}
				}
			}

			return result;
		}

		public static void AddSafe<T, K>(this ConcurrentDictionary<T, K> collection, T key, K item) {
			lock(locker2) {
				if(!collection.ContainsKey(key)) {
					Attempt(c => c.TryAdd(key, item) && collection.ContainsKey(key), collection);
				}
			}
		}
	}
}