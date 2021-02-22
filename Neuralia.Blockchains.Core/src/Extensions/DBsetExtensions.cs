using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Core.Extensions.DbSet {

	public static class DBsetExtensions {

		/// <summary>
		/// clear the tracking. mostly used for testing
		/// </summary>
		/// <param name="source"></param>
		public static void ClearLocal(this DbContext source)
			{
				source.ChangeTracker.Clear();
				source.SaveChanges();
		}
		
		public static bool AnyLocal<T_ENTITY>(this DbSet<T_ENTITY> source, Func<T_ENTITY, bool> predicate)
			where T_ENTITY : class {
			return source.Local.Any(predicate);
		}

		public static async Task<T_SOURCE> GetOrCreate<T_SOURCE>(this DbSet<T_SOURCE> dbSet, Func<T_SOURCE> prepare, Expression<Func<T_SOURCE, bool>> predicate = null)
			where T_SOURCE : class, new() {
			var entity = await dbSet.SingleOrDefaultAsync(predicate).ConfigureAwait(false);

			if(entity == null) {
				entity = prepare();
				await dbSet.AddAsync(entity).ConfigureAwait(false);
			}

			return entity;
		}

		public static async Task<bool> AddIfNotExists<T_SOURCE>(this DbSet<T_SOURCE> dbSet, Func<T_SOURCE> prepare, Expression<Func<T_SOURCE, bool>> predicate = null)
			where T_SOURCE : class, new() {
			if((prepare != null) && (predicate != null)) {
				if(await dbSet.AnyAsync(predicate).ConfigureAwait(false)) {
					return false;
				}

				T_SOURCE entity = prepare();
				dbSet.Add(entity);
			}

			return true;
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
		
		public static async Task<T_SOURCE> SingleOrDefaultAllAsync<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			T_SOURCE result = source.Local.SingleOrDefault(predicate.Compile());

			return result ??= await source.SingleOrDefaultAsync(predicate).ConfigureAwait(false);

		}

		public static T_SOURCE SingleLocal<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			Func<T_SOURCE, bool> compiled = predicate.Compile();

			return source.Local.Single(compiled);
		}

		public static T_SOURCE SingleAll<T_SOURCE>(this DbSet<T_SOURCE> source, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			Func<T_SOURCE, bool> compiled = predicate.Compile();

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
		///     delete based on a predicate
		/// </summary>
		/// <param name="dbSet"></param>
		/// <param name="predicate"></param>
		/// <typeparam name="T"></typeparam>
		public static void Delete<T_SOURCE>(this DbSet<T_SOURCE> dbSet, DbContext dbContext, Expression<Func<T_SOURCE, bool>> predicate)
			where T_SOURCE : class {

			var changeTracker = dbContext.ChangeTracker;
			try {
				//changeTracker.AutoDetectChangesEnabled = false;
				dbSet.RemoveRange(dbSet.Where(predicate));

			} finally {
				//changeTracker.AutoDetectChangesEnabled = true;
			}

		}
		
		public static string TableName<T_SOURCE>(this DbSet<T_SOURCE> dbSet, DbContext dbContext)
			where T_SOURCE : class {
			
			var entityType = dbContext.Model.FindEntityType(typeof(T_SOURCE));
			return entityType.GetTableName();
		}

		public static Task ClearTable<T_SOURCE>(this DbSet<T_SOURCE> dbSet, DbContext dbContext)
			where T_SOURCE : class {
			
			return dbContext.Database.ExecuteSqlRawAsync($"DELETE from \"{dbSet.TableName(dbContext)}\"");
		}

		public static async Task<List<T_KEY>> DeleteById<T_SOURCE, T_KEY>(this DbSet<T_SOURCE> dbSet, DbContext dbContext, Expression<Func<T_SOURCE, T_KEY>> keysSelector, Expression<Func<T_SOURCE, bool>> selector, Func<T_KEY, T_SOURCE> factory)
			where T_SOURCE : class {

			List<T_KEY> deleteIds = await dbSet.AsNoTracking().Where(selector).Select(keysSelector).ToListAsync().ConfigureAwait(false);

			if(deleteIds.Any()) {
				
				var changeTracker = dbContext.ChangeTracker;
				try {
					//changeTracker.AutoDetectChangesEnabled = false;

					// get the local ones first
					List<T_SOURCE> allEntities = dbSet.Local.Where(selector.Compile()).ToList();

					// where is missing, we make up
					List<T_KEY> localIds = allEntities.Select(keysSelector.Compile()).ToList();

					allEntities.AddRange(deleteIds.Where(e => !localIds.Contains(e)).Select(factory));

					dbSet.RemoveRange(allEntities);

				} finally {
					//changeTracker.AutoDetectChangesEnabled = true;
				}
			}

			return deleteIds;
		}

		public static async Task UpdateOrAddAsync<T>(this DbSet<T> dbSet, Expression<Func<T, bool>> predicate, Action<T, bool> operation)
			where T : class, new() {

			var entity = await dbSet.SingleOrDefaultAllAsync(predicate).ConfigureAwait(false);
			bool created = false;
			if(entity == null) {
				entity = new T();
	
				dbSet.Add(entity); //The keys are passed by argument, so we know we won't have to generate them. We use the non-async version that is much faster in that case and do not block the thread.
				created = true;
			}

			operation(entity, created);
		}
		
		/// <summary>
        ///     If the entity with the provided key(s) is already being tracked by the context or is found in the database, then it
        ///     will be tracked in the <see cref="EntityState.Modified" /> state.
        ///     If the entity associated to the key(s) is not already tracked or can't be found in the database, then the entity
        ///     will be tracked in the <see cref="EntityState.Added" /> state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dbSet"></param>
        /// <param name="entity"></param>
        /// <param name="keyValues"></param>
        public static void UpdateOrAdd<T>(this DbSet<T> dbSet, T entity, params object[] keyValues)
			where T : class {
			T trackedEntity;

			if((trackedEntity = dbSet.Find(keyValues)) != null) {
				if(!ReferenceEquals(trackedEntity, entity)) {
					dbSet.Attach(trackedEntity).State = EntityState.Detached;
				}

				dbSet.Update(entity);
			} else {
				dbSet.Add(entity);
			}
		}

		
		
        /// <summary>
        ///     If the entity with the provided key(s) is already being tracked by the context or is found in the database, then it
        ///     will be tracked in the <see cref="EntityState.Modified" /> state.
        ///     If the entity associated to the key(s) is not already tracked or can't be found in the database, then the entity
        ///     will be tracked in the <see cref="EntityState.Added" /> state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dbSet"></param>
        /// <param name="entity"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public static async Task UpdateOrAddAsync<T>(this DbSet<T> dbSet, T entity, params object[] keyValues)
			where T : class {
			T trackedEntity;

			if((trackedEntity = await dbSet.FindAsync(keyValues).ConfigureAwait(false)) != null) {
				if(!ReferenceEquals(trackedEntity, entity)) {
					dbSet.Attach(trackedEntity).State = EntityState.Detached;
				}

				dbSet.Update(entity);
			} else {
				dbSet.Add(entity); //The keys are passed by argument, so we know we won't have to generate them. We use the non-async version that is much faster in that case and do not block the thread.
			}
		}

        /// <summary>
        ///     If the entity with the provided key(s) is already being tracked by the context or is found in the database, then it
        ///     will be tracked in the <see cref="EntityState.Deleted" /> state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dbSet"></param>
        /// <param name="keyValues"></param>
        public static void RemoveIfExists<T>(this DbSet<T> dbSet, params object[] keyValues)
			where T : class {
			T entity;

			if((entity = dbSet.Find(keyValues)) != null) {
				dbSet.Remove(entity);
			}
		}

        /// <summary>
        ///     If the entity with the provided key(s) is already being tracked by the context or is found in the database, then it
        ///     will be tracked in the <see cref="EntityState.Deleted" /> state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dbSet"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public static async Task RemoveIfExistsAsync<T>(this DbSet<T> dbSet, params object[] keyValues)
			where T : class {
			T entity;

			if((entity = await dbSet.FindAsync(keyValues).ConfigureAwait(false)) != null) {
				dbSet.Remove(entity);
			}
		}

        /// <summary>
        ///     Every entity of the sequence that satisfy the condition will be tracked in the <see cref="EntityState.Deleted" />
        ///     state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dbSet"></param>
        /// <param name="predicate"></param>
        public static void RemoveManyOnCondition<T>(this DbSet<T> dbSet, Expression<Func<T, bool>> predicate)
			where T : class {
			foreach(T entity in dbSet.Where(predicate)) {
				dbSet.Remove(entity);
			}
		}
	}

}