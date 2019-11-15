using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess {
#if (NETSTANDARD2_0)
			public interface IEntityFrameworkContext : IDisposableExtended, IInfrastructure<IServiceProvider>, IDbContextDependencies, IDbSetCache, IDbQueryCache, IDbContextPoolable{
#else
	public interface IEntityFrameworkContext : IDisposableExtended, IAsyncDisposable, IInfrastructure<IServiceProvider>, IDbContextDependencies, IDbSetCache, IDbContextPoolable {
			
#endif

		DbContext Context { get; }

		AppSettingsBase.SerializationTypes SerializationType { get; set; }
		void EnsureCreated();
		void ForceFieldModified(object entity, string property);

		int SaveChanges();
	}

	public static class EntityFrameworkContext {

		public static DBCONTEXT CreateContext<DBCONTEXT>(AppSettingsBase.SerializationTypes serializationType)
			where DBCONTEXT : class, IEntityFrameworkContext, new() {

			return CreateContext(() => new DBCONTEXT(), serializationType);
		}

		public static DBCONTEXT CreateContext<DBCONTEXT>(Func<DBCONTEXT> getContext, AppSettingsBase.SerializationTypes serializationType)
			where DBCONTEXT : IEntityFrameworkContext {

			DBCONTEXT db = getContext();

			PrepareContext(db, serializationType);

			return db;
		}

		public static void PrepareContext(IEntityFrameworkContext context, AppSettingsBase.SerializationTypes serializationType) {
			context.SerializationType = serializationType;
		}
	}

	public abstract class EntityFrameworkContext<TContext> : DbContext, IEntityFrameworkContext
		where TContext : DbContext {

		private readonly object locker = new object();

		public EntityFrameworkContext() {
		}

		public EntityFrameworkContext(DbContextOptions<TContext> options) : base(options) {
		}

		private bool CanSave => this.SerializationType == AppSettingsBase.SerializationTypes.Master;

		public AppSettingsBase.SerializationTypes SerializationType { get; set; } = AppSettingsBase.SerializationTypes.Master;

		public override int SaveChanges() {

			if(!this.CanSave) {
				// only masters can save
				return 0;
			}

			return base.SaveChanges();
		}

		/// <summary>
		///     Ensure the database and tables have been created
		/// </summary>
		public virtual void EnsureCreated() {

			try {
				lock(this.locker) {
					RelationalDatabaseCreator databaseCreator = (RelationalDatabaseCreator) this.Database.GetService<IDatabaseCreator>();
					databaseCreator.EnsureCreated();
				}
			} catch(Exception ex) {
				Log.Error(ex, "Failed to create database schema");

				throw;
			}
		}

		/// <summary>
		///     For ce a field to be set as updated, even if entity framework may not detect it.
		///     This is useful when changing bytes inside a byte array, when EF does not detect the change
		/// </summary>
		/// <param name="db"></param>
		/// <param name="entity"></param>
		/// <param name="property"></param>
		/// <typeparam name="ENTITY"></typeparam>
		public void ForceFieldModified(object entity, string property) {

			this.Entry(entity).Property(property).IsModified = true;
		}

		public DbContext Context => this;
		public bool IsDisposed { get; }

		public override int SaveChanges(bool acceptAllChangesOnSuccess) {
			if(!this.CanSave) {
				// only masters can save
				return 0;
			}

			return base.SaveChanges(acceptAllChangesOnSuccess);
		}

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken()) {
			if(!this.CanSave) {
				// only masters can save
				return Task.FromResult(0);
			}

			return base.SaveChangesAsync(cancellationToken);
		}

		public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken()) {
			if(!this.CanSave) {
				// only masters can save
				return Task.FromResult(0);
			}

			return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		}
	}
}