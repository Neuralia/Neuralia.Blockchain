using System;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Core.Workflows {

	/// <summary>
	///     A tool to handle the correlation between networking workflows
	/// </summary>
	/// <typeparam name="WORKFLOW"></typeparam>
	/// <typeparam name="R"></typeparam>
	public class WorkflowTracker<WORKFLOW, R>
		where WORKFLOW : class, IWorkflow
		where R : IRehydrationFactory {
		private readonly uint correlationId;
		private readonly Guid myClientId;
		private readonly Guid originatorId;

		private readonly PeerConnection peerConnection;
		private readonly IWorkflowCoordinator<WORKFLOW, R> workflowCoordinator;

		public WorkflowTracker(PeerConnection peerConnection, uint correlationId, Guid originatorId, Guid myClientId, IWorkflowCoordinator<WORKFLOW, R> workflowCoordinator) {
			this.peerConnection = peerConnection;
			this.correlationId = correlationId;
			this.originatorId = originatorId;
			this.myClientId = myClientId;
			this.workflowCoordinator = workflowCoordinator;
		}

		public bool WorkflowExists() {

			string workflowId = this.GetWorkflowId();

			return this.workflowCoordinator.WorkflowExists(workflowId);
		}

		public WORKFLOW GetActiveWorkflow() {

			string workflowId = this.GetWorkflowId();

			WORKFLOW workflow = this.workflowCoordinator.GetExecutingWorkflow(workflowId);

			if(workflow == null) {
				// ok, its not executing. lets check if it is at least queued, and if so, we will try to start it
				workflow = this.workflowCoordinator.GetWorkflow(workflowId);

				if(workflow != null) {
					// ok, lets try to start it
					this.workflowCoordinator.AttemptStart(workflowId, w => {
						workflow = w;
					}, w => {
						workflow = w;
					});
				}
			}

			return workflow;
		}

		public string GetWorkflowId() {
			string workflowId = "";

			// now we verify if this message originator was us. if it was, we override the client ID
			if(this.originatorId == this.myClientId) {
				workflowId = NetworkingWorkflow.FormatScoppedId(this.myClientId, this.correlationId);
			} else {
				workflowId = NetworkingWorkflow.FormatScoppedId(this.peerConnection.ClientUuid, this.correlationId);
			}

			return workflowId;
		}
	}
}