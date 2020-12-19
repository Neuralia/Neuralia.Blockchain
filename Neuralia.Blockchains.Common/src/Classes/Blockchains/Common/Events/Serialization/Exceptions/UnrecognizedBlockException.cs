using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedBlockException : UnrecognizedElementException {

		public UnrecognizedBlockException(BlockchainType blockchainType, string chainName) : base(blockchainType, chainName) {
		}
	}
}