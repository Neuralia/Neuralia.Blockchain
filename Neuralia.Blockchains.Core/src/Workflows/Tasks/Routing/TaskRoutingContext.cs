using System;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {

	public interface ITaskStasher {
		bool StashingEnabled { get; }
		void Stash();
		void CompleteStash();
	}

	/// <summary>
	///     This is a helper class that a task can use during its execution to perform various operations, such as route
	///     further children tasks, wait, etc.
	/// </summary>
	public class TaskRoutingContext : ITaskStasher {
		private readonly InternalRoutedTask ownerTask;
		private readonly IRoutedTaskRoutingHandler service;
		private Task<bool> stashSideTask;

		public TaskRoutingContext(IRoutedTaskRoutingHandler service, InternalRoutedTask ownerTask) {
			this.ownerTask = ownerTask;
			this.service = service;
		}

		/// <summary>
		///     are there any child tasks ready to run?
		/// </summary>
		public bool HasChildTasks => this.ownerTask.HasChildren;

		public bool StashingEnabled => this.service.StashingEnabled;

		/// <summary>
		///     This method will stash the task while it performs side work ina  side thread. Once the work is done, the task will
		///     be brought back into context
		///     in top priority (before other waiting tasks)
		/// </summary>
		/// <param name="stashAction"></param>
		/// <param name="stashCompleted"></param>
		/// <exception cref="ApplicationException"></exception>
		public void Stash() {

			this.service.StashTask(this.ownerTask);
		}

		public void CompleteStash() {
			this.service.RestoreStashedTask(this.ownerTask);
		}

		/// <summary>
		///     Add a sibling task in the same set as the current executing task
		/// </summary>
		/// <param name="task"></param>
		/// <param name="destination"></param>
		public void AddSibling(IRoutedTask task, string destination) {

			InternalRoutedTask routingTask = (InternalRoutedTask) task;

			if(routingTask.RoutingStatus != RoutedTask.RoutingStatuses.New) {
				throw new ApplicationException("A task that has already been routed has been added.");
			}

			routingTask.ParentContext = this.ownerTask.ChildrenContext;
			routingTask.Caller = this.service;
			routingTask.Destination = destination;

			if(this.ownerTask.ParentContext == null) {
				throw new ApplicationException("This is a top level task, it has no parent context and sibbling tasks can not be added");
			}

			this.ownerTask.ParentContext.AddTask(routingTask);
		}

		public void AddSibling(IRoutedTask task) {

			this.AddSibling(task, task.Destination);
		}

		/// <summary>
		///     Add a child task to the current set
		/// </summary>
		/// <param name="task"></param>
		/// <param name="destination"></param>
		public void AddChild(IRoutedTask task, string destination) {

			InternalRoutedTask routingTask = (InternalRoutedTask) task;

			if(routingTask.RoutingStatus != RoutedTask.RoutingStatuses.New) {
				throw new ApplicationException("A task that has already been routed has been added.");
			}

			routingTask.ParentContext = this.ownerTask.ChildrenContext;
			routingTask.Caller = this.service;
			routingTask.Destination = destination;

			this.ownerTask.ChildrenContext.AddTask(routingTask);
		}

		public void AddChild(IRoutedTask task) {

			if(task == null) {
				return;
			}

			this.AddChild(task, task.Destination);
		}

		/// <summary>
		///     Dispatch any accumulated children task and continue processing. Call WaitDispatchedChildren() to wait for their
		///     return.
		/// </summary>
		public void DispatchChildrenAsync() {
			if(this.ownerTask.ChildrenContext.HasMoreChildren) {

				if(this.ownerTask.StashStatus == RoutedTask.StashStatuses.Stashed) {
					throw new ApplicationException("Can not dispatch children while the task is stashed");
				}

				this.ownerTask.ExecutionStatus = RoutedTask.ExecutionStatuses.ChildrenDispatched;
				this.ownerTask.ChildrenContext.ProcessNextTask(this.service);
			}
		}

		/// <summary>
		///     Dispatch any accumulated children task and wait before executing any further processing.
		/// </summary>
		public bool DispatchChildrenSync() {
			return this.DispatchChildrenSync(TimeSpan.MaxValue);
		}

		/// <summary>
		///     Dispatch any accumulated children task and wait before executing any further processing.
		/// </summary>
		public bool DispatchChildrenSync(TimeSpan timeout) {
			if(this.ownerTask.HasChildren) {
				this.DispatchChildrenAsync();

				return this.WaitDispatchedChildren(timeout);
			}

			return true;
		}

		/// <summary>
		///     Here we freeze the entire thread to wait for the children tasks to complete
		/// </summary>
		public bool WaitDispatchedChildren() {
			return this.WaitDispatchedChildren(TimeSpan.MaxValue);
		}

		/// <summary>
		///     Here we freeze the entire thread to wait for the children tasks to complete
		/// </summary>
		public bool WaitDispatchedChildren(TimeSpan timeout) {
			if(!this.ownerTask.ChildrenContext.IsRunning) {
				return true;
			}

			DateTime absoluteTimeout = this.GetAbsoluteTimeout(timeout);

			//this is important. since the parent task is locked, waiting for the children to complete, we dotn want it to be sent back to it's caller. so we disable the return option.
			this.ownerTask.ChildrenContext.DispatchBackParent = false;

			do {
				bool found = false;

				do {

					if(DateTime.Now > absoluteTimeout) {
						return false;
					}

					found = this.service.CheckSingleTask(this.ownerTask.ChildrenContext.CurrentDispatchedTask.Id);

					if(!found) {
						// since we run this synchrnously, we jsut stop the thread and wait for a trigger
						if(timeout == TimeSpan.MaxValue) {
							this.service.Wait();
						} else {
							this.service.Wait(absoluteTimeout - DateTime.Now);
						}
					}
				} while(!found);
			} while(this.ownerTask.ChildrenContext.IsRunning);

			// an exception occured, we must rethrow it
			if(!this.ownerTask.ChildrenContext.TaskExecutionResults.Success && (this.ownerTask.ChildrenContext.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
				this.ownerTask.ChildrenContext.TaskExecutionResults.ExceptionDispatchInfo.Throw();
			}

			// we are done, all children have returned. we can reset it all
			this.ResetChildTasks();

			return true;
		}

		private DateTime GetAbsoluteTimeout(TimeSpan timeout) {
			return timeout == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.Now.Add(timeout);
		}

		public void ResetChildTasks() {
			this.ownerTask.ResetChildContext();
			this.ownerTask.ExecutionStatus = RoutedTask.ExecutionStatuses.New;
		}

		public void SetChildrenTasksCompleted(Action<TaskExecutionResults> completed) {
			this.ownerTask.ChildrenContext.SetCompleted(completed);
		}
	}

}