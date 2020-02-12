using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Core.General {

	/// <summary>
	/// a version with return
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	/// <typeparam name="R"></typeparam>
	public class OverrideSetFunc<PARAM, R> : OverrideSet<PARAM, ICaller<PARAM, R>, IOverridenCaller<PARAM, R>>
		where PARAM : class {

		public delegate bool AfterFunction(PARAM parameter, R lastResult, ref R combinedResults);
	
		public void AddSet<T>(Func<T, PARAM, R> action) {
			this.CheckTypeNotAddedy<T>();
			
			this.basic.Add(typeof(T), new SimpleCaller<T, PARAM, R>(action));
			this.Changed = true;
		}
	
		public void AddOverrideSet<C, P>(Func<C, PARAM, Func<P, PARAM, R>, R> action) {
			
			this.CheckTypeNotAddedy<C>();

			this.overloads.Add(typeof(C), (typeof(P), new OverridenCaller<C, PARAM, R>((c, x, callParent) => {
					                              if(action == null) {
						                              throw new ArgumentNullException("This class has no parent.");
					                              }
					                              
					                              return action(c, x, (c2, x2) => {

						                              if(callParent == null) {
							                              throw new ArgumentNullException("This class has no parent.");
						                              }
						                              return callParent.Run(c2, x2);
					                              });
				                              })));
			
			this.Changed = true;
		}
	
		public R Run<T>(T item, PARAM parameter, out bool hasRun, AfterFunction after = null) {
	
			R finalResult = default;
			hasRun = false;
			foreach(var entry in this.BuildHierarchy(item)) {
	
				if(entry.HasAction) {
					R lastResult = entry.Run(item, parameter);
					hasRun = true;
					bool shouldContinue = after?.Invoke(parameter, lastResult, ref finalResult)??true;
	
					if(!shouldContinue) {
						break;
					}
				}
			}
			
			return finalResult;
		}
	}

	/// <summary>
	/// a version without return
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	public class OverrideSetAction<PARAM> : OverrideSet<PARAM, ICaller<PARAM>, IOverridenCaller<PARAM>>
		where PARAM : class {

		public void AddSet<T>(Action<T, PARAM> action) {

			this.CheckTypeNotAddedy<T>();

			this.basic.Add(typeof(T), new SimpleCaller<T, PARAM>(action));
			this.Changed = true;
		}

		public void AddSet<C, P>(Action<C, PARAM, Action<P, PARAM>> action) {

			this.CheckTypeNotAddedy<C>();

			this.overloads.Add(typeof(C), (typeof(P), new OverridenCaller<C, PARAM>((c, x, callParent) => {
					                              action?.Invoke(c, x, (c2, x2) => callParent?.Run(c2, x2));
				                              })));

			this.Changed = true;
		}

		public void Run<T>(T item, PARAM parameter, out bool hasRun) {
			hasRun = false;
			foreach(var entry in this.BuildHierarchy(item)) {
				if(entry.HasAction) {
					entry.Run(item, parameter);
					hasRun = true;
				}
			}
		}
	}

	/// <summary>
	/// a class to handle calling functions based on type and considering a class hierarchy and polymorphism
	/// </summary>
	/// <typeparam name="PARAM"></typeparam>
	/// <typeparam name="CALLER"></typeparam>
	/// <typeparam name="OVERRIDE_CALLER"></typeparam>
	public abstract class OverrideSet<PARAM, CALLER, OVERRIDE_CALLER>
		where CALLER : ICallerBase
		where OVERRIDE_CALLER : IOverridenCallerBase<CALLER>, CALLER
		where PARAM : class {

		protected readonly Dictionary<Type, CALLER> basic = new Dictionary<Type, CALLER>();
		protected readonly Dictionary<Type, (Type parent, OVERRIDE_CALLER caller)> overloads = new Dictionary<Type, (Type parent, OVERRIDE_CALLER caller)>();

		private readonly Dictionary<Type, List<CALLER>> cache = new Dictionary<Type, List<CALLER>>();

		protected bool Changed { get; set; }

		protected void CheckTypeNotAddedy<T>(){
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
			var basics = this.basic.Where(s => s.Value.CanRun(item)).ToDictionary(E => E.Key, E => E.Value);
			// and the overrides that apply
			var overriden = this.overloads.Where(s => s.Value.caller.CanRun(item)).ToDictionary(E => E.Key, E => E.Value);

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
			var finals = basics.Select(v => v.Value).ToList();
			finals.AddRange(overriden.Select(e => (CALLER)e.Value.caller));

			this.cache.Add(item.GetType(), finals);

			return finals;
		}

		public bool Any<T>(T item) {
			return this.cache.ContainsKey(item.GetType()) || this.basic.Any(s => s.Value.CanRun(item));
		}
	}

	
	public interface ICallerBase {
		bool CanRun(object entry);
		bool HasAction { get; }
	}

	public interface ICaller<in PARAM> : ICallerBase {
		void Run(object entry, PARAM parameter);
	}

	public interface ICaller<in PARAM, out R> : ICallerBase {
		R Run(object entry, PARAM parameter);
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

		private readonly Action<T, PARAM> action;

		public SimpleCaller(Action<T, PARAM> action) {
			this.action = action;
		}

		public override bool HasAction => this.action != null;

		public void Run(object entry, PARAM parameter) {
			this.action((T) entry, parameter);
		}
	}

	public class SimpleCaller<T, PARAM, R> : CallerBase<T>, ICaller<PARAM, R> {

		private readonly Func<T, PARAM, R> action;

		public SimpleCaller(Func<T, PARAM, R> action) {
			this.action = action;
		}

		public override bool HasAction => this.action != null;

		public R Run(object entry, PARAM parameter) {
			return this.action((T) entry, parameter) ?? default;
		}
	}

	public class OverridenCaller<T, PARAM> : CallerBase<T>, IOverridenCaller<PARAM> {

		private readonly Action<T, PARAM, ICaller<PARAM>> action;

		public OverridenCaller(Action<T, PARAM, ICaller<PARAM>> action) {
			this.action = action;
		}

		public ICaller<PARAM> Parent { get; set; }
		public Type ParentType { get; set; }

		public override bool HasAction => this.action != null;
		public bool HasParent=> this.Parent != null;
		public void Run(object entry, PARAM parameter) {
			this.action((T) entry, parameter, this.Parent);
		}
	}

	public class OverridenCaller<T, PARAM, R> : CallerBase<T>, IOverridenCaller<PARAM, R> {

		private readonly Func<T, PARAM, ICaller<PARAM, R>, R> action;

		public OverridenCaller(Func<T, PARAM, ICaller<PARAM, R>, R> action) {
			this.action = action;
		}

		public ICaller<PARAM, R> Parent { get; set; }
		public Type ParentType { get; set; }

		public override bool HasAction => this.action != null;
		public bool HasParent=> this.Parent != null;

		public R Run(object entry, PARAM parameter) {
			return this.action((T) entry, parameter, this.Parent);
		}
	}
}