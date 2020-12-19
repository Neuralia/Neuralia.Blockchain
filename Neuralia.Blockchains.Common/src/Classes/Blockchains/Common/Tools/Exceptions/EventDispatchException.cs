using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions {
	public class EventDispatchException : ApplicationException {

		public EventDispatchException() {
		}

		public EventDispatchException(string? message) : base(message) {
		}

		public EventDispatchException(string? message, Exception? innerException) : base(message, innerException) {
		}
	}
}