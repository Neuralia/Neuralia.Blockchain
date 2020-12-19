using System;

namespace Neuralia.Blockchains.Core.Exceptions {
	public class QueryHubsException : ApplicationException {

		public QueryHubsException() {
		}

		public QueryHubsException(string message) : base(message) {
		}

		public QueryHubsException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}