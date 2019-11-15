using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers {
	public interface IRoutedTaskRoutingReceiver : IRoutedTaskReceiver<IRoutedTask>, IRoutedTaskRoutingHandler {
	}

	public abstract class RoutedTaskRoutingReceiver : RoutedTaskReceiver<IRoutedTask>, IRoutedTaskRoutingReceiver {

		//TODO: this entire class may need a review, especially the executingTasks dictionary
		public enum RouteMode {
			ReceiverOnly,
			Emiter
		}

		protected readonly bool enableStashing;

		private readonly Dictionary<Guid, (Task threadTask, InternalRoutedTask routedTask)> executingTasks = new Dictionary<Guid, (Task threadTask, InternalRoutedTask routedTask)>();

		protected readonly int maxParallelTasks;

		private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

		protected readonly RouteMode routeMode;

		private readonly Dictionary<Guid, (Task threadTask, InternalRoutedTask routedTask)> stashedTasks = new Dictionary<Guid, (Task threadTask, InternalRoutedTask routedTask)>();

		/// <summary>
		///     if we are awaiting for a task, then we mark it so it wont get cleaned
		/// </summary>
		private Guid? awaitingTaskId;

		private bool performWalletTransactionCheck = true;

		public RoutedTaskRoutingReceiver(ITaskRouter coordinatorTaskRouter, bool enableStashing = true, int maxParallelTasks = 1, RouteMode routeMode = RouteMode.Emiter) {
			this.TaskRouter = coordinatorTaskRouter;
			this.routeMode = routeMode;
			this.maxParallelTasks = maxParallelTasks;
			this.enableStashing = enableStashing;
		}

		/// <summary>
		///     Under certain conditions, we can optimize by not creating a delegate thread.
		/// </summary>
		protected bool IsInthreaded => this.Synchronous || (this.maxParallelTasks == 1);

		/// <summary>
		///     Make sure we return the owner of the receiver to check for authenticity
		/// </summary>
		/// <param name="caller"></param>
		/// <returns></returns>
		protected abstract IRoutedTaskRoutingHandler Owner { get; }

		public bool Synchronous { get; set; } = false;
		public bool StashingEnabled => this.enableStashing;

		public ITaskRouter TaskRouter { get; }

		/// <summary>
		///     Stash a running task. This will cause the task to be put on the side for long runnning, and the queue is freed to
		///     run other tasks
		/// </summary>
		/// <param name="task"></param>
		/// <param name="stashCompleted"></param>
		public void StashTask(InternalRoutedTask task) {
			if(task == null) {
				return;
			}

			if(!this.enableStashing) {
				throw new ApplicationException("Stashing is not enabled");
			}

			lock(this.locker) {
				if(this.stashedTasks.ContainsKey(task.Id)) {
					return;
				}

				if(this.executingTasks.ContainsKey(task.Id)) {
					(Task threadTask, InternalRoutedTask routedTask) stashingTask = this.executingTasks[task.Id];
					this.executingTasks.Remove(task.Id);

					task.StashStatus = RoutedTask.StashStatuses.Stashed;
					this.stashedTasks.Add(task.Id, stashingTask);
				}
			}
		}

		/// <summary>
		///     This method will take a stashed task, mark it as completed, and place it back into the main execution queue.
		/// </summary>
		/// <param name="task"></param>
		/// <remarks>
		///     its not absolutely required to call this, but its nice. If we dont, completed stashed tasks will still be cleaned
		///     up. but, when
		///     they are returned to the main stack, they are counted in the max parallel count, which is nicer
		/// </remarks>
		public void RestoreStashedTask(InternalRoutedTask task) {
			if(!this.enableStashing) {
				throw new ApplicationException("Stashing is not enabled");
			}

			if(task.StashStatus != RoutedTask.StashStatuses.Stashed) {
				return;
			}

			lock(this.locker) {
				if(this.executingTasks.ContainsKey(task.Id)) {
					return;
				}

				if(this.stashedTasks.ContainsKey(task.Id)) {
					(Task threadTask, InternalRoutedTask routedTask) stashingTask = this.stashedTasks[task.Id];
					this.stashedTasks.Remove(task.Id);

					task.StashStatus = RoutedTask.StashStatuses.None;
					this.executingTasks.Add(task.Id, stashingTask);
				}
			}
		}

		/// <summary>
		///     Here we poll the whole system and move it until we get the task we want
		/// </summary>
		/// <param name="taskId"></param>
		/// <returns></returns>
		public bool CheckSingleTask(Guid taskId) {

			this.CleanBuffers();

			this.TransferAvailableTasks();

			InternalRoutedTask targettedTask = null;

			if(this.enableStashing) {
				lock(this.locker) {
					if(this.stashedTasks.Any()) {
						targettedTask = this.stashedTasks.FirstOrDefault(t => (t.Value.routedTask.StashStatus == RoutedTask.StashStatuses.None) && (t.Key == taskId)).Value.routedTask;

						if(targettedTask != null) {
							this.stashedTasks.Remove(taskId);
						}
					}
				}
			}

			lock(this.locker) {
				if(this.executingTasks.Any()) {

					// now see if we have any parallel tasks that might finally be completed
					targettedTask = this.executingTasks.FirstOrDefault(t => (t.Value.threadTask?.IsCompleted ?? true)).Value.routedTask;

					if(targettedTask != null) {
						if(this.executingTasks.ContainsKey(taskId)) {
							this.executingTasks.Remove(taskId);
						}
					}
				}
			}

			lock(this.locker) {
				if((targettedTask == null) && this.selectedTaskIds.Contains(taskId)) {

					var tasks = this.selectedTaskQueue.OfType<InternalRoutedTask>().Where(t => t.Id == taskId).ToList();

					if(tasks.Any()) {
						targettedTask = tasks.First();

						// by mistake we might have doubles. lets remove them all
						foreach(InternalRoutedTask task in tasks) {
							Repeater.Repeat(() => {
								try {
									if(this.selectedTaskQueue.Contains(task)) {
										this.selectedTaskQueue.Remove(task);
									}
								} catch {
									// do nothing
								}

								try {
									if(this.selectedTaskIds.Contains(targettedTask.Id)) {
										this.selectedTaskIds.Remove(targettedTask.Id);
									}
								} catch {
									// do nothing
								}
							});
						}
					}
				}
			}

			if(targettedTask != null) {
				// we found it!

				bool executionCompleted = false;

				if(targettedTask.RoutingStatus != RoutedTask.RoutingStatuses.Disposed) {
					try {
						executionCompleted = this.ProcessTask(targettedTask);
					} catch(NotReadyForProcessingException nex) {
						// ok, this task is just not ready, we reinsert it for a later replay
						lock(this.locker) {
							if(!this.selectedTaskIds.Contains(targettedTask.Id)) {
								this.selectedTaskQueue.Add(targettedTask);
								this.selectedTaskIds.Add(targettedTask.Id);
							}
						}
					}
				} else {
					executionCompleted = true;
				}

				if(targettedTask.TaskExecutionResults.Error && (targettedTask.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
					// ok, we have an exception, we need to throw it here
					throw targettedTask.TaskExecutionResults.Exception;
				}

				// if the task has finished its routing, then it will be disposed. anything else, this task is still going and might be forwarding children!
				if(executionCompleted) {
					return targettedTask.RoutingStatus == RoutedTask.RoutingStatuses.Disposed;
				}
			}

			return false;
		}

		public virtual void DispatchSelfTask(IRoutedTask task) {
			if(task == null) {
				return;
			}

			InternalRoutedTask routingTask = (InternalRoutedTask) task;
			routingTask.EnableSelfLoop = true;
			routingTask.Caller = null;
			routingTask.RoutingStatus = RoutedTask.RoutingStatuses.Dispatched;
			routingTask.ParentContext = null;
			routingTask.Mode = RoutedTask.ExecutionMode.Sync;

			// let's receive it ourselves
			this.ReceiveTaskSynchronous(task);

			this.CheckTasks();
		}

		public virtual void DispatchTaskAsync(IRoutedTask task) {
			if(task == null) {
				return;
			}

			if(this.performWalletTransactionCheck && this.TaskRouter.IsWalletProviderTransaction(task)) {
				throw new AsyncNotAllowedInTransactionException();
			}

			InternalRoutedTask routingTask = (InternalRoutedTask) task;
			routingTask.Caller = this.Owner;

			this.DispatchTaskNoReturnAsync(task);
		}

		public void DispatchTaskNoReturnAsync(IRoutedTask task) {
			if(task == null) {
				return;
			}

			if(this.performWalletTransactionCheck && this.TaskRouter.IsWalletProviderTransaction(task)) {
				throw new AsyncNotAllowedInTransactionException();
			}

			InternalRoutedTask routingTask = (InternalRoutedTask) task;

			if(routingTask.RoutingStatus == RoutedTask.RoutingStatuses.Disposed) {
				throw new ApplicationException("Task has been disposed. can not resend.");
			}

			routingTask.ParentContext = null;

			this.TaskRouter.RouteTask(task);
		}

		public virtual bool DispatchTaskSync(IRoutedTask task) {
			try {
				this.performWalletTransactionCheck = false;
				this.DispatchTaskAsync(task);
			} catch(AsyncNotAllowedInTransactionException ex) {
				// ignore it
			} finally {
				this.performWalletTransactionCheck = true;
			}

			return this.WaitSingleTask(task);
		}

		public bool DispatchTaskNoReturnSync(IRoutedTask task) {
			try {
				this.performWalletTransactionCheck = false;
				this.DispatchTaskNoReturnAsync(task);
			} catch(AsyncNotAllowedInTransactionException ex) {
				// ignore it
			} finally {
				this.performWalletTransactionCheck = true;
			}

			return this.WaitSingleTask(task);
		}

		public bool WaitSingleTask(IRoutedTask task) {
			return this.WaitSingleTask(task, TimeSpan.MaxValue);
		}

		public bool WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			if(task == null) {
				return false;
			}

			InternalRoutedTask routingTask = (InternalRoutedTask) task;

			if(this.Synchronous || (routingTask.Mode == RoutedTask.ExecutionMode.Sync) || (routingTask.RoutingStatus == RoutedTask.RoutingStatuses.Disposed)) {
				return true; // in sync mode, we always find it.
			}

			Guid taskId = task.Id;

			// we are waiting for this task, so lets make sure its not cleaned out before we receive it
			this.awaitingTaskId = taskId;

			lock(this.locker) {
				this.excludedTasks.Add(taskId);
			}

			try {
				DateTime absoluteTimeout = this.GetAbsoluteTimeout(timeout);

				// loop a certain amount of time until we find our task
				bool found = false;

				(Task threadTask, InternalRoutedTask routedTask)? taskSet = null;

				do {
					found = this.CheckSingleTask(taskId);

					lock(this.locker) {
						if(this.executingTasks.ContainsKey(taskId)) {
							taskSet = this.executingTasks[taskId];
						}
					}

					if(found) {
						continue;
					}

					if(absoluteTimeout < DateTime.UtcNow) {
						// time out, we stop here
						return false;
					}

					if(taskSet.HasValue) {
						// ok, the task is there. so we will loop actively to poll for it. If its completed, we dont even sleep, we just loop immediately. otherwise we give it some time
						while((taskSet.Value.threadTask?.IsCompleted ?? true) == false) {
							Thread.Sleep(10);
						}
					} else {
						// ok, since we did not find our task, we will process the other ones. there might be children
						this.CheckTasks();

						// since we didnt have anything we are looking for, we will wait until we get a wakeup call or time our
						Thread.Sleep(100);

					}
				} while((DateTime.UtcNow < absoluteTimeout) && !found);

				return found;
			} finally {
				this.awaitingTaskId = null;

				lock(this.locker) {
					this.excludedTasks.Remove(taskId);
				}
			}
		}

		public void Wait() {
			this.Wait(TimeSpan.MaxValue);
		}

		public void Wait(TimeSpan timeout) {
			if(timeout == TimeSpan.MaxValue) {
				this.resetEvent.Wait();
			} else {
				this.resetEvent.Wait(timeout);
			}
			this.resetEvent.Reset();
		}

		public override void ReceiveTask(IRoutedTask task) {
			if(task == null) {
				return;
			}

			InternalRoutedTask internalRoutedTask = (InternalRoutedTask) task;

			internalRoutedTask.Mode = RoutedTask.ExecutionMode.Async;

			if(!internalRoutedTask.EnableSelfLoop && (internalRoutedTask.RoutingStatus != RoutedTask.RoutingStatuses.Returned) && (internalRoutedTask.RoutingStatus != RoutedTask.RoutingStatuses.Disposed) && (internalRoutedTask.Caller != null) && internalRoutedTask.Caller.Equals(this.Owner)) {
				throw new ApplicationException("Sending a task to sender. loops are not allowed.");
			}

			//TODO: do we really need this?
			//			if(this.routeMode == RouteMode.ReceiverOnly) {
			//				if(internalRoutedTask.RoutingStatus != RoutedTask.RoutingStatuses.Returned) {
			//					throw new ApplicationException("This receiver is marked as receving executed messages only. no new messages allowed");
			//				}
			//			}

#if DEBUG
			internalRoutedTask.StackTrace = Environment.StackTrace;
#endif

			base.ReceiveTask(task);

			this.resetEvent.Set();
		}

		public override void ReceiveTaskSynchronous(IRoutedTask task) {
			if(task == null) {
				return;
			}

			InternalRoutedTask internalRoutedTask = (InternalRoutedTask) task;
			internalRoutedTask.Mode = RoutedTask.ExecutionMode.Sync;

			base.ReceiveTaskSynchronous(task);

			this.resetEvent.Set();
		}

		private void CleanBuffers() {

			// clear stashed tasks
			if(this.enableStashing) {
				lock(this.locker) {
					foreach(var completedTask in this.stashedTasks.Where(t => t.Value.threadTask?.IsCompleted ?? true).ToArray()) {
						this.stashedTasks.Remove(completedTask.Key);
					}

					//stashed tasks that have completed their stash but not their thread, we return them to the tashed
					foreach(var completedTask in this.stashedTasks.Where(t => t.Value.routedTask.StashStatus == RoutedTask.StashStatuses.None).ToArray()) {
						this.RestoreStashedTask(completedTask.Value.routedTask);
					}
				}
			}

			if(!this.IsInthreaded) {
				lock(this.locker) {
					// clear completed tasks
					foreach(var completedTask in this.executingTasks.Where(t =>  t.Value.threadTask == null || t.Value.threadTask.IsCompleted && (!this.awaitingTaskId.HasValue || (t.Key != this.awaitingTaskId.Value))).ToArray()) {
						this.executingTasks.Remove(completedTask.Key);
					}
				}
			}
		}

		protected override IRoutedTask GetNextQueuedTask() {

			this.CleanBuffers();

			if(!this.IsInthreaded) {
				lock(this.locker) {
					// we dont go above our maximum amount of executing tasks
					if(this.executingTasks.Count() >= this.maxParallelTasks) {
						return null;
					}
				}
			}

			return base.GetNextQueuedTask();
		}

		public void WaitTasks() {

			List<(Task threadTask, InternalRoutedTask routedTask)> taskLists = null;

			lock(this.locker) {
				taskLists = this.executingTasks.Values.ToList();

				taskLists.AddRange(this.stashedTasks.Values);
			}

			Task.WaitAll(taskLists.Where(t => t.threadTask != null).Select(t => t.threadTask).ToArray());
		}

		private DateTime GetAbsoluteTimeout(TimeSpan timeout) {
			return timeout == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.UtcNow.Add(timeout);
		}

		protected override bool ProcessTask(IRoutedTask task) {

			if(((InternalRoutedTask) task).RoutingStatus == RoutedTask.RoutingStatuses.Disposed) {
				return true;
			}

			if(this.IsInthreaded) {

				RoutedTaskProcessor.ProcessTask((InternalRoutedTask) task, this.Owner);
				
				return true;
			}

			// this is multi threaded.
			Task processingTask = Task.Factory.StartNew(() => {
				try {
					RoutedTaskProcessor.ProcessTask((InternalRoutedTask) task, this.Owner);
				} catch(NotReadyForProcessingException nrex) {
					Log.Error("NotReadyForProcessingExceptions can not be launched from multi threaded processing");
				}

				// lets set it to wake up the parent, in case it is waiting
				this.resetEvent.Set();
			});

			lock(this.locker) {
				this.executingTasks.Add(task.Id, (processingTask, (InternalRoutedTask) task));
			}

			return false;
		}
	}
}