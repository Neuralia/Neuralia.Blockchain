using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class CollectionExtensions {
		

		public static T[] Concat<T>(this T[] x, T[] y) {
			if(x == null) {
				throw new ArgumentNullException(nameof(x));
			}

			if(y == null) {
				throw new ArgumentNullException(nameof(y));
			}

			int oldLen = x.Length;
			Array.Resize(ref x, x.Length + y.Length);
			Array.Copy(y, 0, x, oldLen, y.Length);

			return x;
		}
		
		public static ConcurrentDictionary<KEY, ENTRY> ToConcurrentDictionary<SOURCE, KEY, ENTRY>(this IEnumerable<SOURCE> source, Func<SOURCE, KEY> keySelector, Func<SOURCE, ENTRY> elementSelector) {
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if (keySelector == null)
			{
				throw new ArgumentNullException(nameof(keySelector));
			}

			if (elementSelector == null)
			{
				throw new ArgumentNullException(nameof(elementSelector));
			}

			if (source is ICollection<SOURCE> collection)
			{
				if (collection.Count == 0)
				{
					return new ConcurrentDictionary<KEY, ENTRY>();
				}
			}

			var d = new ConcurrentDictionary<KEY, ENTRY>();
			foreach (SOURCE element in source)
			{
				d.AddSafe(keySelector(element), elementSelector(element));
			}

			return d;
		}
		

		/// <summary>
		///     This method will find the consecutive elements in the array and return them in groups.
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="minSequenceCount">The minimum of elements required for the sequence to be selected</param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static IEnumerable<IEnumerable<T>> FindConsecutive<T>(this IEnumerable<T> sequence, int minSequenceCount, Func<T, int, T> addIndex)
			where T : struct, IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T> {
			return sequence.ToDictionary(entry => entry).FindConsecutive(minSequenceCount, addIndex).Select(entry => entry.Select(entry2 => entry2.Key));
		}

		/// <summary>
		///     This method will find the consecutive elements in the array and return them in groups.
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="predicate">which element to select as the key to find sequences on</param>
		/// <param name="minSequenceCount">The minimum of elements required for the sequence to be selected</param>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="K"></typeparam>
		/// <returns></returns>
		public static IEnumerable<IEnumerable<KeyValuePair<K, T>>> FindConsecutive<T, K>(this IEnumerable<T> sequence, Func<T, K> predicate, int minSequenceCount, Func<K, int, K> addIndex)
			where K : IComparable<K> {
			return sequence.ToDictionary(predicate).FindConsecutive(minSequenceCount, addIndex);
		}

		/// <summary>
		///     This method will find the consecutive elements in the array and return them in groups.
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="minSequenceCount">The minimum of elements required for the sequence to be selected</param>
		/// <param name="addIndex">
		///     because of generics, we externalize the index addition. so in there, endsure it does return
		///     A+Index
		/// </param>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="K"></typeparam>
		/// <returns></returns>
		public static IEnumerable<IEnumerable<KeyValuePair<K, T>>> FindConsecutive<T, K>(this IDictionary<K, T> sequence, int minSequenceCount, Func<K, int, K> addIndex)
			where K : IComparable<K> {
			return sequence.GroupBy(entry => sequence.Where(entry2 => {

				return entry2.Key.CompareTo(entry.Key) >= 0;

			}).OrderBy(entry2 => entry2.Key).TakeWhile((entry2, index) => {

				K b = entry.Key;

				return entry2.Key.Equals(addIndex(b, index));
			}).Last()).Where(seq => seq.Count() >= minSequenceCount).Select(seq => seq.OrderBy(entry => entry.Key));
		}
		
		
		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		public static void AddDictionary<T, U>(this Dictionary<T, U> collection, IDictionary<T, U> other) {

			foreach((T key, U value) in other) {
				collection.Add(key, value);
			}
		}
		
	}
}