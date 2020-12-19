using System;

namespace Neuralia.Blockchains.Core.Network.Exceptions {
	public class TcpApplicationException : ApplicationException {

		public TcpApplicationException() {
		}

		public TcpApplicationException(string message) : base(message) {
		}

		public TcpApplicationException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}