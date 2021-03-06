using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {

	public interface ITaskStasher {
		bool StashingEnabled { get; }
		void Stash(LockContext lockContext);
		void CompleteStash();
		void SetCorrelationContext(CorrelationContext correlationContext);
	}

	/// <summary>
	///     This is a helper class that a task can use during its execution to perform various operations, such as route
	///     further children tasks, wait, etc.
	/// </summary>
	public class TaskRoutingContext : ITaskStasher {
		private readonly IRoutedTaskRoutingHandler service;
		private Task<bool> stashSideTask;

		public TaskRoutingContext(IRoutedTaskRoutingHandler service, InternalRoutedTask ownerTask) {
			this.OwnerTask = ownerTask;
			this.service = service;
		}

		public InternalRoutedTask OwnerTask { get; }

		/// <summary>
		///     are there any child tasks ready to run?
		/// </summary>
		public bool HasChildTasks => this.OwnerTask.HasChildren;

		public bool StashingEnabled => this.service.StashingEnabled;

		/// <summary>
		///     This method will stash the task while it performs side work ina  side thread. Once the work is done, the task will
		///     be brought back into context
		///     in top priority (before other waiting tasks)
		/// </summary>
		/// <param name="lockContext"></param>
		/// <param name="stashAction"></param>
		/// <param name="stashCompleted"></param>
		/// <exception cref="ApplicationException"></exception>
		public void Stash(LockContext lockContext) {

			this.service?.StashTask(this.OwnerTask);
		}

		public void CompleteStash() {
			this.service?.RestoreStashedTask(this.OwnerTask);
		}

		public void SetCorrelationContext(CorrelationContext correlationContext) {
			this.OwnerTask.CorrelationContext = correlationContext;
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

			routingTask.ParentContext = this.OwnerTask.ChildrenContext;
			routingTask.Caller = this.service;
			routingTask.Destination = destination;

			if(this.OwnerTask.ParentContext == null) {
				throw new ApplicationException("This is a top level task, it has no parent context and sibbling tasks can not be added");
			}

			this.OwnerTask.ParentContext.AddTask(routingTask);
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

			routingTask.ParentContext = this.OwnerTask.ChildrenContext;
			routingTask.Caller = this.service;
			routingTask.Destination = destination;

			this.OwnerTask.ChildrenContext.AddTask(routingTask);
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
		public async Task DispatchChildrenAsync() {
			if(this.OwnerTask.ChildrenContext.HasMoreChildren) {

				if(this.OwnerTask.StashStatus == RoutedTask.StashStatuses.Stashed) {
					throw new ApplicationException("Can not dispatch children while the task is stashed");
				}

				this.OwnerTask.ExecutionStatus = RoutedTask.ExecutionStatuses.ChildrenDispatched;
				await this.OwnerTask.ChildrenContext.ProcessNextTask(this.service).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Dispatch any accumulated children task and wait before executing any further processing.
		/// </summary>
		/// <param name="lockContext"></param>
		public Task<bool> DispatchChildrenSync(LockContext lockContext) {
			return this.DispatchChildrenSync(TimeSpan.MaxValue, lockContext);
		}

		/// <summary>
		///     Dispatch any accumulated children task and wait before executing any further processing.
		/// </summary>
		public async Task<bool> DispatchChildrenSync(TimeSpan timeout, LockContext lockContext) {
			if(this.OwnerTask.HasChildren) {
				await this.DispatchChildrenAsync().ConfigureAwait(false);

				return await this.WaitDispatchedChildren(timeout, lockContext).ConfigureAwait(false);
			}

			return true;
		}

		/// <summary>
		///     Here we freeze the entire thread to wait for the children tasks to complete
		/// </summary>
		/// <param name="lockContext"></param>
		public Task<bool> WaitDispatchedChildren(LockContext lockContext) {
			return this.WaitDispatchedChildren(TimeSpan.MaxValue, lockContext);
		}

		/// <summary>
		///     Here we freeze the entire thread to wait for the children tasks to complete
		/// </summary>
		public async Task<bool> WaitDispatchedChildren(TimeSpan timeout, LockContext lockContext) {
			if(!this.OwnerTask.ChildrenContext.IsRunning) {
				return true;
			}

			DateTime absoluteTimeout = this.GetAbsoluteTimeout(timeout);

			//this is important. since the parent task is locked, waiting for the children to complete, we dotn want it to be sent back to it's caller. so we disable the return option.
			this.OwnerTask.ChildrenContext.DispatchBackParent = false;

			do {
				bool found = false;

				do {

					if(DateTimeEx.CurrentTime > absoluteTimeout) {
						return false;
					}

					found = await this.service.CheckSingleTask(this.OwnerTask.ChildrenContext.CurrentDispatchedTask.Id).ConfigureAwait(false);

					if(!found) {
						// since we run this synchrnously, we jsut stop the thread and wait for a trigger
						if(timeout == TimeSpan.MaxValue) {
							await this.service.Wait().ConfigureAwait(false);
						} else {
							await this.service.Wait(absoluteTimeout - DateTimeEx.CurrentTime).ConfigureAwait(false);
						}
					}
				} while(!found);
			} while(this.OwnerTask.ChildrenContext.IsRunning);

			// an exception occured, we must rethrow it
			if(!this.OwnerTask.ChildrenContext.TaskExecutionResults.Success && (this.OwnerTask.ChildrenContext.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
				this.OwnerTask.ChildrenContext.TaskExecutionResults.ExceptionDispatchInfo.Throw();
			}

			// we are done, all children have returned. we can reset it all
			this.ResetChildTasks();

			return true;
		}

		private DateTime GetAbsoluteTimeout(TimeSpan timeout) {
			return timeout == TimeSpan.MaxValue ? DateTimeEx.MaxValue : DateTimeEx.CurrentTime.Add(timeout);
		}

		public void ResetChildTasks() {
			this.OwnerTask.ResetChildContext();
			this.OwnerTask.ExecutionStatus = RoutedTask.ExecutionStatuses.New;
		}

		public void SetChildrenTasksCompleted(Action<TaskExecutionResults> completed) {
			this.OwnerTask.ChildrenContext.SetCompleted(completed);
		}
	}

}