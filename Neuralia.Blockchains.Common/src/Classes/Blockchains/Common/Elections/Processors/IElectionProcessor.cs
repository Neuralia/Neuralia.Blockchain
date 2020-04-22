using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors {
	public interface IElectionProcessor {
		ElectedCandidateResultDistillate PerformActiveElection(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount, Enums.MiningTiers miningTier);

		Dictionary<string, object> PrepareActiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, long password);
		Dictionary<string, object> PreparePassiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, long password);

		IElectionCandidacyMessage PrepareActiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);
		IElectionCandidacyMessage PreparePassiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);

		SafeArrayHandle DetermineIfElected(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount, Enums.MiningTiers miningTier);
		SafeArrayHandle DetermineIfElected(BlockElectionDistillate matureBlockElectionDistillate, SafeArrayHandle currentBlockHash, AccountId miningAccount, Enums.MiningTiers miningTier);
		Task<List<TransactionId>> SelectTransactions(long blockId, BlockElectionDistillate blockElectionDistillate, LockContext lockContext);
	}
}