using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Core.Extensions {

	public static class DBsetExtensions {

		/// <summary>
		///     Query both the database and local version at the same time.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="predicate"></param>
		/// <typeparam name="T_SOURCE"></typeparam>
		/// <returns></returns>
		public static T_SOURCE SingleOrDefaultAll<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			T_SOURCE result = source.Local.SingleOrDefault(predicate.Compile());

			return result ?? (result = source.SingleOrDefault(predicate));

		}

		public static T_SOURCE SingleAll<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			var compiled = predicate.Compile();

			if(source.Local.Any(compiled)) {
				return source.Local.Single(compiled);
			}

			return source.Single(predicate);
		}

		public static bool AnyAll<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			if(source.Local.Any(predicate.Compile())) {
				return true;
			}

			return source.Any(predicate);
		}

		/// <summary>
		///     the name sucks.  refactor...
		/// </summary>
		/// <param name="source"></param>
		/// <param name="predicate"></param>
		/// <typeparam name="T_SOURCE"></typeparam>
		/// <returns></returns>
		public static bool AllAll<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			if(source.Local.All(predicate.Compile())) {
				return true;
			}

			return source.All(predicate);

		}
	}

}