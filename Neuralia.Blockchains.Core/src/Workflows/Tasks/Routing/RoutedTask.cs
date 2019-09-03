using System;
using System.Threading;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {
	public interface IRoutedTask : IDelegatedTask {
		string Destination { get; }
		string Name { get; set; }
		event Action OnCompleted;
	}

	public interface IRoutedTaskResult<KResults> : IRoutedTask {
		KResults Results { get; set; }
	}

	public interface IRoutedTask<out T> : IRoutedTask
		where T : IRoutedTaskRoutingHandler {

		void SetAction(Action<T, TaskRoutingContext> newAction);
		void SetCompleted(Action<TaskExecutionResults, TaskRoutingContext> newAction);
		void Set(Action<T, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted);
		void SetAction(Action<T, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted);
	}

	public interface IRoutedTask<out T, KResults> : IRoutedTask<T>, IRoutedTaskResult<KResults>
		where T : IRoutedTaskRoutingHandler {
	}

	public interface InternalRoutedTask : IRoutedTask {
		RoutedTask.ExecutionMode Mode { get; set; }
		TaskExecutionResults TaskExecutionResults { get; }
		IRoutedTaskRoutingHandler Caller { get; set; }
		ITaskRouter Router { get; }
		TaskChildrenContext ParentContext { get; set; }
		bool HasChildren { get; }
		int CallerThreadId { get; }
		new string Destination { get; set; }
		TaskChildrenContext ChildrenContext { get; }

		RoutedTask.RoutingStatuses RoutingStatus { get; set; }
		RoutedTask.ExecutionStatuses ExecutionStatus { get; set; }
		RoutedTask.StashStatuses StashStatus { get; set; }
		bool EnableSelfLoop { get; set; }

		string StackTrace { get; set; }

		void TriggerBaseAction(IRoutedTaskRoutingHandler service);
		void TriggerDispatchReturned();
		void TriggerStashCompleted();
		void TriggerOnCompleted();
		void SetStashCompleted(Action<TaskExecutionResults, TaskRoutingContext> stashCompleted);

		void ResetChildContext();
		void SetExecutionException(Exception exception);
	}

	public interface InternalRoutedTask<T> : InternalRoutedTask, IRoutedTask<T>
		where T : IRoutedTaskRoutingHandler {
		void TriggerAction(T service);
	}

	public interface InternalRoutedTask<T, KResults> : InternalRoutedTask<T>, IRoutedTask<T, KResults>
		where T : IRoutedTaskRoutingHandler {
	}

	/// <summary>
	///     a router message that is sent from one calling service to another. The action is executed on the dispatched
	///     service, and the dispatchReturned event happens on the calling service. Each of these methods can route children
	///     tasks,
	///     which are part of a TaskChildrenContext. All the tasks in this child context will be executed sequentially, and
	///     only when all have run (or are interrupted by an unhandled exception) will the parent be allowed to continue. The
	///     action can also stash the task and continue later once this task is done. Exceptions will interrupt any activity
	///     unless they are marked as handled. otherwise they will propagate up the task chain.
	/// </summary>
	public class RoutedTask<T, KResults> : DelegatedTask<T>, InternalRoutedTask<T, KResults>
		where T : IRoutedTaskRoutingHandler {
		private Action<T, TaskRoutingContext> action;
		private IRoutedTaskRoutingHandler caller;
		private TaskChildrenContext childrenContext;

		private Action<TaskExecutionResults, TaskRoutingContext> dispatchReturned;
		private Action<TaskExecutionResults, TaskRoutingContext> stashCompleted;

		public RoutedTask() : this(null, null) {

		}

		public RoutedTask(Action<T, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) : this("", newAction, newCompleted) {

		}

		public RoutedTask(string destination, Action<T, TaskRoutingContext> newAction = null, Action<TaskExecutionResults, TaskRoutingContext> newCompleted = null) {
			this.Destination = destination;
			this.Set(newAction, newCompleted);
		}

		public event Action OnCompleted;

		public void TriggerOnCompleted() {
			this.OnCompleted?.Invoke();
		}

		/// <summary>
		///     here we can store result values that will be sent back to the called
		/// </summary>
		/// <returns></returns>
		public KResults Results { get; set; }

		public RoutedTask.ExecutionMode Mode { get; set; } = RoutedTask.ExecutionMode.Async;
		public TaskExecutionResults TaskExecutionResults { get; } = new TaskExecutionResults();

		public string Destination { get; set; }
		public string Name { get; set; }

		public int CallerThreadId { get; private set; }

		/// <summary>
		///     The routing status of the task itself. Where it is in the chain of services
		/// </summary>
		public RoutedTask.RoutingStatuses RoutingStatus { get; set; } = RoutedTask.RoutingStatuses.New;

		public RoutedTask.ExecutionStatuses ExecutionStatus { get; set; } = RoutedTask.ExecutionStatuses.New;

		public RoutedTask.StashStatuses StashStatus { get; set; } = RoutedTask.StashStatuses.None;
		public bool EnableSelfLoop { get; set; } = false;

		/// <summary>
		///     Used for debugging purposes. tells us the stacktrace of the parent before dispatch
		/// </summary>
		public string StackTrace { get; set; }

		public IRoutedTaskRoutingHandler Caller {
			get => this.caller;
			set {
				this.caller = value;
				this.CallerThreadId = Thread.CurrentThread.ManagedThreadId;
			}
		}

		public ITaskRouter Router => this.Caller.TaskRouter;

		public TaskChildrenContext ParentContext { get; set; } = null;

		public bool HasChildren => (this.childrenContext != null) && this.childrenContext.HasMoreChildren;

		public TaskChildrenContext ChildrenContext {
			get {
				if(this.childrenContext == null) {
					this.ResetChildContext();
				}

				return this.childrenContext;
			}
		}

		public void SetAction(Action<T, TaskRoutingContext> newAction) {
			this.action = newAction;
		}

		public void SetCompleted(Action<TaskExecutionResults, TaskRoutingContext> newAction) {
			this.dispatchReturned = newAction;
		}

		public void Set(Action<T, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			this.SetAction(newAction);
			this.SetCompleted(newCompleted);
		}

		public void SetAction(Action<T, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) {
			this.Set(newAction, newCompleted);
		}

		public void SetStashCompleted(Action<TaskExecutionResults, TaskRoutingContext> stashCompleted) {
			this.stashCompleted = stashCompleted;
		}

		public void ResetChildContext() {
			this.childrenContext = new TaskChildrenContext(this, this.ParentContext);
		}

		public void SetExecutionException(Exception exception) {
			this.TaskExecutionResults.Exception = exception;
		}

		public void TriggerBaseAction(IRoutedTaskRoutingHandler service) {
			this.TriggerAction((T) service);

		}

		public void TriggerDispatchReturned() {
			TaskRoutingContext taskRoutingContext = new TaskRoutingContext(this.Caller, this);

			this.RegisterActiveTaskRoutingContext(taskRoutingContext);
			this.dispatchReturned?.Invoke(this.TaskExecutionResults, taskRoutingContext);

		}

		public void TriggerStashCompleted() {
			TaskRoutingContext taskRoutingContext = new TaskRoutingContext(this.Caller, this);

			this.RegisterActiveTaskRoutingContext(taskRoutingContext);
			this.stashCompleted?.Invoke(this.TaskExecutionResults, taskRoutingContext);

		}

		public void TriggerAction(T service) {
			TaskRoutingContext taskRoutingContext = new TaskRoutingContext(service, this);

			this.RegisterActiveTaskRoutingContext(taskRoutingContext);

			this.action?.Invoke(service, taskRoutingContext);

			this.ClearActiveTaskRoutingContext();
		}

		public static bool operator ==(RoutedTask<T, KResults> a, RoutedTask<T, KResults> b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(RoutedTask<T, KResults> a, RoutedTask<T, KResults> b) {
			return !(a == b);
		}

		protected bool Equals(RoutedTask<T, KResults> other) {
			return base.Equals(other);
		}

		private void RegisterActiveTaskRoutingContext(TaskRoutingContext taskRoutingContext) {
			TaskContextRegistry.Instance.RegisterActiveTaskContext(taskRoutingContext);
		}

		private void ClearActiveTaskRoutingContext() {
			TaskContextRegistry.Instance.ClearActiveTaskRoutingContext();
		}
	}

	public static class RoutedTask {

		public enum ExecutionMode {
			Sync,
			Async
		}

		public enum ExecutionStatuses {

			/// <summary>
			///     No action yet taken
			/// </summary>
			New,

			/// <summary>
			///     The children tasks have been dispatched and are in process
			/// </summary>
			ChildrenDispatched,

			/// <summary>
			///     The children tasks have run successfully. execution can resume
			/// </summary>
			ChildrenCompleted,

			/// <summary>
			///     Method has been run completely
			/// </summary>
			Executed
		}

		public enum RoutingStatuses {
			/// <summary>
			///     A new task that has not executed yet
			/// </summary>
			New,

			/// <summary>
			///     A task that is now on its destination thread, not yet executed
			/// </summary>
			Dispatched,

			/// <summary>
			///     returned to the source thread.
			/// </summary>
			Returned,

			/// <summary>
			///     Is all processed and fully disposed. do not reuse.
			/// </summary>
			Disposed
		}

		public enum StashStatuses {
			/// <summary>
			///     nothing stashed
			/// </summary>
			None,

			/// <summary>
			///     Task has been stashed
			/// </summary>
			Stashed
		}
	}
}