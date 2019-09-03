using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Core.DataAccess {
	public interface IEntityFrameworkDal<out DBCONTEXT>
		where DBCONTEXT : IEntityFrameworkContext {
	}

	public abstract class EntityFrameworkDal<DBCONTEXT> : IEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IEntityFrameworkContext {

		private readonly Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator;
		protected readonly object locker = new object();
		protected readonly AppSettingsBase.SerializationTypes serializationType;

		public EntityFrameworkDal(Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) {
			this.contextInstantiator = contextInstantiator;
			this.serializationType = serializationType;
		}

		protected DBCONTEXT CreateContext(Action<DBCONTEXT> initializer = null) {
			lock(this.locker) {
				DBCONTEXT db = this.contextInstantiator(this.serializationType);

				this.PrepareContext(db, initializer);

				return db;
			}
		}

		protected virtual void PerformInnerContextOperation(Action<DBCONTEXT> action, params object[] contents) {
			using(DBCONTEXT db = this.CreateContext()) {
				action(db);
			}
		}

		protected virtual void PerformOperation(Action<DBCONTEXT> process, params object[] contents) {
			lock(this.locker) {
				this.PerformInnerContextOperation(process, contents);
			}
		}

		protected virtual void PerformOperations(IEnumerable<Action<DBCONTEXT>> processes, params object[] contents) {
			lock(this.locker) {
				this.PerformInnerContextOperation(db => this.PerformContextOperations(db, processes), contents);
			}
		}

		protected virtual T PerformOperation<T>(Func<DBCONTEXT, T> process, params object[] contents) {
			lock(this.locker) {
				T result = default;
				this.PerformInnerContextOperation(db => result = this.PerformContextOperation(db, process), contents);

				return result;
			}
		}

		protected virtual List<T> PerformOperation<T>(Func<DBCONTEXT, List<T>> process, params object[] contents) {
			lock(this.locker) {
				var results = new List<T>();
				this.PerformInnerContextOperation(db => results.AddRange(this.PerformContextOperation(db, process)), contents);

				return results;
			}
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

		protected virtual void PerformContextOperations(DBCONTEXT db, IEnumerable<Action<DBCONTEXT>> processes) {
			lock(this.locker) {

				foreach(var process in processes) {
					process(db);
				}
			}
		}

		protected virtual T PerformContextOperation<T>(DBCONTEXT db, Func<DBCONTEXT, T> process) {
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

		}
	}
}