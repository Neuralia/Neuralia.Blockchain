using System;

namespace Neuralia.Blockchains.Core.Exceptions {
	public abstract class BlockchainException : ApplicationException {

		public BlockchainException(string message, BlockchainType blockchainType, string chainName) : base(message) {
			this.BlockchainType = blockchainType;
			this.ChainName = chainName;
		}
		
		public BlockchainException(string message, BlockchainType blockchainType, string chainName, Exception exception) : base(FormattedWarningMessage(message, blockchainType, chainName), exception) {
			this.BlockchainType = blockchainType;
			this.ChainName = chainName;
		}

		public BlockchainType BlockchainType { get; private set; }
		public string ChainName { get; private set; }
		
		public static string FormattedWarningMessage(string message, BlockchainType blockchainType, string chainName) {
			return $"{message} - Blockchain: {chainName} | {blockchainType}";
		}
	}
}