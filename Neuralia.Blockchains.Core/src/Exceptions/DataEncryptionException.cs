using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Exceptions {
	public class DataEncryptionException : ApplicationException {

		public DataEncryptionException() {
		}

		protected DataEncryptionException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public DataEncryptionException(string message) : base(message) {
		}

		public DataEncryptionException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}