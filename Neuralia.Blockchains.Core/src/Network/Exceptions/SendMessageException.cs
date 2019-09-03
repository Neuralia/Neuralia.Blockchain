using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Network.Exceptions {
	public class SendMessageException : ApplicationException {

		public SendMessageException() {
		}

		protected SendMessageException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public SendMessageException(string message) : base(message) {
		}

		public SendMessageException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}