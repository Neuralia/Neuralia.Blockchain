using System;
using Neuralia.Blockchains.Components.Blocks;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions {
	public class WalletSyncException : ApplicationException {

		public WalletSyncException(BlockId blockId) {
			this.BlockId = blockId;
		}

		public WalletSyncException(BlockId blockId, string message) : base(message) {
			this.BlockId = blockId;
		}

		public WalletSyncException(BlockId blockId, string message, Exception innerException) : base(message, innerException) {
			this.BlockId = blockId;
		}

		public BlockId BlockId { get; }
	}
}