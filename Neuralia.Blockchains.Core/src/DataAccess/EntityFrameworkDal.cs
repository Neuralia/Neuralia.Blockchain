﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess {
	public interface IEntityFrameworkDal<out DBCONTEXT>
		where DBCONTEXT : IEntityFrameworkContext {
	}

	public abstract class EntityFrameworkDal<DBCONTEXT> : IEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IEntityFrameworkContext {

		private readonly Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator;
		protected readonly object locker = new object();
		protected readonly AppSettingsBase.SerializationTypes serializationType;
		protected readonly SoftwareVersion softwareVersion;
		
		public EntityFrameworkDal(SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) {
			this.contextInstantiator = contextInstantiator;
			this.serializationType = serializationType;
			this.softwareVersion = softwareVersion;
		}

		protected DBCONTEXT CreateRawContext(Action<DBCONTEXT> initializer = null) {
			lock(this.locker) {
				return this.contextInstantiator(this.serializationType);
			}
		}
		
		protected DBCONTEXT CreateContext(Action<DBCONTEXT> initializer = null) {
			lock(this.locker) {
				DBCONTEXT db = this.CreateRawContext();

				this.PrepareContext(db, initializer);

				return db;
			}
		}

		protected void EnsureVersionCreated() {
			this.PerformOperation(this.EnsureVersionCreated);
		}
		
		protected void EnsureVersionCreated(DBCONTEXT db) {

			((IEntityFrameworkContextInternal)db).EnsureVersionCreated(this.softwareVersion);
		}
		
		protected virtual void PerformInnerContextOperation(Action<DBCONTEXT> action, params object[] contents) {
			try {
				using(DBCONTEXT db = this.CreateContext()) {
					action(db);
				}
			} catch(Exception ex) {
				Log.Error(ex, "exception occured during an Entity Framework action");

				throw;
			}
		}
		
		protected virtual async Task PerformInnerContextOperationAsync(Func<DBCONTEXT, Task> action, params object[] contents) {
			try {
				using(DBCONTEXT db = this.CreateContext()) {
					await action(db);
				}
			} catch(Exception ex) {
				Log.Error(ex, "exception occured during an Entity Framework action");

				throw;
			}
		}
		
		protected virtual async Task<T> PerformInnerContextOperationAsync<T>(Func<DBCONTEXT, Task<T>> action, params object[] contents) {
			try {
				using(DBCONTEXT db = this.CreateContext()) {
					return await action(db);
				}
			} catch(Exception ex) {
				Log.Error(ex, "exception occured during an Entity Framework action");

				throw;
			}
		}

		protected virtual void PerformOperation(Action<DBCONTEXT> process, params object[] contents) {
			lock(this.locker) {
				this.PerformInnerContextOperation(process, contents);
			}
		}
		
		protected virtual Task PerformOperationAsync(Func<DBCONTEXT, Task> process, params object[] contents) {

				return this.PerformInnerContextOperationAsync(process, contents);
			
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT>> processes, params object[] contents) {
			lock(this.locker) {
				this.PerformInnerContextOperation(db => this.PerformContextOperations(db, processes), contents);
			}
		}

		protected virtual Task PerformOperationsAsync(IEnumerable<Func<DBCONTEXT, Task>> processes, params object[] contents) {

				return this.PerformInnerContextOperationAsync(db => this.PerformContextOperationsAsync(db, processes), contents);
			
		}
		
		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, params object[] contents) {
			lock(this.locker) {
				T result = default;
				this.PerformInnerContextOperation(db => result = this.PerformContextOperation(db, process), contents);

				return result;
			}
		}
		
		protected virtual Task<T> PerformOperationAsync<T>(Func<DBCONTEXT, Task<T>> process, params object[] contents) {

			return this.PerformInnerContextOperationAsync<T>(db => this.PerformContextOperationAsync(db, process), contents);
			
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, params object[] contents) {
			lock(this.locker) {
				var results = new List<T>();
				this.PerformInnerContextOperation(db => results.AddRange(this.PerformContextOperation(db, process)), contents);

				return results;
			}
		}

		protected virtual Task<List<T>> PerformOperationAsync<T>(Func<DBCONTEXT, Task<List<T>>> process, params object[] contents) {

			return this.PerformInnerContextOperationAsync(db => this.PerformContextOperationAsync(db, process), contents);
			
		}
		
		protected void PerformTransaction(Action<DBCONTEXT> process, params object[] contents) {

			lock(this.locker) {
				this.PerformInnerContextOperation(db => {
					IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();

					strategy.Execute(() => {

						using(IDbContextTransaction transaction = db.Database.BeginTransaction()) {
							try {
								process(db);

								transaction.Commit();

							} catch(Exception e) {
								transaction.Rollback();

								throw;
							}
						}
					});
				}, contents);
			}
		}
		
		protected Task PerformTransactionAsync(Func<DBCONTEXT, Task> process, params object[] contents) {

			lock(this.locker) {
				return this.PerformInnerContextOperationAsync(async db => {
					IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();

					await strategy.ExecuteAsync(async () => {

						using(IDbContextTransaction transaction = await db.Database.BeginTransactionAsync()) {
							try {
								await process(db);

								await transaction.CommitAsync();

							} catch(Exception e) {
								await transaction.RollbackAsync();

								throw;
							}
						}
					});
				}, contents);
			}
		}

		protected virtual void PerformContextOperations(DBCONTEXT db, IEnumerable<Action<DBCONTEXT>> processes) {
			lock(this.locker) {

				foreach(var process in processes) {
					process(db);
				}
			}
		}

		protected virtual async Task PerformContextOperationsAsync(DBCONTEXT db, IEnumerable<Func<DBCONTEXT, Task>> processes) {

			foreach(var process in processes) {
				await process(db);
			}
		}

		
		protected virtual T PerformContextOperation<T>(DBCONTEXT db, Func<DBCONTEXT, T> process) {
			lock(this.locker) {

				return process(db);
			}
		}
		
		protected virtual Task<T> PerformContextOperationAsync<T>(DBCONTEXT db, Func<DBCONTEXT, Task<T>> process) {
			lock(this.locker) {

				return process(db);
			}
		}

		protected virtual void ClearDb() {

		}

		protected void PrepareContext(DBCONTEXT db, Action<DBCONTEXT> initializer = null) {

			lock(this.locker) {
				db.SerializationType = this.serializationType;
			}

			if(initializer != null) {
				initializer(db);
			} else {
				this.InitContext(db);
			}
		}

		protected virtual void InitContext(DBCONTEXT db) {

			this.PerformCustomMappings(db);
		}
		
		protected virtual void PerformCustomMappings(DBCONTEXT db) {

		}
	}
}