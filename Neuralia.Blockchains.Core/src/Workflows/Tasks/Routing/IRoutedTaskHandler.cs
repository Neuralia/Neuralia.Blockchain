using System;

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
		void StashTask(InternalRoutedTask task);
		void RestoreStashedTask(InternalRoutedTask task);
		bool CheckSingleTask(Guid taskId);
		void Wait();
		void Wait(TimeSpan timeout);
		void DispatchSelfTask(IRoutedTask task);
		void DispatchTaskAsync(IRoutedTask task);
		void DispatchTaskNoReturnAsync(IRoutedTask task);
		bool DispatchTaskSync(IRoutedTask task);
		bool DispatchTaskNoReturnSync(IRoutedTask task);
		bool WaitSingleTask(IRoutedTask task);
		bool WaitSingleTask(IRoutedTask task, TimeSpan timeout);
	}

	public interface IRoutedTaskRoutingHandler<U> : IRoutedTaskRoutingHandler
		where U : class {
	}
}