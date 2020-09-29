using System;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletElectionsHistory {
		[BsonId]
		long BlockId { get; set; }

		DateTime Timestamp { get; set; }

		string DelegateAccount { get; set; }

		Enums.MiningTiers MiningTier { get; set; }

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

		public string DelegateAccount { get; set; }
		public Enums.MiningTiers MiningTier { get; set; }
		public string SelectedTransactions { get; set; }
	}

	public interface IWalletElectionsMiningSessionStatistics {
		[BsonId]
		ObjectId Id { get; set; }
		byte MiningTier { get; set; }
		long BlocksProcessed { get; set; }
		long BlocksElected { get; set; }
		long BlockStarted { get; set; }
		long LastBlockElected { get; set; }
		DateTime Start { get; set; }
		DateTime? Stop { get; set; }
	}

	public abstract class WalletElectionsMiningSessionStatistics : IWalletElectionsMiningSessionStatistics {
		[BsonId]
		public ObjectId Id { get; set; }
		public byte MiningTier { get; set; }
		public long BlocksProcessed { get; set; }
		public long BlocksElected { get; set; }
		
		public long BlockStarted { get; set; }
		public long LastBlockElected { get; set; }
		public DateTime Start { get; set; }
		public DateTime? Stop { get; set; }
	}
	
	public interface IWalletElectionsMiningAggregateStatistics {
		[BsonId]
		ObjectId Id { get; set; }
		byte MiningTier { get; set; }
		long BlocksProcessed { get; set; }
		long BlocksElected { get; set; }
		long LastBlockElected { get; set; }
		int MiningSessions { get; set; }
	}
	
	public abstract class WalletElectionsMiningAggregateStatistics : IWalletElectionsMiningAggregateStatistics {
		[BsonId]
		public ObjectId Id { get; set; }
		public byte MiningTier { get; set; }
		public long BlocksProcessed { get; set; }
		public long BlocksElected { get; set; }
		public long LastBlockElected { get; set; }
		public int MiningSessions { get; set; }
	}
}
