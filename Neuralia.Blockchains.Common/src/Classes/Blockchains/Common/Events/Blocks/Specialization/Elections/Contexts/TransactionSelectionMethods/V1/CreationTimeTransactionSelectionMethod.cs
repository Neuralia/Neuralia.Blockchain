using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {

	/// <summary>
	///     Select transactions by age. the oldest ones will be chosen first.
	/// </summary>
	public class CreationTimeTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly CreationTimeTransactionSelectionStrategySettings creationTimeTransactionSelectionStrategySettings;

		public CreationTimeTransactionSelectionMethod(long blockId, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext, CreationTimeTransactionSelectionStrategySettings creationTimeTransactionSelectionStrategySettings, NodeShareType nodeShareType) : base(blockId, chainStateProvider, walletProvider, electionContext, nodeShareType) {
			this.creationTimeTransactionSelectionStrategySettings = creationTimeTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.CreationTime, 1, 0);
		}

		public override List<TransactionId> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {

			var poolTransactions = chainEventPoolProvider.GetTransactionIds();

			// exclude the transactions that should not be selected
			var availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();

			if(this.creationTimeTransactionSelectionStrategySettings.SortingMethod == CreationTimeTransactionSelectionStrategySettings.SortingMethods.NewerToOlder) {
				availableTransactions = availableTransactions.OrderByDescending(t => t.Timestamp).ToList();
			} else if(this.creationTimeTransactionSelectionStrategySettings.SortingMethod == CreationTimeTransactionSelectionStrategySettings.SortingMethods.OlderToNewer) {
				availableTransactions = availableTransactions.OrderBy(t => t.Timestamp).ToList();
			} else {
				availableTransactions = availableTransactions.Shuffle().ToList();
			}

			return this.SelectSelection(availableTransactions);
		}

		protected override List<TransactionId> SelectSelection(List<TransactionId> transactionIds) {
			return transactionIds.OrderByDescending(t => t.Timestamp).Take(this.MaximumTransactionCount).ToList();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}