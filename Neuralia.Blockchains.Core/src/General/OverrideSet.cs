using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.General {

	/// <summary>
	///     a version with return
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	/// <typeparam name="R"></typeparam>
	public class OverrideSetFunc<PARAM, R> : OverrideSet<PARAM, ICaller<PARAM, R>, IOverridenCaller<PARAM, R>>
		where PARAM : class {

		public delegate bool AfterFunction(PARAM parameter, R lastResult, ref R combinedResults);

		public void AddSet<T>(Func<T, PARAM, LockContext, Task<R>> action) {
			this.CheckTypeNotAddedy<T>();

			this.basic.Add(typeof(T), new SimpleCaller<T, PARAM, R>(action));
			this.Changed = true;
		}

		public void AddOverrideSet<C, P>(Func<C, PARAM, Func<P, PARAM, LockContext, Task<R>>, LockContext, Task<R>> action) {

			this.CheckTypeNotAddedy<C>();

			this.overloads.Add(typeof(C), (typeof(P), new OverridenCaller<C, PARAM, R>((c, x, callParent, lc) => {
					                              if(action == null) {
						                              throw new ArgumentNullException("This class has no parent.");
					                              }

					                              return action(c, x, (c2, x2, lc2) => {

						                              if(callParent == null) {
							                              throw new ArgumentNullException("This class has no parent.");
						                              }

						                              return callParent.Run(c2, x2, lc2);
					                              }, lc);
				                              })));

			this.Changed = true;
		}

		public async Task<(R result, bool hashRun)> Run<T>(T item, PARAM parameter, LockContext lockContext, AfterFunction after = null) {

			R finalResult = default;
			bool hasRun = false;

			foreach(ICaller<PARAM, R> entry in this.BuildHierarchy(item)) {

				if(entry.HasAction) {
					R lastResult = await entry.Run(item, parameter, lockContext).ConfigureAwait(false);
					hasRun = true;
					bool shouldContinue = true;

					if(after != null) {
						shouldContinue = after(parameter, lastResult, ref finalResult);
					}

					if(!shouldContinue) {
						break;
					}
				}
			}

			return (finalResult, hasRun);
		}
	}

	/// <summary>
	///     a version without return
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	public class OverrideSetAction<PARAM> : OverrideSet<PARAM, ICaller<PARAM>, IOverridenCaller<PARAM>>
		where PARAM : class {

		public void AddSet<T>(Func<T, PARAM, LockContext, Task> action) {

			this.CheckTypeNotAddedy<T>();

			this.basic.Add(typeof(T), new SimpleCaller<T, PARAM>(action));
			this.Changed = true;
		}

		public void AddSet<C, P>(Func<C, PARAM, Func<P, PARAM, LockContext, Task>, LockContext, Task> action) {

			this.CheckTypeNotAddedy<C>();

			this.overloads.Add(typeof(C), (typeof(P), new OverridenCaller<C, PARAM>((c, x, callParent, lc) => {
					                              if(action == null) {
						                              return Task.CompletedTask;
					                              }

					                              return action(c, x, async (c2, x2, lc2) => {
						                              if(callParent != null) {
							                              await callParent.Run(c2, x2, lc2).ConfigureAwait(false);
						                              }
					                              }, lc);
				                              })));

			this.Changed = true;
		}

		public async Task<bool> Run<T>(T item, PARAM parameter, LockContext lockContext) {
			bool hasRun = false;

			foreach(ICaller<PARAM> entry in this.BuildHierarchy(item)) {
				if(entry.HasAction) {
					Task task = entry.Run(item, parameter, lockContext);

					if(task != null) {
						await task.ConfigureAwait(false);
					}

					hasRun = true;
				}
			}

			return hasRun;
		}
	}

	/// <summary>
	///     a class to handle calling functions based on type and considering a class hierarchy and polymorphism
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	/// <typeparam name="CALLER"></typeparam>
	/// <typeparam name="OVERRIDE_CALLER"></typeparam>
	public abstract class OverrideSet<PARAM, CALLER, OVERRIDE_CALLER>
		where CALLER : ICallerBase
		where OVERRIDE_CALLER : IOverridenCallerBase<CALLER>, CALLER
		where PARAM : class {

		protected readonly Dictionary<Type, CALLER> basic = new Dictionary<Type, CALLER>();

		private readonly Dictionary<Type, List<CALLER>> cache = new Dictionary<Type, List<CALLER>>();
		protected readonly Dictionary<Type, (Type parent, OVERRIDE_CALLER caller)> overloads = new Dictionary<Type, (Type parent, OVERRIDE_CALLER caller)>();

		protected bool Changed { get; set; }

		protected void CheckTypeNotAddedy<T>() {
			if(this.basic.ContainsKey(typeof(T)) || this.overloads.ContainsKey(typeof(T))) {
				throw new ArgumentException("type already added");
			}

		}

		protected IEnumerable<CALLER> BuildHierarchy<T>(T item) {

			if(this.Changed) {
				this.cache.Clear();
			}

			this.Changed = false;

			if(this.cache.ContainsKey(item.GetType())) {
				return this.cache[item.GetType()];
			}

			// get the basic types which apply
			Dictionary<Type, CALLER> basics = this.basic.Where(s => s.Value.CanRun(item)).ToDictionary(E => E.Key, E => E.Value);

			// and the overrides that apply
			Dictionary<Type, (Type parent, OVERRIDE_CALLER caller)> overriden = this.overloads.Where(s => s.Value.caller.CanRun(item)).ToDictionary(E => E.Key, E => E.Value);

			void RemoveParents(Type parent, OVERRIDE_CALLER caller) {

				// remove any basic type that is overriden by an override
				if(basics.ContainsKey(parent)) {
					caller.Parent = basics[parent];
					basics.Remove(parent);
				}

				// and remove any override that is also overriden by a further override
				if(overriden.ContainsKey(parent)) {
					caller.Parent = overriden[parent].caller;
					RemoveParents(overriden[parent].parent, overriden[parent].caller);
					overriden.Remove(parent);
				}
			}

			// now we rebuild the hierarchy by removing types that are overriden
			foreach((Type parent, OVERRIDE_CALLER caller) in overriden.Values.ToList()) {
				RemoveParents(parent, caller);
			}

			// lets bring it all back together, this is our call hierarchy
			List<CALLER> finals = basics.Select(v => v.Value).ToList();
			finals.AddRange(overriden.Select(e => (CALLER) e.Value.caller));

			this.cache.Add(item.GetType(), finals);

			return finals;
		}

		public bool Any<T>(T item) {
			return this.cache.ContainsKey(item.GetType()) || this.basic.Any(s => s.Value.CanRun(item));
		}
	}

	public interface ICallerBase {
		bool HasAction { get; }
		bool CanRun(object entry);
	}

	public interface ICaller<in PARAM> : ICallerBase {
		Task Run(object entry, PARAM parameter, LockContext lockContext);
	}

	public interface ICaller<in PARAM, R> : ICallerBase {
		Task<R> Run(object entry, PARAM parameter, LockContext lockContext);
	}

	public interface IOverridenCallerBase<P> : ICallerBase
		where P : ICallerBase {
		Type ParentType { get; set; }
		P Parent { get; set; }
		bool HasParent { get; }
	}

	public interface IOverridenCaller<PARAM> : ICaller<PARAM>, IOverridenCallerBase<ICaller<PARAM>> {
	}

	public interface IOverridenCaller<PARAM, R> : ICaller<PARAM, R>, IOverridenCallerBase<ICaller<PARAM, R>> {
	}

	public abstract class CallerBase<T> : ICallerBase {

		public bool CanRun(object entry) {
			return entry is T;
		}

		public abstract bool HasAction { get; }
	}

	public class SimpleCaller<T, PARAM> : CallerBase<T>, ICaller<PARAM> {

		private readonly Func<T, PARAM, LockContext, Task> action;

		public SimpleCaller(Func<T, PARAM, LockContext, Task> action) {
			this.action = action;
		}

		public override bool HasAction => this.action != null;

		public Task Run(object entry, PARAM parameter, LockContext lockContext) {
			return this.action((T) entry, parameter, lockContext);
		}
	}

	public class SimpleCaller<T, PARAM, R> : CallerBase<T>, ICaller<PARAM, R> {

		private readonly Func<T, PARAM, LockContext, Task<R>> action;

		public SimpleCaller(Func<T, PARAM, LockContext, Task<R>> action) {
			this.action = action;
		}

		public override bool HasAction => this.action != null;

		public Task<R> Run(object entry, PARAM parameter, LockContext lockContext) {
			return this.action((T) entry, parameter, lockContext) ?? default;
		}
	}

	public class OverridenCaller<T, PARAM> : CallerBase<T>, IOverridenCaller<PARAM> {

		private readonly Func<T, PARAM, ICaller<PARAM>, LockContext, Task> action;

		public OverridenCaller(Func<T, PARAM, ICaller<PARAM>, LockContext, Task> action) {
			this.action = action;
		}

		public ICaller<PARAM> Parent { get; set; }
		public Type ParentType { get; set; }

		public override bool HasAction => this.action != null;
		public bool HasParent => this.Parent != null;

		public Task Run(object entry, PARAM parameter, LockContext lockContext) {
			return this.action((T) entry, parameter, this.Parent, lockContext);
		}
	}

	public class OverridenCaller<T, PARAM, R> : CallerBase<T>, IOverridenCaller<PARAM, R> {

		private readonly Func<T, PARAM, ICaller<PARAM, R>, LockContext, Task<R>> action;

		public OverridenCaller(Func<T, PARAM, ICaller<PARAM, R>, LockContext, Task<R>> action) {
			this.action = action;
		}

		public ICaller<PARAM, R> Parent { get; set; }
		public Type ParentType { get; set; }

		public override bool HasAction => this.action != null;
		public bool HasParent => this.Parent != null;

		public Task<R> Run(object entry, PARAM parameter, LockContext lockContext) {
			return this.action((T) entry, parameter, this.Parent, lockContext);
		}
	}
}