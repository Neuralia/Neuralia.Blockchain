using LiteDB;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {
	public interface IWalletAccountChainStateKey {
		[BsonId]
		byte Ordinal { get; set; }

		IdKeyUseIndexSet LocalIdKeyUse { get; set; }
		IdKeyUseIndexSet LatestBlockSyncIdKeyUse { get; set; }
	}

	public abstract class WalletAccountChainStateKey : IWalletAccountChainStateKey {

		[BsonId]
		public byte Ordinal { get; set; }

		// the latest key data as we see it from our side from our use
		public IdKeyUseIndexSet LocalIdKeyUse { get; set; } = new IdKeyUseIndexSet();

		// the latest key data as received from block confirmations
		public IdKeyUseIndexSet LatestBlockSyncIdKeyUse { get; set; } = new IdKeyUseIndexSet();
	}

}