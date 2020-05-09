using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {
	public class TaskChildrenContext {

		private readonly InternalRoutedTask owner;
		private readonly LinkedList<InternalRoutedTask> tasks = new LinkedList<InternalRoutedTask>();

		/// <summary>
		///     An event that is invoke when all children in the set have completed. Contains the results of the execution with
		///     errors if applicable.
		/// </summary>
		/// <remarks>
		///     Note that this is a simple even and it does not support any further routing, stashing, etc. It's only power is
		///     to handle or rethrow the exception
		/// </remarks>
		private Action<TaskExecutionResults> completed;

		private TaskChildrenContext parent;

		public TaskChildrenContext(InternalRoutedTask owner, TaskChildrenContext parent) {
			this.parent = parent;
			this.owner = owner;
		}

		public TaskExecutionResults TaskExecutionResults { get; private set; }

		public InternalRoutedTask CurrentDispatchedTask { get; private set; }
		private bool HasCompleted { get; set; }

		/// <summary>
		///     Once this set of children is completed. do we send the parent back to its caller? usually we want to. but if the
		///     parent is explicitely waiting for the completion, then we wont. its already there.
		/// </summary>
		public bool DispatchBackParent { get; set; } = true;

		public bool HasMoreChildren => this.tasks.Any();
		public bool IsRunning => this.CurrentDispatchedTask != null;

		public void SetCompleted(Action<TaskExecutionResults> completed) {
			this.completed = completed;
		}

		public void AddTask(InternalRoutedTask task) {
			this.tasks.AddLast(task);
		}

		public async Task ProcessNextTask(IRoutedTaskRoutingHandler currentIRoutedTaskRoutingHandler) {
			if(this.HasCompleted) {
				throw new ApplicationException("Cannot reuse a completed children context object");
			}

			if(this.HasMoreChildren) {
				InternalRoutedTask routingTask = this.tasks.First.Value;
				this.tasks.RemoveFirst();

				// make sure this is always set
				routingTask.Caller = currentIRoutedTaskRoutingHandler;

				this.CurrentDispatchedTask = routingTask;
				this.TaskExecutionResults = new TaskExecutionResults();

				// route it to its destination
				await routingTask.Router.RouteTask(routingTask).ConfigureAwait(false);
			} else {
				this.CurrentDispatchedTask = null;

				// that's it, we are done
				try {
					if(this.completed != null) {
						this.completed(this.TaskExecutionResults);
					}
				} catch(Exception ex) {
					//TODO: what should we do here? we dont want to do much. log perhaps?
					NLog.Default.Error(ex, "Exception occured while invoking the children task set completed action");
				}

				this.HasCompleted = true;

				this.owner.ExecutionStatus = RoutedTask.ExecutionStatuses.ChildrenCompleted;

				if(this.DispatchBackParent) {
					RoutedTaskProcessor.ReturnToCaller(this.owner);
				}
			}
		}

		/// <summary>
		///     This is called when an exception happened on the local side.
		/// </summary>
		/// <param name="ex"></param>
		public void RaiseException(Exception ex) {
			// ok, here we stop everything
			this.CurrentDispatchedTask = null;
			this.tasks.Clear();

			this.TaskExecutionResults.Exception = ex;

			if(this.completed != null) {
				this.completed(this.TaskExecutionResults);
			}

			this.HasCompleted = true;

			this.owner.ExecutionStatus = RoutedTask.ExecutionStatuses.ChildrenCompleted;

			if(this.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow) {

				this.owner.TaskExecutionResults.Copy(this.TaskExecutionResults);

				if(this.owner.Caller == null) {
					// we must rethrow the exception and this task has no caller, its the end of the line. so we throw it here
					throw this.TaskExecutionResults.Exception;
				}
			}

			if(this.owner.RoutingStatus == RoutedTask.RoutingStatuses.Dispatched) {
				// if we were dispatched, we go back
				RoutedTaskProcessor.ReturnToCaller(this.owner);
			}
		}
	}
}