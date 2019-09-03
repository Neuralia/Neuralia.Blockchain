using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Core.Tools {

	/// <summary>
	///     a utility class to run mutiple actions and ensure each is run, despite if any has exceptions. exceptions if any are
	///     finally aggregated and thrown
	/// </summary>
	public static class IndependentActionRunner {

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