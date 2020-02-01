using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedMessageException : UnrecognizedElementException {
		
		public UnrecognizedMessageException(BlockchainType blockchainType, string chainName) : base(blockchainType, chainName) {
		}
		
	}
}