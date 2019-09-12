using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	/// <summary>
	///     A block that is distilled into the transactions that concern us only
	/// </summary>
	public abstract class SynthesizedBlock {

		public enum AccountIdTypes {
			Hash,
			Public
		}

		public long BlockId { get; set; }
		public AccountIdTypes AccountType { get; set; }

		public Dictionary<AccountId, SynthesizedBlockAccountSet> AccountScopped { get; set; } = new Dictionary<AccountId, SynthesizedBlockAccountSet>();

		public Dictionary<TransactionId, ITransaction> ConfirmedGeneralTransactions { get; set; } = new Dictionary<TransactionId, ITransaction>();

		public Dictionary<TransactionId, ITransaction> ConfirmedTransactions { get; set; } = new Dictionary<TransactionId, ITransaction>();
		public List<RejectedTransaction> RejectedTransactions { get; set; } = new List<RejectedTransaction>();

		public List<AccountId> Accounts { get; set; } = new List<AccountId>();

		public List<SynthesizedElectionResult> FinalElectionResults { get; set; } = new List<SynthesizedElectionResult>();

		public abstract SynthesizedElectionResult CreateSynthesizedElectionResult();

		public class SynthesizedBlockAccountSet {

			public Dictionary<TransactionIdExtended, ITransaction> ConfirmedLocalTransactions { get; set; } = new Dictionary<TransactionIdExtended, ITransaction>();
			public Dictionary<TransactionId, ITransaction> ConfirmedExternalsTransactions { get; set; } = new Dictionary<TransactionId, ITransaction>();

			public Dictionary<TransactionId, ITransaction> ConfirmedTransactions { get; set; } = new Dictionary<TransactionId, ITransaction>();

			public List<RejectedTransaction> RejectedTransactions { get; set; } = new List<RejectedTransaction>();
		}

		public abstract class SynthesizedElectionResult {
			public long BlockId { get; set; }
			public DateTime Timestamp { get; set; }

			public List<AccountId> DelegateAccounts { get; set; } = new List<AccountId>();
			public Dictionary<AccountId, (AccountId accountId, AccountId delegateAccountId, Enums.ElectedPeerShareTypes peerShareType, string selectedTransactions)> ElectedAccounts { get; set; } = new Dictionary<AccountId, (AccountId accountId, AccountId delegateAccountId, Enums.ElectedPeerShareTypes peerType, string selectedTransactions)>();
		}
	}
}