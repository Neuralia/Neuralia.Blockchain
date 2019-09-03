using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers {
	public class SimpleRoutedTaskReceiver : BasicRoutedTaskReceiver<ISimpleTask, object>, ISimpleRoutedTaskHandler {
		public SimpleRoutedTaskReceiver() : base(null) {
		}
	}
}