using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess {

	public interface IEntityFrameworkContext : IDisposableExtended, IAsyncDisposable, IInfrastructure<IServiceProvider>, IDbContextDependencies, IDbSetCache, IDbContextPoolable {

		DbContext Context { get; }

		AppSettingsBase.SerializationTypes SerializationType { get; set; }

		DbSet<DBVersion> Versions { get; set; }
		void EnsureCreated();
		void ForceFieldModified(object entity, string property);

		int SaveChanges();
		Task<int> SaveChangesAsync();
	}

	public interface IEntityFrameworkContextInternal : IEntityFrameworkContext {
		void EnsureVersionCreated(SoftwareVersion softwareVersion);
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

	public abstract class EntityFrameworkContext<TContext> : DbContext, IEntityFrameworkContext, IEntityFrameworkContextInternal
		where TContext : DbContext {

		private readonly object locker = new object();

		public EntityFrameworkContext() {
		}

		public EntityFrameworkContext(DbContextOptions<TContext> options) : base(options) {
		}

		private bool CanSave => this.SerializationType == AppSettingsBase.SerializationTypes.Master;
		public DbSet<DBVersion> Versions { get; set; }

		public AppSettingsBase.SerializationTypes SerializationType { get; set; } = AppSettingsBase.SerializationTypes.Master;

		public override int SaveChanges() {

			if(!this.CanSave) {
				// only masters can save
				return 0;
			}

			return base.SaveChanges();
		}

		public Task<int> SaveChangesAsync() {
			if(!this.CanSave) {
				// only masters can save
				return Task.FromResult(0);
			}

			return base.SaveChangesAsync();
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
				NLog.Default.Error(ex, "Failed to create database schema");

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

		public void EnsureVersionCreated(SoftwareVersion softwareVersion) {
			if(!this.Versions.Any()) {
				DBVersion version = new DBVersion();

				version.Id = 1;
				version.Major = softwareVersion.Major;
				version.Minor = softwareVersion.Minor;
				version.Revision = softwareVersion.Revision;
				version.LastUpdate = DateTimeEx.CurrentTime;

				this.Versions.Add(version);

				this.SaveChanges();
			}
		}

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