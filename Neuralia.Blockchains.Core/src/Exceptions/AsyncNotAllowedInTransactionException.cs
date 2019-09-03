using System;

namespace Neuralia.Blockchains.Core.Exceptions {
	public class AsyncNotAllowedInTransactionException : ApplicationException {
		public AsyncNotAllowedInTransactionException() : base("Async not supported inside a transaction. Deadlocks possible") {

		}
	}
}