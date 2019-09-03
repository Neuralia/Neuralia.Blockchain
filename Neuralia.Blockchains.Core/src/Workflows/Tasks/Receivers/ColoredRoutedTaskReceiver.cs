using System;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers {
	/// <summary>
	///     A special task receiver that does not execute action but allows to trigger actions based on message type
	/// </summary>
	public class ColoredRoutedTaskReceiver : BasicRoutedTaskReceiver<IColoredTask, object>, IColoredRoutedTaskHandler {
		private readonly Action<IColoredTask> handleTask;

		public ColoredRoutedTaskReceiver(Action<IColoredTask> handleTask) : base(null) {
			this.handleTask = handleTask;
		}

		/// <summary>
		///     here we handle only our own returning tasks
		/// </summary>
		/// <param name="task"></param>
		protected override bool ProcessTask(IColoredTask task) {
			try {
				this.handleTask(task);
			} catch(Exception ex) {
				Log.Error(ex, "Processing loop error");
			}

			return true;
		}
	}
}