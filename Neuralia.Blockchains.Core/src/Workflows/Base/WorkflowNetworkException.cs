using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Workflows.Base {
	public class WorkflowNetworkException : ApplicationException {

		public WorkflowNetworkException() {
		}

		protected WorkflowNetworkException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public WorkflowNetworkException(string message) : base(message) {
		}

		public WorkflowNetworkException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}