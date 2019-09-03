using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools {
	public class ImpossibleToSyncException : Exception {
		public ImpossibleToSyncException() {
		}

		public ImpossibleToSyncException(string message) : base(message) {
		}

		public ImpossibleToSyncException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}