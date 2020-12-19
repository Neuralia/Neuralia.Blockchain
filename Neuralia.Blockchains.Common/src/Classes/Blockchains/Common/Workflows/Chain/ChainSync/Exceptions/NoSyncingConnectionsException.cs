using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Exceptions {
	public class NoSyncingConnectionsException : ApplicationException {
		public NoSyncingConnectionsException() {
		}

		protected NoSyncingConnectionsException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public NoSyncingConnectionsException(string message) : base(message) {
		}

		public NoSyncingConnectionsException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}