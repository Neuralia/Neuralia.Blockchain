using System;

namespace Neuralia.Blockchains.Core.Network.Protocols {

	public class MessageTooSmallException : Exception {

		public MessageTooSmallException() {
		}

		public MessageTooSmallException(string message) : base(message) {
		}

		public MessageTooSmallException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}