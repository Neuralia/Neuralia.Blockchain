using System;

namespace Neuralia.Blockchains.Core.Exceptions {
	public class TransactionValidationException : ApplicationException {
		public TransactionValidationException(string message, Exception ex) : base(message, ex) {
		}
	}
}