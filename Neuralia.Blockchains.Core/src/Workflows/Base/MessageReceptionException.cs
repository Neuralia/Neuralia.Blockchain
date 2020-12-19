using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Workflows.Base {
	public class MessageReceptionException : WorkflowException {

		public MessageReceptionException() {
		}

		protected MessageReceptionException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public MessageReceptionException(string message) : base(message) {
		}

		public MessageReceptionException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}