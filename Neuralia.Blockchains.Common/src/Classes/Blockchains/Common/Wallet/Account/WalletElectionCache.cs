using LiteDB;
using Neuralia.Blockchains.Components.Transactions.Identifiers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletElectionCache {
		[BsonId]
		string TransactionId { get; set; }

		long BlockId { get; set; }
	}

	public abstract class WalletElectionCache : IWalletElectionCache {

		[BsonId]
		public string TransactionId { get; set; }

		public long BlockId { get; set; }
	}
}