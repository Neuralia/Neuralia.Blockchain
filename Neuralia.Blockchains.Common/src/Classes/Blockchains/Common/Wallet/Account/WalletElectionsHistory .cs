using System;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletElectionsHistory {
		[BsonId]
		long BlockId { get; set; }

		DateTime Timestamp { get; set; }

		AccountId DelegateAccount { get; set; }

		Enums.PeerTypes PeerType { get; set; }

		string SelectedTransactions { get; set; }
	}

	/// <summary>
	///     Here we save generic history of transactions. Contrary to the transaction cache, this is not meant for active use
	///     and is only a history for convenience
	/// </summary>
	public abstract class WalletElectionsHistory : IWalletElectionsHistory {

		[BsonId]
		public long BlockId { get; set; }

		public DateTime Timestamp { get; set; }

		public AccountId DelegateAccount { get; set; }
		public Enums.PeerTypes PeerType { get; set; }
		public string SelectedTransactions { get; set; }
	}
}