using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows {
	public interface IWorkflowCoordinator<WORKFLOW, R> : IDisposable2
		where WORKFLOW : IWorkflow
		where R : IRehydrationFactory {

		int WaitingWorkflows { get; }
		void AddWorkflow(WORKFLOW workflow, bool immediateStart = false);
		void AddImmediateWorkflow(WORKFLOW workflow);
		bool WorkflowExists(string Id);

		WORKFLOW GetExecutingWorkflow(string Id);
		WORKFLOW GetWorkflow(string Id);
		bool AttemptStart(string Id, Action<WORKFLOW> started, Action<WORKFLOW> failed);
	}

	/// <summary>
	///     A coordinating class to handle and manage operating workflows
	/// </summary>
	public class WorkflowCoordinator<WORKFLOW, R> : IWorkflowCoordinator<WORKFLOW, R>
		where WORKFLOW : class, IWorkflow
		where R : IRehydrationFactory {

		public readonly ConcurrentDictionary<string, (WORKFLOW workflow, DateTime starttime)> executingWorkflows = new ConcurrentDictionary<string, (WORKFLOW workflow, DateTime starttime)>();
		private readonly object locker = new object();

		private readonly int maximumParallelExecution;

		/// <summary>
		///     when some workflows are long running, we promote them so that we can clear the wait for other workflows
		/// </summary>
		public readonly ConcurrentDictionary<string, WORKFLOW> promotedWorkflows = new ConcurrentDictionary<string, WORKFLOW>();

		private readonly ConcurrentQueue<WORKFLOW> queuedWorkflows = new ConcurrentQueue<WORKFLOW>();

		private readonly TimeSpan WORKFLOW_PROMOTION_TIME = TimeSpan.FromSeconds(5);

		/// <summary>
		///     all the workflows tracked, no matter their status
		/// </summary>
		private readonly ConcurrentDictionary<string, WORKFLOW> workflows = new ConcurrentDictionary<string, WORKFLOW>();

		public WorkflowCoordinator(ServiceSet<R> serviceSet, int maximumParallelExecution = 10) {
			this.maximumParallelExecution = maximumParallelExecution;
		}

		public int WaitingWorkflows => this.queuedWorkflows.Count;

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void AddImmediateWorkflow(WORKFLOW workflow) {
			this.AddWorkflow(workflow, true);
		}

		public void AddWorkflow(WORKFLOW workflow, bool immediateStart = false) {

			lock(this.locker) {
				this.CheckWorkflowPromotions();
			}

			if(workflow == null) {
				Log.Verbose("A null workflow was provided.");

				return;
			}

			if(this.workflows.ContainsKey(workflow.Id)) {
				//return, we dont run the SAME workflow twice
				Log.Verbose($"A workflow of type '{workflow.GetType().FullName}' that was already added was being added again. ignoring.");

				return;
			}

			if(workflow.ExecutionMode.HasFlag(Workflow.ExecutingMode.Single)) {
				var workingWorkflows = this.workflows.ToArray().Where(w => w.Value.VirtualMatch(workflow)).ToList();

				if(workingWorkflows.Any()) {

					if(workingWorkflows.Count > 1) {
						Log.Error($"Multiple instances of a single workflow detected for type \"{workingWorkflows.GetType().Name}\"");

						workflow.Dispose();

						return;
					}

					var active = workingWorkflows.Single();

					// we already have one, so we ignore it
					if(active.Value.ExecutionMode != Workflow.ExecutingMode.SingleRepleacable) {
						workflow.Dispose();

						return;
					}

					// ok, it is replaceable, lets do that and remove the current one so we can add it again

					// potential async solution:
					// active.Value.Completed += (w, e) => {
					// 	this.AddWorkflow(workflow, immediateStart);
					// };
					//TODO: this cna be very time expensive. redesign.
					active.Value.Dispose();
				}
			}

			try {
				this.workflows.AddSafe(workflow.Id, workflow);

			} catch(Exception ex) {
				throw new ApplicationException("Failed to add workflow");
			}

			if(workflow.ExecutionMode == Workflow.ExecutingMode.Sequential) {
				lock(this.locker) {
					if(this.executingWorkflows.ToArray().Any(w => w.Value.workflow.VirtualMatch(workflow)) || this.promotedWorkflows.ToArray().Any(w => w.Value.VirtualMatch(workflow))) {
						// we enqueue it
						this.queuedWorkflows.Enqueue(workflow);

						return;
					}
				}
			}

			bool canStartWorkflow = immediateStart;

			// check if we have any workflows to promote
			if(!canStartWorkflow) {
				lock(this.locker) {

					canStartWorkflow = (workflow.Priority == Workflow.Priority.High) || (this.executingWorkflows.Count < this.maximumParallelExecution);
				}
			}

			if(canStartWorkflow) {
				this.StartWorkflow(workflow);
			} else {
				this.queuedWorkflows.Enqueue(workflow);
			}
		}

		/// <summary>
		///     attempt to start a non started workflow, if it is possible to do so.
		/// </summary>
		/// <param name="Id"></param>
		/// <param name="started"></param>
		/// <returns></returns>
		public bool AttemptStart(string Id, Action<WORKFLOW> started, Action<WORKFLOW> failed) {
			WORKFLOW workflow = this.GetWorkflow(Id);

			if((workflow != null) && !workflow.IsCompleted && !workflow.IsStarted) {
				// ok, we can attempt tos start it
				bool canStart = true;

				if(workflow.ExecutionMode.HasFlag(Workflow.ExecutingMode.Single)) {
					var workingWorkflows = this.workflows.ToArray().Where(w => w.Value.VirtualMatch(workflow)).ToList();

					if(workingWorkflows.Any()) {
						// thas too bad, we can't
						canStart = false;
					}
				}

				if(workflow.ExecutionMode == Workflow.ExecutingMode.Sequential) {
					if(this.executingWorkflows.ToArray().Any(w => w.Value.workflow.VirtualMatch(workflow)) || this.promotedWorkflows.ToArray().Any(w => w.Value.VirtualMatch(workflow))) {
						// thas too bad, we can't
						canStart = false;
					}
				}

				if(canStart) {
					this.StartWorkflow(workflow);
					started?.Invoke(workflow);

					return true;
				}
			}

			failed?.Invoke(workflow);

			return false;
		}

		public bool WorkflowExists(string Id) {
			return this.workflows.ContainsKey(Id);
		}

		public WORKFLOW GetWorkflow(string Id) {

			if(this.workflows.ContainsKey(Id)) {
				return this.workflows[Id];
			}

			return null;
		}

		public WORKFLOW GetExecutingWorkflow(string Id) {
			if(this.WorkflowExists(Id)) {
				WORKFLOW workflow = null;

				if(this.executingWorkflows.ContainsKey(Id)) {
					workflow = this.executingWorkflows[Id].workflow;
				}

				if((workflow == null) && this.promotedWorkflows.ContainsKey(Id)) {
					workflow = this.promotedWorkflows[Id];
				}

				if((workflow != null) && !workflow.IsCompleted) {
					return workflow;
				}
			}

			return null;
		}

		/// <summary>
		///     check for long running workflows and promote them in their own queue. free some space
		/// </summary>
		private void CheckWorkflowPromotions() {
			foreach(var promoted in this.executingWorkflows.ToArray().Where(w => w.Value.workflow.IsLongRunning || ((w.Value.starttime + this.WORKFLOW_PROMOTION_TIME) < DateTime.UtcNow))) {
				this.executingWorkflows.RemoveSafe(promoted.Key);
				this.promotedWorkflows.AddSafe(promoted.Key, promoted.Value.workflow);
			}
		}

		public bool WorkflowExecuting(string Id) {
			WORKFLOW workflow = this.GetExecutingWorkflow(Id);

			return false;
		}

		public bool IsWorkflowStarted(string Id) {
			if(!this.WorkflowExists(Id)) {
				return false;
			}

			return this.workflows[Id].IsStarted;
		}

		private void StartWorkflow(WORKFLOW workflow) {

			if(!this.executingWorkflows.ContainsKey(workflow.Id)) {
				this.executingWorkflows.AddSafe(workflow.Id, (workflow, DateTime.UtcNow));

				// make sure we are alerted when it completes in any way
				workflow.Completed += this.WorkflowCompleted;

				workflow.Start();
			}
		}

		/// <summary>
		///     a workflow is completed, so we clean up and let it die
		/// </summary>
		/// <param name="sender"></param>
		protected void WorkflowCompleted(bool success, object sender) {

			WORKFLOW workflow = (WORKFLOW) sender;

			try {

				bool canStartNextWorkflow = false;
				workflow.Completed -= this.WorkflowCompleted;

				// now we clean up the garbage
				string Id = workflow.Id;

				if(this.workflows.ContainsKey(Id)) {
					this.workflows.RemoveSafe(Id);
				}

				if(this.executingWorkflows.ContainsKey(Id)) {
					this.executingWorkflows.RemoveSafe(Id);
				}

				if(this.promotedWorkflows.ContainsKey(Id)) {
					this.promotedWorkflows.RemoveSafe(Id);
				}

				lock(this.locker) {
					canStartNextWorkflow = (this.queuedWorkflows.Count != 0) && (this.executingWorkflows.Count < this.maximumParallelExecution);
				}

				// now check if we should start another workflow
				if(canStartNextWorkflow) {

					int numToStart = this.maximumParallelExecution - this.executingWorkflows.Count;
					int potentialWorkflows = this.queuedWorkflows.Count;

					while((numToStart != 0) && (potentialWorkflows != 0)) {

						this.queuedWorkflows.TryDequeue(out WORKFLOW nextWorkflow);

						if(nextWorkflow != null) {
							potentialWorkflows--;

							if(nextWorkflow.ExecutionMode == Workflow.ExecutingMode.Sequential) {
								if(this.executingWorkflows.ToArray().Any(w => w.Value.workflow.VirtualMatch(workflow)) || this.promotedWorkflows.ToArray().Any(w => w.Value.VirtualMatch(workflow))) {
									// we can't run this yet, another one is still running so we reenqueue it
									this.queuedWorkflows.Enqueue(nextWorkflow);

									continue;
								}
							}

							this.StartWorkflow(nextWorkflow);
							numToStart--;
						}
					}
				}
			} finally {
				workflow?.Dispose();

				lock(this.locker) {
					this.CheckWorkflowPromotions();
				}
			}
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				// first we cancel the threads
				foreach(WORKFLOW entry in this.workflows.Values.ToArray()) {
					try {
						entry.Dispose();
					} catch(Exception ex) {
						Log.Error(ex, "Failed to dispose of workflow task");
					}
				}

				// wait for them to complete
				Task.WaitAll(this.workflows.Values.Where(wf => wf.Task.IsCompleted == false).Select(wf => wf.Task).ToArray(), TimeSpan.FromSeconds(5));
			}
			this.IsDisposed = true;
		}

		~WorkflowCoordinator() {
			this.Dispose(false);
		}
	}
}