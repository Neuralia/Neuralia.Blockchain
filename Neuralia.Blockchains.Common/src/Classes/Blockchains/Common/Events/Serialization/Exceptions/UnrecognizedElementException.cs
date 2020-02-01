using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions {
	public class UnrecognizedElementException  : Exception {

		public BlockchainType BlockchainType { get; }
		public string ChainName { get; }

		public UnrecognizedElementException(BlockchainType blockchainType, string chainName) : base(FormatedWarningMessage(blockchainType, chainName)) {
			this.BlockchainType = blockchainType;
			this.ChainName = chainName;
		}


		public static string FormatedWarningMessage(BlockchainType blockchainType, string chainName) {
			return $"An unrecognized event was found in chain {chainName}. You must upgrade your node to be able to continue. Syncs can no longer continue until then!";
		}
		
	}
}