using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions {
	public class WalletSyncException : ApplicationException {

		public BlockId BlockId { get; }

		public WalletSyncException(BlockId blockId) {
			this.BlockId = blockId;
		}

		public WalletSyncException(BlockId blockId, string message) : base(message) {
			this.BlockId = blockId;
		}

		public WalletSyncException(BlockId blockId, string message, Exception innerException) : base(message, innerException) {
			this.BlockId = blockId;
		}
	}
}