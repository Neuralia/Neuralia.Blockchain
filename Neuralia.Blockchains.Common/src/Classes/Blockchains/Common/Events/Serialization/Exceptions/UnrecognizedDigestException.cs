using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedDigestException : UnrecognizedElementException {

		public UnrecognizedDigestException(BlockchainType blockchainType, string chainName) : base(blockchainType, chainName) {
		}
	}
}