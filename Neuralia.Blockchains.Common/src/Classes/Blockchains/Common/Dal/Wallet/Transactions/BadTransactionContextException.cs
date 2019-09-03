using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions {
	public class BadTransactionContextException : ApplicationException {

		public BadTransactionContextException() {
		}

		protected BadTransactionContextException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public BadTransactionContextException(string message) : base(message) {
		}

		public BadTransactionContextException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}