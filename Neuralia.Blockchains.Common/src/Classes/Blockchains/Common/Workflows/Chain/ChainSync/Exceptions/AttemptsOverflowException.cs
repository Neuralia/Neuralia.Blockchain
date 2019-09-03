using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Exceptions {
	public class AttemptsOverflowException : ApplicationException {

		public AttemptsOverflowException() {
		}

		protected AttemptsOverflowException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public AttemptsOverflowException(string message) : base(message) {
		}

		public AttemptsOverflowException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}