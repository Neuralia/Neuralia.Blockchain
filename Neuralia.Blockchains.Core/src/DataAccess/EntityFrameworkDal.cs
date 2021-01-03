using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.DataAccess {
	public interface IEntityFrameworkDal<out DBCONTEXT>
		where DBCONTEXT : IEntityFrameworkContext {
	}

	public abstract class EntityFrameworkDal<DBCONTEXT> : IEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IEntityFrameworkContext {
		private readonly Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator;
		protected readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();
		protected readonly AppSettingsBase.SerializationTypes serializationType;
		protected readonly SoftwareVersion softwareVersion;

		public EntityFrameworkDal(SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) {
			this.contextInstantiator = contextInstantiator;
			this.serializationType = serializationType;
			this.softwareVersion = softwareVersion;
		}

		protected DBCONTEXT CreateRawContext(Action<DBCONTEXT> initializer = null) {
			return this.contextInstantiator(this.serializationType);
		}

		protected DBCONTEXT CreateContext(Action<DBCONTEXT> initializer = null) {
			DBCONTEXT db = this.CreateRawContext();

			this.PrepareContext(db, initializer);

			return db;
		}

		protected void EnsureVersionCreated() {
			this.PerformOperation(this.EnsureVersionCreated);
		}

		protected void EnsureVersionCreated(DBCONTEXT db) {
			((IEntityFrameworkContextInternal) db).EnsureVersionCreated(this.softwareVersion);
		}

		protected virtual void PerformInnerContextOperation(Action<DBCONTEXT> action, params object[] contents) {
			using(this.locker.Lock()) {
				try {
					using(DBCONTEXT db = this.CreateContext()) {
						
						if(TestingUtil.Testing) {
							using(TestingUtil.dbLocker.Lock()) {
								Repeater.Repeat(() => action(db));
							}
						} else {
							Repeater.Repeat(() => action(db));
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an Entity Framework action");

					throw;
				}
			}
		}

		protected virtual async Task PerformInnerContextOperationAsync(Func<DBCONTEXT, Task> action, params object[] contents) {
			if(action != null) {
				using(await this.locker.LockAsync().ConfigureAwait(false)) {
					try {
						await using(DBCONTEXT db = this.CreateContext()) {
							
							if(TestingUtil.Testing) {
								using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
									await Repeater.RepeatAsync(() => action(db)).ConfigureAwait(false);
								}
							} else {
								await Repeater.RepeatAsync(() => action(db)).ConfigureAwait(false);
							}
						}
					} catch(Exception ex) {
						NLog.Default.Error(ex, "exception occured during an Entity Framework action");

						throw;
					}
				}
			}
		}

		protected virtual async Task<T> PerformInnerContextOperationAsync<T>(Func<DBCONTEXT, Task<T>> action, params object[] contents) {
			using(await this.locker.LockAsync().ConfigureAwait(false)) {
				try {
					await using(DBCONTEXT db = this.CreateContext()) {
						
						if(TestingUtil.Testing) {
							using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
								return await Repeater.RepeatAsync(() => action(db)).ConfigureAwait(false);
							}
						} else {
							return await Repeater.RepeatAsync(() => action(db)).ConfigureAwait(false);
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an Entity Framework action");

					throw;
				}
			}
		}

		protected virtual void PerformOperation(Action<DBCONTEXT> process, params object[] contents) {
			this.PerformInnerContextOperation(process, contents);
		}

		protected virtual Task PerformOperationAsync(Func<DBCONTEXT, Task> process, params object[] contents) {
			return this.PerformInnerContextOperationAsync(process, contents);
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT>> processes, params object[] contents) {
			this.PerformInnerContextOperation(db => this.PerformContextOperations(db, processes), contents);
		}

		protected virtual Task PerformOperationsAsync(IEnumerable<Func<DBCONTEXT, Task>> processes, params object[] contents) {
			return this.PerformInnerContextOperationAsync(db => this.PerformContextOperationsAsync(db, processes), contents);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, params object[] contents) {
			T result = default;
			this.PerformInnerContextOperation(db => result = this.PerformContextOperation(db, process), contents);

			return result;
		}

		protected virtual Task<T> PerformOperationAsync<T>(Func<DBCONTEXT, Task<T>> process, params object[] contents) {
			return this.PerformInnerContextOperationAsync(db => this.PerformContextOperationAsync(db, process), contents);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, params object[] contents) {
			List<T> results = new List<T>();
			this.PerformInnerContextOperation(db => results.AddRange(this.PerformContextOperation(db, process)), contents);

			return results;
		}

		protected virtual Task<List<T>> PerformOperationAsync<T>(Func<DBCONTEXT, Task<List<T>>> process, params object[] contents) {
			return this.PerformInnerContextOperationAsync(db => this.PerformContextOperationAsync(db, process), contents);
		}

		protected void PerformTransaction(Action<DBCONTEXT> process, params object[] contents) {
			this.PerformInnerContextOperation(db => {

				db.Database.CreateExecutionStrategy().Execute(() => {
					using(IDbContextTransaction transaction = db.Database.BeginTransaction()) {
						try {
							if(TestingUtil.Testing) {
								using(TestingUtil.dbLocker.Lock()) {
									process(db);
								}
							} else {
								process(db);
							}
							

							transaction.Commit();
						} catch(Exception e) {
							transaction.Rollback();

							throw;
						}
					}
				});
			}, contents);
		}

		protected Task PerformTransactionAsync(Func<DBCONTEXT, Task> process, params object[] contents) {
			return this.PerformInnerContextOperationAsync(db => {
				return db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
					await using(IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false)) {
						try {
							
							if(TestingUtil.Testing) {
								using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
									await process(db).ConfigureAwait(false);
								}
							} else {
								await process(db).ConfigureAwait(false);
							}
							await transaction.CommitAsync().ConfigureAwait(false);
						} catch(Exception e) {
							await transaction.RollbackAsync().ConfigureAwait(false);

							throw;
						}
					}
				});
			}, contents);
		}
		
		protected Task<T> PerformTransactionAsync<T>(Func<DBCONTEXT, Task<T>> process, params object[] contents) {
			return this.PerformInnerContextOperationAsync(db => {
				return db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
					await using(IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false)) {
						try {
							var result = await process(db).ConfigureAwait(false);

							if(TestingUtil.Testing) {
								using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
									await transaction.CommitAsync().ConfigureAwait(false);
								}
							} else {
								await transaction.CommitAsync().ConfigureAwait(false);
							}
							

							return result;
						} catch(Exception e) {
							await transaction.RollbackAsync().ConfigureAwait(false);

							throw;
						}
					}
				});
			}, contents);
		}

		protected virtual void PerformContextOperations(DBCONTEXT db, IEnumerable<Action<DBCONTEXT>> processes) {
			foreach(Action<DBCONTEXT> process in processes) {
				process(db);
			}
		}

		protected virtual async Task PerformContextOperationsAsync(DBCONTEXT db, IEnumerable<Func<DBCONTEXT, Task>> processes) {
			foreach(Func<DBCONTEXT, Task> process in processes) {
				await process(db).ConfigureAwait(false);
			}
		}

		protected virtual T PerformContextOperation<T>(DBCONTEXT db, Func<DBCONTEXT, T> process) {
			return process(db);
		}

		protected virtual Task<T> PerformContextOperationAsync<T>(DBCONTEXT db, Func<DBCONTEXT, Task<T>> process) {
			return process(db);
		}

		protected virtual void ClearDb() {
		}

		protected void PrepareContext(DBCONTEXT db, Action<DBCONTEXT> initializer = null) {
			db.SerializationType = this.serializationType;

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