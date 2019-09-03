using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Workflows.Base {
	public class WorkflowException : ApplicationException {

		public WorkflowException() {
		}

		protected WorkflowException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public WorkflowException(string message) : base(message) {
		}

		public WorkflowException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}