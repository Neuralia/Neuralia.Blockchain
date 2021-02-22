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

		protected DBCONTEXT CreateContext(LockContext lockContext = null, Action<DBCONTEXT, LockContext> initializer = null) {
			DBCONTEXT db = this.CreateRawContext();

			this.PrepareContext(db, lockContext, initializer);

			return db;
		}

		protected void EnsureVersionCreated(LockContext lockContext) {
			this.PerformOperation(this.EnsureVersionCreated, lockContext);
		}

		protected void EnsureVersionCreated(DBCONTEXT db, LockContext lockContext) {
			((IEntityFrameworkContextInternal) db).EnsureVersionCreated(this.softwareVersion);
		}

		protected virtual void PerformInnerContextOperation(Action<DBCONTEXT,LockContext> action, LockContext lockContext = null, params object[] contents) {
			using(var handle = this.locker.Lock(lockContext)) {
				try {
					using(DBCONTEXT db = this.CreateContext(handle)) {
						
						if(TestingUtil.Testing) {
							using(TestingUtil.dbLocker.Lock()) {
								Repeater.Repeat(() => action(db, handle));
							}
						} else {
							Repeater.Repeat(() => action(db, handle));
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an Entity Framework action");

					throw;
				}
			}
		}

		protected virtual async Task PerformInnerContextOperationAsync(Func<DBCONTEXT, LockContext , Task> action, LockContext lockContext = null, params object[] contents) {
			if(action != null) {
				using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
					try {
						await using(DBCONTEXT db = this.CreateContext(handle)) {
							
							if(TestingUtil.Testing) {
								using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
									await Repeater.RepeatAsync(() => action(db, handle)).ConfigureAwait(false);
								}
							} else {
								await Repeater.RepeatAsync(() => action(db, handle)).ConfigureAwait(false);
							}
						}
					} catch(Exception ex) {
						NLog.Default.Error(ex, "exception occured during an Entity Framework action");

						throw;
					}
				}
			}
		}

		protected virtual async Task<T> PerformInnerContextOperationAsync<T>(Func<DBCONTEXT, LockContext, Task<T>> action, LockContext lockContext = null, params object[] contents) {
			using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				try {
					await using(DBCONTEXT db = this.CreateContext(handle)) {
						
						if(TestingUtil.Testing) {
							using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
								return await Repeater.RepeatAsync(() => action(db, handle)).ConfigureAwait(false);
							}
						} else {
							return await Repeater.RepeatAsync(() => action(db, handle)).ConfigureAwait(false);
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "exception occured during an Entity Framework action");

					throw;
				}
			}
		}

		protected virtual void PerformOperation(Action<DBCONTEXT, LockContext> process, LockContext lockContext = null, params object[] contents) {
			this.PerformInnerContextOperation(process, lockContext, contents);
		}

		protected virtual Task PerformOperationAsync(Func<DBCONTEXT, LockContext, Task> process, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync(process, lockContext, contents);
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT, LockContext>> processes, LockContext lockContext = null, params object[] contents) {
			this.PerformInnerContextOperation((db, lc) => this.PerformContextOperations(db, processes, lc), lockContext, contents);
		}

		protected virtual Task PerformOperationsAsync(IEnumerable<Func<DBCONTEXT, LockContext, Task>> processes, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync((db, lc) => this.PerformContextOperationsAsync(db, processes, lc), lockContext, contents);
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, LockContext, T> process, LockContext lockContext = null, params object[] contents) {
			T result = default;
			this.PerformInnerContextOperation((db, lc) => result = this.PerformContextOperation(db, process, lc), lockContext, contents);

			return result;
		}

		protected virtual Task<T> PerformOperationAsync<T>(Func<DBCONTEXT, LockContext, Task<T>> process, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync((db, lc) => this.PerformContextOperationAsync(db, process, lc), lockContext, contents);
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, LockContext, List<T>> process, LockContext lockContext = null, params object[] contents) {
			List<T> results = new List<T>();
			this.PerformInnerContextOperation((db, lc) => results.AddRange(this.PerformContextOperation(db, process, lc)), lockContext, contents);

			return results;
		}

		protected virtual Task<List<T>> PerformOperationAsync<T>(Func<DBCONTEXT, LockContext, Task<List<T>>> process, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync((db, lc) => this.PerformContextOperationAsync(db, process, lc), lockContext, contents);
		}

		protected void PerformTransaction(Action<DBCONTEXT, LockContext> process, LockContext lockContext = null, params object[] contents) {
			this.PerformInnerContextOperation((db, lc) => {

				db.Database.CreateExecutionStrategy().Execute(() => {
					using(IDbContextTransaction transaction = db.Database.BeginTransaction()) {
						try {
							if(TestingUtil.Testing) {
								using(TestingUtil.dbLocker.Lock()) {
									process(db, lc);
								}
							} else {
								process(db, lc);
							}
							

							transaction.Commit();
						} catch(Exception e) {
							transaction.Rollback();

							throw;
						}
					}
				});
			}, lockContext, contents);
		}

		protected Task PerformTransactionAsync(Func<DBCONTEXT, LockContext, Task> process, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync((db, lc) => {
				return db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
					await using(IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false)) {
						try {
							
							if(TestingUtil.Testing) {
								using(await TestingUtil.dbLocker.LockAsync().ConfigureAwait(false)) {
									await process(db, lc).ConfigureAwait(false);
								}
							} else {
								await process(db, lc).ConfigureAwait(false);
							}
							await transaction.CommitAsync().ConfigureAwait(false);
						} catch(Exception e) {
							await transaction.RollbackAsync().ConfigureAwait(false);

							throw;
						}
					}
				});
			}, lockContext, contents);
		}
		
		protected Task<T> PerformTransactionAsync<T>(Func<DBCONTEXT, LockContext, Task<T>> process, LockContext lockContext = null, params object[] contents) {
			return this.PerformInnerContextOperationAsync((db, lc) => {
				return db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
					await using(IDbContextTransaction transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false)) {
						try {
							var result = await process(db, lc).ConfigureAwait(false);

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
			}, lockContext, contents);
		}

		protected virtual void PerformContextOperations(DBCONTEXT db, IEnumerable<Action<DBCONTEXT, LockContext>> processes, LockContext lockContext) {
			foreach(Action<DBCONTEXT, LockContext> process in processes) {
				process(db, lockContext);
			}
		}

		protected virtual async Task PerformContextOperationsAsync(DBCONTEXT db, IEnumerable<Func<DBCONTEXT, LockContext, Task>> processes, LockContext lockContext) {
			foreach(var process in processes) {
				await process(db, lockContext).ConfigureAwait(false);
			}
		}

		protected virtual T PerformContextOperation<T>(DBCONTEXT db, Func<DBCONTEXT, LockContext, T> process, LockContext lockContext) {
			return process(db, lockContext);
		}

		protected virtual Task<T> PerformContextOperationAsync<T>(DBCONTEXT db, Func<DBCONTEXT, LockContext, Task<T>> process, LockContext lockContext) {
			return process(db, lockContext);
		}

		protected virtual void ClearDb() {
		}

		protected void PrepareContext(DBCONTEXT db, LockContext lockContext = null, Action<DBCONTEXT, LockContext> initializer = null) {
			db.SerializationType = this.serializationType;

			if(initializer != null) {
				initializer(db, lockContext);
			} else {
				this.InitContext(db, lockContext);
			}
		}

		protected virtual void InitContext(DBCONTEXT db, LockContext lockContext) {
			this.PerformCustomMappings(db, lockContext);
		}

		protected virtual void PerformCustomMappings(DBCONTEXT db, LockContext lockContext) {
		}
	}
}