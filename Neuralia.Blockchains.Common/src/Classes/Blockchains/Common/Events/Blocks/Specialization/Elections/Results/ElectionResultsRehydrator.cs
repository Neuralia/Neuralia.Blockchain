using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results {
	public interface IElectionResultsRehydrator {
		IIntermediaryElectionResults RehydrateIntermediateResults(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree);
		IFinalElectionResults RehydrateFinalResults(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree);
	}

	public abstract class ElectionResultsRehydrator : IElectionResultsRehydrator {
		public abstract IIntermediaryElectionResults RehydrateIntermediateResults(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree);

		public abstract IFinalElectionResults RehydrateFinalResults(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree);
	}
}