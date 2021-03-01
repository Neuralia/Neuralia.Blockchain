using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Threading;

namespace Neuralia.Blockchains.Core.Workflows.Base {

	public interface IWorkflow : IThreadBase {

		WorkflowId Id { get; }
		uint WorkflowId { get; }

		bool TestingMode { get; set; }

		Workflow.ExecutingMode ExecutionMode { get; }

		Workflow.Priority Priority { get; }
		bool IsLongRunning { get; }

		bool VirtualMatch(IWorkflow other);
		bool Equals(IWorkflow other);
	}

	public interface IWorkflow<R> : IThreadBase<Workflow<R>>, IWorkflow
		where R : IRehydrationFactory {
	}

	public static class Workflow {
		/// <summary>
		///     Define how this workflow will run if multiple copies are created at the same time
		/// </summary>
		[Flags]
		public enum ExecutingMode {
			/// <summary>
			///     A single instance is allowed. If other ones are created while on runs, they will be discarded.
			/// </summary>
			Single = 1 << 0,

			/// <summary>
			///     Single workflows must wait for the active one to finish before doing another. this will kill the existing one and
			///     replace it
			/// </summary>
			SingleRepleacable = (1 << 1) | Single,

			/// <summary>
			///     Multiple instances can run at the same time fine
			/// </summary>
			Parallel = 1 << 2,

			/// <summary>
			///     Multiple instances need to be queued. one much complete for another to start
			/// </summary>
			Sequential = 1 << 3
		}

		public enum Priority {
			Low,
			Normal,
			High
		}
	}

	public abstract class Workflow<R> : ThreadBase<Workflow<R>>, IWorkflow<R>
		where R : IRehydrationFactory {
		
		public static readonly TimeSpan DEFAULT_HIBERNATE_TIMEOUT = TimeSpan.FromSeconds(60 * 1);
		protected readonly ServiceSet<R> serviceSet;

		protected readonly ITimeService timeService;

		public Workflow(ServiceSet<R> serviceSet) {
			this.timeService = serviceSet?.TimeService;
			this.serviceSet = serviceSet;

			this.WorkflowId = GlobalRandom.GetNextUInt();

			this.Id = new WorkflowId(this.WorkflowId);

			// how long do we wait for an operations until we declare this workflow as dead?
			// this can happen if the peers on the other side stop responding and go mute.
			this.hibernateTimeoutSpan = DEFAULT_HIBERNATE_TIMEOUT;
		}

		/// <summary>
		///     Workflows are usually much shorter
		/// </summary>
		protected override TaskCreationOptions TaskCreationOptions => TaskCreationOptions.None;

		/// <summary>
		///     A special variable we can set to put ourselves in unit test mode and override some annoying behaviors like
		///     hibernates ;)
		/// </summary>
		public bool TestingMode { get; set; } = false;

		public uint WorkflowId { get; }

		/// <summary>
		///     Unique Id of the workflow
		/// </summary>
		public virtual WorkflowId Id { get; }

		/// <summary>
		/// </summary>
		public Workflow.Priority Priority { get; protected set; } = Workflow.Priority.Normal;

		/// <summary>
		///     is this workflow likely to last long?  will affect the promotion to long running in the workflow coordinator
		/// </summary>
		public bool IsLongRunning { get; protected set; } = false;

		public Workflow.ExecutingMode ExecutionMode { get; protected set; } = Workflow.ExecutingMode.Parallel;

		public virtual bool Equals(IWorkflow other) {
			if(other == null) {
				return false;
			}

			return this.Id == other.Id;
		}

		/// <summary>
		///     determine if a workflow, while different (by it's ID) is the same on other factors
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public virtual bool VirtualMatch(IWorkflow other) {

			if(this.Equals(other)) {
				return true;
			}

			Type ourType = this.GetType();
			Type otherType = other.GetType();

			if((ourType == otherType) || ourType.IsAssignableFrom(otherType)) {
				return true;
			}

			return false;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((Workflow<R>) obj);
		}

		public override int GetHashCode() {
			return (int) this.WorkflowId;
		}

		protected override async Task TriggerError(Exception ex) {
			await base.TriggerError(ex).ConfigureAwait(false);

			this.LogWorkflowException(ex);
		}

		protected virtual void LogWorkflowException(Exception ex) {
			NLog.Default.Verbose(ex, $"Workflow of type '{this.GetType().Name}' ended in error");
		}
	}
}