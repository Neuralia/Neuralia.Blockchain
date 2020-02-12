using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Core.Extensions.DbSet {

	public static class DBsetExtensions {

		public static bool AnyLocal<T_ENTITY>(this DbSet<T_ENTITY> source,Func<T_ENTITY, bool> predicate)
			where T_ENTITY : class
		{
			return source.Local.Any(predicate);
		}
		
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

			return result ??= source.SingleOrDefault(predicate);

		}

		public static T_SOURCE SingleLocal<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			var compiled = predicate.Compile();


			return source.Local.Single(compiled);
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
		
		/// <summary>
		/// delete based on a predicate
		/// </summary>
		/// <param name="dbSet"></param>
		/// <param name="predicate"></param>
		/// <typeparam name="T"></typeparam>
		public static void Delete<T_SOURCE>(this DbSet<T_SOURCE> dbSet, DbContext dbContext, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			try {
				dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
				dbSet.RemoveRange(dbSet.Where(predicate));

			} finally {
				dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
			}
			
		}
		
		public static async Task<List<T_KEY>> DeleteById<T_SOURCE, T_KEY>(this DbSet<T_SOURCE> dbSet, DbContext dbContext, Expression<Func<T_SOURCE, T_KEY>> keysSelector, Expression<Func<T_SOURCE, bool>> selector, Func<T_KEY, T_SOURCE> factory)
			where T_SOURCE : class {

			var deleteIds = await dbSet.AsNoTracking().Where(selector).Select(keysSelector).ToListAsync();

			if(deleteIds.Any()) {
				try {
					dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

					// get the local ones first
					var allEntities = dbSet.Local.Where(selector.Compile()).ToList();

					// where is missing, we make up
					var localIds = allEntities.Select(keysSelector.Compile()).ToList();

					allEntities.AddRange(deleteIds.Where(e => !localIds.Contains(e)).Select(factory));

					dbSet.RemoveRange(allEntities);

				} finally {
					dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
				}
			}

			return deleteIds;
		}
	}

}