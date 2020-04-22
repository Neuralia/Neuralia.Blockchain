using System;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {

	public interface ITaskRouter {
		Task<bool> RouteTask(IRoutedTask task);
		Task<bool> RouteTask(IRoutedTask task, string destination);
		Task<bool> IsWalletProviderTransaction(IRoutedTask task);
	}
}