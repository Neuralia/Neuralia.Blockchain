using System.Collections.Generic;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers {

	public interface ISpecializedRoutedTaskRoutingReceiver : IRoutedTaskRoutingReceiver {
	}

	public interface ISpecializedRoutedTaskRoutingReceiver<T> : ISpecializedRoutedTaskRoutingReceiver {
	}

	/// <summary>
	///     a special verison of the task receiver that will not need generics, but late bind the action.
	/// </summary>
	public class SpecializedRoutedTaskRoutingReceiver<T> : RoutedTaskRoutingReceiver, ISpecializedRoutedTaskRoutingReceiver<T>
		where T : IRoutedTaskRoutingHandler {
		protected readonly T owner;

		protected List<IRoutedTask> shelvedTasks = new List<IRoutedTask>();

		public SpecializedRoutedTaskRoutingReceiver(ICoordinatorTaskDispatcher coordinatorTaskDispatcher, T owner, bool enableStashing = true, int maxParallelTasks = 1, RouteMode routeMode = RouteMode.Emiter) : base(coordinatorTaskDispatcher, enableStashing, maxParallelTasks, routeMode) {
			this.owner = owner;
		}

		protected override IRoutedTaskRoutingHandler Owner => this.owner;
	}
}