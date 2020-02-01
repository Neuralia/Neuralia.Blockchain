using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain {
	public class BlockLoadException : ApplicationException{

		public BlockLoadException() {
		}

		protected BlockLoadException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}

		public BlockLoadException(string message) : base(message) {
		}

		public BlockLoadException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}