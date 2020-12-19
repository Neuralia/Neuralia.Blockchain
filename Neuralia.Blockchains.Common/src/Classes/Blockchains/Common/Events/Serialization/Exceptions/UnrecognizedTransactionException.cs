using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedTransactionException : UnrecognizedElementException {

		public UnrecognizedTransactionException(BlockchainType blockchainType, string chainName) : base(blockchainType, chainName) {
		}
	}
}