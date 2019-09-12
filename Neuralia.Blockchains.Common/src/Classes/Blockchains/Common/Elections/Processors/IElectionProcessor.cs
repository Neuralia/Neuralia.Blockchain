using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors {
	public interface IElectionProcessor {
		ElectedCandidateResultDistillate PerformActiveElection(int maturityBlockHash, BlockElectionDistillate blockElectionDistillate, AccountId miningAccount);

		Dictionary<string, object> PrepareActiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);
		Dictionary<string, object> PreparePassiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);

		IElectionCandidacyMessage PrepareActiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);
		IElectionCandidacyMessage PreparePassiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate);
		IByteArray DetermineIfElected(BlockElectionDistillate blockElectionDistillate, AccountId miningAccount);

		List<TransactionId> SelectTransactions(long blockId, BlockElectionDistillate blockElectionDistillate);
	}
}