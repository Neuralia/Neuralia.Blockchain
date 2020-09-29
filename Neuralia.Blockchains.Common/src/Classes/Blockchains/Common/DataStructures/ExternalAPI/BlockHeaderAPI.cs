using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	public abstract class BlockHeaderAPI {
		public long BlockId { get; set; }
		public string Version { get; set; }
		public long Timestamp { get; set; }
		public DateTime FullTimestamp { get; set; }
		public byte[] Hash { get; set; }
		public string ElectionContext { get; set; }
		
		public Dictionary<byte, BlockHeaderIntermediaryResultAPI> IntermediaryElectionResults { get; set; } = new Dictionary<byte, BlockHeaderIntermediaryResultAPI>();
		public Dictionary<byte, BlockHeaderFinalResultsAPI> FinalElectionResults { get; set; } = new Dictionary<byte, BlockHeaderFinalResultsAPI>();

		
		public abstract BlockHeaderIntermediaryResultAPI CreateBlockHeaderIntermediaryResultAPI();
		public abstract BlockHeaderFinalResultsAPI CreateBlockHeaderFinalResultsAPI();

	}

	public abstract class BlockHeaderIntermediaryResultAPI {
		public byte Offset { get; set; }
		public Dictionary<string, byte> PassiveElected { get; set; } = new Dictionary<string, byte>();
	}

	public abstract class BlockHeaderFinalResultsAPI {

		public byte Offset { get; set; }
		public List<BlockHeaderElectedResultAPI> ElectedResults { get; set; } = new List<BlockHeaderElectedResultAPI>();
		public List<BlockHeaderDelegateResultAPI> DelegateResults { get; set; } = new List<BlockHeaderDelegateResultAPI>();

		public abstract BlockHeaderElectedResultAPI CreateBlockHeaderElectedResultAPI();
		public abstract BlockHeaderDelegateResultAPI CreateBlockHeaderDelegateResultAPI();
		
		
		public virtual void FillFromElectionResult(IFinalElectionResults electionResults, IBlock block) {

			this.Offset = electionResults.BlockOffset;

			foreach (var delegateAccount in electionResults.DelegateAccounts) {
				var delegateResult = this.CreateBlockHeaderDelegateResultAPI();
				
				delegateResult.FillFromDelegateResult(delegateAccount.Key, delegateAccount.Value);
				
				this.DelegateResults.Add(delegateResult);
			}
			
			foreach(var electionResult in electionResults.ElectedCandidates) {
				var elected = this.CreateBlockHeaderElectedResultAPI();
				
				elected.FillFromElectionResult(electionResult.Key, electionResult.Value, block);
				
				this.ElectedResults.Add(elected);
			}
		}
	}
	
	public abstract class BlockHeaderElectedResultAPI {
		
		public AccountId Account { get; set; }
		public AccountId Delegate { get; set; }
		public int MiningTier { get; set; }
		
		public virtual void FillFromElectionResult(AccountId accountId, IElectedResults electionResults, IBlock block) {
			this.Account = accountId;
			this.Delegate = electionResults.DelegateAccountId;
			this.MiningTier = (int)electionResults.ElectedTier;
		}
	}
	
	public abstract class BlockHeaderDelegateResultAPI {
		
		public AccountId Delegate { get; set; }
		
		public virtual void FillFromDelegateResult(AccountId accountId, IDelegateResults delegateResults) {
			this.Delegate = accountId;
		}
	}

	public class TransactionInfoAPI {
			
		public List<string> ImpactedAccountIds { get; set; } = new List<string>();
		public string Version { get; set; }
		public ushort TransactionType { get; set; }
		public byte[] TransactionBytes { get; set; }
		public Enums.TransactionTargetTypes TargetType { get; set; }
	}

	public class IndexedTransactionInfoAPI : TransactionInfoAPI{
		public int IndexedTransactionIndex { get; set; }
	}

	public class RejectedTransactionInfoAPI {
		public ushort ReasonCode { get; set; }
	}

	public abstract class DecomposedBlockAPI {
		
		public BlockHeaderAPI BlockHeader { get; set; }
		public Dictionary<string, IndexedTransactionInfoAPI> IndexedTransactions { get; set; } = new Dictionary<string, IndexedTransactionInfoAPI>();
		public Dictionary<string, TransactionInfoAPI> Transactions { get; set; } = new Dictionary<string, TransactionInfoAPI>();
		public Dictionary<string, RejectedTransactionInfoAPI> RejectedTransactions { get; set; } = new Dictionary<string, RejectedTransactionInfoAPI>();
	}
}