using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Logging;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IManagerBase : IRoutedTaskRoutingThread {
	}

	public interface IManagerBase<out T, out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IRoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IManagerBase
		where T : IManagerBase<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class ManagerBase<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : RoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IManagerBase<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where T : class, IManagerBase<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected ManagerBase(CENTRAL_COORDINATOR centralCoordinator, int maxParallelTasks) : base(centralCoordinator, maxParallelTasks) {

			this.Error2 += async (sender, ex) => {
				this.ExceptionOccured(ex);
			};
		}

		protected ManagerBase(CENTRAL_COORDINATOR centralCoordinator, int maxParallelTasks, int sleepTime) : base(centralCoordinator, maxParallelTasks, sleepTime) {
			this.Error2 += async (sender, ex) => {
				this.ExceptionOccured(ex);
			};
		}

		protected virtual void ExceptionOccured(Exception ex) {
			NLog.Default.Error(ex, "Exception occured,");
		}
	}
}