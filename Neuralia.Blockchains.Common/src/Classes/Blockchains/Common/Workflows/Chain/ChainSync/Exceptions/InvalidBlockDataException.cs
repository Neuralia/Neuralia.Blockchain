using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Exceptions {
	public class InvalidBlockDataException : ApplicationException {

		public InvalidBlockDataException() {
		}

		protected InvalidBlockDataException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public InvalidBlockDataException(string message) : base(message) {
		}

		public InvalidBlockDataException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}