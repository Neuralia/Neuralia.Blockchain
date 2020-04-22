using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers {
	/// <summary>
	///     A special task receiver that does not execute action but allows to trigger actions based on message type
	/// </summary>
	public class ColoredRoutedTaskReceiver : BasicRoutedTaskReceiver<IColoredTask, object>, IColoredRoutedTaskHandler {
		private readonly Func<IColoredTask, Task> handleTask;

		public ColoredRoutedTaskReceiver(Func<IColoredTask, Task> handleTask) : base(null) {
			this.handleTask = handleTask;
		}

		/// <summary>
		///     here we handle only our own returning tasks
		/// </summary>
		/// <param name="task"></param>
		/// <param name="lockContext"></param>
		protected override async Task<bool> ProcessTask(IColoredTask task) {
			try {
				await this.handleTask(task).ConfigureAwait(false);
			} catch(Exception ex) {
				Log.Error(ex, "Processing loop error");
			}

			return true;
		}
	}
}