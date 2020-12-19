using System;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers {
	/// <summary>
	///     a special version of the task receive that lets the owner decide how to handle the action call
	/// </summary>
	public class DelegatingRoutedTaskRoutingReceiver : RoutedTaskRoutingReceiver {

		public DelegatingRoutedTaskRoutingReceiver(IRoutedTaskRoutingHandler owner, ICoordinatorTaskDispatcher coordinatorTaskDispatcher, bool enableStashing = true, int maxParallelTasks = 1, RouteMode routeMode = RouteMode.Emiter) : base(coordinatorTaskDispatcher, enableStashing, maxParallelTasks, routeMode) {
			this.Owner = owner;
		}

		protected override IRoutedTaskRoutingHandler Owner { get; }

		// <summary>
		/// Implement this event when a task was found and must be handled in whatever way
		/// </summary>
		public event Func<IRoutedTask, IRoutedTask> OnHandleTask;
	}
}