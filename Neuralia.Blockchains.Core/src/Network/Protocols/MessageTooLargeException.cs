using System;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageTooLargeException : Exception {

		public MessageTooLargeException() {
		}

		public MessageTooLargeException(string message) : base(message) {
		}

		public MessageTooLargeException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}