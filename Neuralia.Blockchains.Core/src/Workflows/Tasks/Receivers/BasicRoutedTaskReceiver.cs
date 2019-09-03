using System;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers {
	public class BasicRoutedTaskReceiver<T, U> : RoutedTaskReceiver<T>, IBasicRoutedTaskHandler<T, U>
		where T : class, IBasicTask<U>
		where U : class {
		/// <summary>
		///     this is the parameter we will pass to action calls
		/// </summary>
		protected readonly U parameter;

		public BasicRoutedTaskReceiver(U parameter) {
			this.parameter = parameter;
		}

		/// <summary>
		///     here we handle only our own returning tasks
		/// </summary>
		/// <param name="task"></param>
		protected override bool ProcessTask(T task) {
			try {
				task.TriggerAction(this.parameter);
			} catch(Exception ex) {
				Log.Error(ex, "Processing loop error");
			}

			return true;
		}
	}
}