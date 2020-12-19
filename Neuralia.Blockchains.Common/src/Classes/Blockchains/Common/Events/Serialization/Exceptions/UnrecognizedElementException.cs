using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Exceptions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedElementException : BlockchainException {

		public UnrecognizedElementException(BlockchainType blockchainType, string chainName) : base(FormattedWarningMessage(blockchainType, chainName), blockchainType, chainName) {
			
		}
		
		public static string FormattedWarningMessage(BlockchainType blockchainType, string chainName) {
			return $"An unrecognized event was found in chain {chainName}. You must upgrade your node to be able to continue. Syncs can no longer continue until then!";
		}
	}
}