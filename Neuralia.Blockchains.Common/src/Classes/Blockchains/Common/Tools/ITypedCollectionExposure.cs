using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {

	public interface ITypedCollectionExposure {
	}

	public interface ITypedCollectionExposure<T> : ITypedCollectionExposure {

		ImmutableList<T> CollectionCopy { get; }

		void ClearCollection();
		void CreateNewCollectionEntry(out T result);
		void AddCollectionEntry(T entry);
		void RemoveCollectionEntry(Func<T, bool> predicate);
		T GetCollectionEntry(Func<T, bool> predicate);
		List<T> GetCollectionEntries(Func<T, bool> predicate);
	}

	/// <summary>
	///     A utility class to ensure we can access collectoins from a sub class where strong typing might not be available
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public static class TypedCollectionExposureUtil<T> {

		public static void CreateNewCollectionEntry(IList array, out T result) {
			Type genericType = array.GetType().GetGenericArguments().Single();

			result = (T) Activator.CreateInstance(genericType);
		}

		public static void AddCollectionEntry<K>(T entry, List<K> array) {

			if(entry is K castedEntry) {
				array.Add(castedEntry);
			} else {
				throw new InvalidCastException();
			}
		}

		public static void RemoveCollectionEntry<K>(Func<T, bool> predicate, List<K> array)
			where K : T {
			array.RemoveAll(e => predicate(e));
		}

		public static T GetCollectionEntry<K>(Func<T, bool> predicate, List<K> array)
			where K : T {

			return array.SingleOrDefault(e => predicate(e));
		}

		public static List<T> GetCollectionEntries<K>(Func<T, bool> predicate, List<K> array)
			where K : T {
			return array.Where(e => predicate(e)).Cast<T>().ToList();
		}

		public static ImmutableList<T> GetCollection<K>(List<K> array) {

			return array.Cast<T>().ToImmutableList();
		}
	}
}