using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Tools {

	/// <summary>
	///     a utility class to run mutiple actions and ensure each is run, despite if any has exceptions. exceptions if any are
	///     finally aggregated and thrown
	/// </summary>
	public static class IndependentActionRunner {
		
		public static Task RunAsync(List<Func<Task>> actions) {

			return RunAsync(actions.Select(a => new ActionSetAsync(a)).ToArray());
		}
		
		public static async Task RunAsync(params ActionSetAsync[] actions) {

			List<Exception> exceptions = null;

			foreach(ActionSetAsync action in actions) {

				try {

					if(action.action != null) {
						await action.action();
					}

				} catch(Exception ex) {

					if(exceptions == null) {
						exceptions = new List<Exception>();
					}

					try {
						action.exception?.Invoke(ex);
					} catch(Exception ex2) {
						exceptions.Add(ex2);
					}

					exceptions.Add(ex);
				}
			}

			if((exceptions != null) && exceptions.Any()) {
				throw new AggregateException(exceptions);
			}
		}
		
		public static void Run(List<Action> actions) {

			Run(actions.Select(a => new ActionSet(a)).ToArray());
		}
		
		public static void Run(params Action[] actions) {

			Run(actions.Select(a => new ActionSet(a)).ToArray());
		}

		public static void Run(params ActionSet[] actions) {

			List<Exception> exceptions = null;

			foreach(ActionSet action in actions) {

				try {

					action.action?.Invoke();

				} catch(Exception ex) {

					if(exceptions == null) {
						exceptions = new List<Exception>();
					}

					try {
						action.exception?.Invoke(ex);
					} catch(Exception ex2) {
						exceptions.Add(ex2);
					}

					exceptions.Add(ex);
				}
			}

			if((exceptions != null) && exceptions.Any()) {
				throw new AggregateException(exceptions);
			}
		}
		
		public struct ActionSetAsync {

			public ActionSetAsync(Func<Task> action, Action<Exception> exception = null) {
				this.action = action;
				this.exception = exception;
			}

			public Func<Task> action;
			public Action<Exception> exception;
		}

		public struct ActionSet {

			public ActionSet(Action action, Action<Exception> exception = null) {
				this.action = action;
				this.exception = exception;
			}

			public Action action;
			public Action<Exception> exception;
		}
	}
}