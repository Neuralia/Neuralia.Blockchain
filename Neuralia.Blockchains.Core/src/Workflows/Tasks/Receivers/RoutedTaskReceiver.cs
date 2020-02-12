using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers {

	public interface IRoutedTaskReceiver<T> : IRoutedTaskHandler
		where T : IDelegatedTask {
		List<Guid> CheckTasks(Action loopItemAction = null);
	}

	/// <summary>
	///     a special object to handle all logistics related to receiving our own tasks (only) and posting them to full
	///     dispatchers
	/// </summary>
	public abstract class RoutedTaskReceiver<T> : IRoutedTaskReceiver<T>
		where T : class, IDelegatedTask {

		/// <summary>
		///     the queue for tasks that we sent and get the answer back. Workflow never receive fresh tasks, only send and get the
		///     answer
		/// </summary>
		/// <returns></returns>
		/// <remarks>ConcurrentQueue has a weird behavior, it stores references to entries in its slot field. This means big objects stay referenced even if dequeued. We use a wrapper to eliminate this.</remarks>
		private readonly WrapperConcurrentQueue<T> entryTaskQueue = new WrapperConcurrentQueue<T>();

		/// <summary>
		///     Tasks that we want excluded from the pickup
		/// </summary>
		protected readonly List<Guid> excludedTasks = new List<Guid>();

		protected readonly object locker = new object();

		/// <summary>
		///     tasks that need to be reinserted for defered execution
		/// </summary>
		protected readonly Dictionary<Guid, ReinsertedTaskInfo> reinsertedTasks = new Dictionary<Guid, ReinsertedTaskInfo>();

		/// <summary>
		///     we also store the Ids for quick check if we have a task
		/// </summary>
		protected readonly HashSet<Guid> selectedTaskIds = new HashSet<Guid>();

		/// <summary>
		///     The local queue that is not thread safe
		/// </summary>
		protected readonly List<T> selectedTaskQueue = new List<T>();

		/// <summary>
		///     wait for messages and when received, process the search in the lambda.
		/// </summary>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns>The guid of the tasks that were processesd</returns>
		public virtual List<Guid> CheckTasks(Action loopItemAction = null) {
			var processedTasks = new List<Guid>();

			this.TransferAvailableTasks();

			T task = null;

			//First, we remove the messages from the shared queue and transfer them into our own personal queue
			while((task = this.GetNextQueuedTask()) != null) {
				// run a potential action on every loop (like checking if we should cancel, for example. trask processing can be long)
				loopItemAction?.Invoke();

				// transfer into our own personal thread queue
				try {
					this.ProcessTask(task);

					processedTasks.Add(task.Id);

					lock(this.locker) {
						if(this.reinsertedTasks.ContainsKey(task.Id)) {
							this.reinsertedTasks.Remove(task.Id);
						}
					}
				} catch(NotReadyForProcessingException nrex) {
					// the task is not ready to be processed. let's reinsert it

					ReinsertedTaskInfo existingTask = null;

					lock(this.locker) {
						if(!this.reinsertedTasks.ContainsKey(task.Id)) {
							this.reinsertedTasks.Add(task.Id, new ReinsertedTaskInfo(task));
						} else {
							existingTask = this.reinsertedTasks[task.Id];
						}
					}

					existingTask?.IncrementAttempt();
				}
			}

			// now reinsert tasks that should be rerun

			return processedTasks;
		}

		protected virtual T GetNextQueuedTask() {
			T task = null;

			lock(this.locker) {
				task = this.selectedTaskQueue.FirstOrDefault(t => !this.excludedTasks.Contains(t.Id));

				if(task == null) {
					return null;
				}

				this.selectedTaskQueue.Remove(task);
				this.selectedTaskIds.Remove(task.Id);
			}

			return task;
		}

		/// <summary>
		///     Transfer the tasks for the public (thread safe) entry queue into our own private one
		/// </summary>
		protected void TransferAvailableTasks() {
			//First, we remove the messages from the shared queue and transfer them into our own personal queue
			while(this.entryTaskQueue.TryDequeue(out T task)) {
				lock(this.locker) {
					if(!this.selectedTaskIds.Contains(task.Id)) {
						this.selectedTaskQueue.Add(task);
						this.selectedTaskIds.Add(task.Id);
					}
				}
			}

			// now the tasks to be reinserted
			T[] tasks = null;

			lock(this.locker) {
				if(this.reinsertedTasks.Any()) {
					tasks = this.reinsertedTasks.Values.Where(t => t.CanRun).OrderBy(t => t.timestamp).Select(t => t.task).ToArray();
				}
			}

			if(tasks != null) {
				foreach(T reinsertTask in tasks) {
					lock(this.locker) {
						if(!this.selectedTaskIds.Contains(reinsertTask.Id)) {
							this.selectedTaskQueue.Add(reinsertTask);
							this.selectedTaskIds.Add(reinsertTask.Id);
						}
					}
				}
			}
		}

		public event Action TaskReceived;

		public virtual void ReceiveTask(T task) {

			try {
				this.entryTaskQueue.Enqueue(task);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to post task");
			} finally {
				// now lets wakeup our thread and continue
				this.TriggerTaskReceived();
			}
		}

		/// <summary>
		///     Receive and run a task simultaneously
		/// </summary>
		/// <param name="task"></param>
		public virtual void ReceiveTaskSynchronous(T task) {

			this.ProcessTask(task);
		}

		private void TriggerTaskReceived() {
			this.TaskReceived?.Invoke();
		}

		/// <summary>
		///     here we handle only our own returning tasks
		/// </summary>
		/// <param name="task"></param>
		protected abstract bool ProcessTask(T task);

		protected class ReinsertedTaskInfo {
			public static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

			public ReinsertedTaskInfo(T task) {
				this.task = task;
			}

			public T task { get; }
			public DateTime timestamp { get; } = DateTime.UtcNow;
			public DateTime nexttry { get; private set; } = DateTime.UtcNow;
			public int attempt { get; private set; }

			public TimeSpan Delay { get; private set; }

			public bool CanRun => this.nexttry < DateTime.UtcNow;

			public void IncrementAttempt() {

				this.Delay = TimeSpan.FromMilliseconds(10 * this.attempt);

				// dont wait longer than a second.
				if(this.Delay > OneSecond) {
					this.Delay = OneSecond;
				}

				// every try wait a little longer before we are reinserted.
				this.nexttry = DateTime.UtcNow + this.Delay;

				if(this.attempt == int.MaxValue) {
					this.attempt = 0;
				}

				this.attempt++;
			}
		}
	}
}