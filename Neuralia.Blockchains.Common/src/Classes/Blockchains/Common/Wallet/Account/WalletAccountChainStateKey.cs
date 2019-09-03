using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {
	public interface IWalletAccountChainStateKey {
		[BsonId]
		byte Ordinal { get; set; }

		KeyUseIndexSet LocalKeyUse { get; set; }
		KeyUseIndexSet LatestBlockSyncKeyUse { get; set; }
	}

	public abstract class WalletAccountChainStateKey : IWalletAccountChainStateKey {

		[BsonId]
		public byte Ordinal { get; set; }

		// the latest key data as we see it from our side from our use
		public KeyUseIndexSet LocalKeyUse { get; set; } = new KeyUseIndexSet();

		// the latest key data as received from block confirmations
		public KeyUseIndexSet LatestBlockSyncKeyUse { get; set; } = new KeyUseIndexSet();
	}

}