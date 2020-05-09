using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class LinqExtensions {

		/// <summary>
		///     here we can select async from a collection
		/// </summary>
		/// <param name="source"></param>
		/// <param name="selector"></param>
		/// <typeparam name="TSource"></typeparam>
		/// <typeparam name="TResult"></typeparam>
		/// <returns></returns>
		public static Task<TResult[]> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> selector) {
			return Task.WhenAll(source.Select(selector));
		}

		/// <summary>
		///     This allows to select async and the filter using a where predicate.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="selector"></param>
		/// <param name="predicate"></param>
		/// <typeparam name="TSource"></typeparam>
		/// <typeparam name="TResult"></typeparam>
		/// <returns></returns>
		public static async Task<IEnumerable<TSource>> WhereAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> selector, Func<TResult, bool> predicate) {

			return (await source.SelectAsync(async e => {
					       return (source: e, selected: await selector(e).ConfigureAwait(false));
				       }).ConfigureAwait(false)).Where(e => {
				return predicate(e.selected);
			}).Select(e => e.source);
		}
	}
}