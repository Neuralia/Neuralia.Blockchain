using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedEnvelopeException : UnrecognizedElementException {

		public UnrecognizedEnvelopeException(BlockchainType blockchainType, string chainName) : base(blockchainType, chainName) {
		}
	}
}