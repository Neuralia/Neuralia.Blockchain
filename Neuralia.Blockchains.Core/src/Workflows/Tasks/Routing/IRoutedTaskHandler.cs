using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {
	public interface IRoutedTaskHandler {
	}

	public interface IRoutedTaskHandler<T> : IRoutedTaskHandler {
	}

	public interface IBasicRoutedTaskHandler : IRoutedTaskHandler {
	}

	public interface IBasicRoutedTaskHandler<T, U> : IBasicRoutedTaskHandler
		where T : IBasicTask<U>
		where U : class {
		void ReceiveTask(T task);
	}

	public interface ISimpleRoutedTaskHandler : IRoutedTaskHandler {
		void ReceiveTask(ISimpleTask task);
	}

	public interface IColoredRoutedTaskHandler : IRoutedTaskHandler {
		void ReceiveTask(IColoredTask task);
	}

	public interface IRoutedTaskRoutingHandler : IRoutedTaskHandler {

		bool Synchronous { get; set; }
		bool StashingEnabled { get; }
		ITaskRouter TaskRouter { get; }
		void ReceiveTask(IRoutedTask task);
		void ReceiveTaskSynchronous(IRoutedTask task);
		Task StashTask(InternalRoutedTask task);
		Task RestoreStashedTask(InternalRoutedTask task);
		Task<bool> CheckSingleTask(Guid taskId);
		Task Wait();
		Task Wait(TimeSpan timeout);
		Task DispatchSelfTask(IRoutedTask task, LockContext lockContext);
		Task DispatchTaskAsync(IRoutedTask task, LockContext lockContext);
		Task DispatchTaskNoReturnAsync(IRoutedTask task, LockContext lockContext);
		Task<bool> DispatchTaskSync(IRoutedTask task, LockContext lockContext);
		Task<bool> DispatchTaskNoReturnSync(IRoutedTask task, LockContext lockContext);
		Task<bool> WaitSingleTask(IRoutedTask task);
		Task<bool> WaitSingleTask(IRoutedTask task, TimeSpan timeout);
	}

	public interface IRoutedTaskRoutingHandler<U> : IRoutedTaskRoutingHandler
		where U : class {
	}
}