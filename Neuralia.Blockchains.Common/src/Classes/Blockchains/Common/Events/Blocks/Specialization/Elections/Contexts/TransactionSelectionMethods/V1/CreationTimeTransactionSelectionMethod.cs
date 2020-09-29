using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {

	/// <summary>
	///     Select transactions by age. the oldest ones will be chosen first.
	/// </summary>
	public class CreationTimeTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly CreationTimeTransactionSelectionStrategySettings creationTimeTransactionSelectionStrategySettings;

		public CreationTimeTransactionSelectionMethod(long blockId, IChainMiningStatusProvider chainMiningStatusProvider, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext, CreationTimeTransactionSelectionStrategySettings creationTimeTransactionSelectionStrategySettings) : base(blockId, chainMiningStatusProvider, configuration, chainStateProvider, walletProvider, electionContext) {
			this.creationTimeTransactionSelectionStrategySettings = creationTimeTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.CreationTime, 1, 0);
		}

		public override async Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions, List<WebTransactionPoolResult> webTransactions) {

			List<TransactionId> poolTransactions = await chainEventPoolProvider.GetTransactionIds().ConfigureAwait(false);

			// exclude the transactions that should not be selected
			List<TransactionId> availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();
			List<WebTransactionPoolResult> availableWebTransactions = webTransactions.Where(p => !existingTransactions.Contains(new TransactionId(p.TransactionId))).ToList();

			if(this.creationTimeTransactionSelectionStrategySettings.SortingMethod == CreationTimeTransactionSelectionStrategySettings.SortingMethods.NewerToOlder) {
				availableTransactions = availableTransactions.OrderByDescending(t => t.Timestamp).ToList();
				availableWebTransactions = availableWebTransactions.OrderByDescending(t => new TransactionId(t.TransactionId).Timestamp).ToList();
			} else if(this.creationTimeTransactionSelectionStrategySettings.SortingMethod == CreationTimeTransactionSelectionStrategySettings.SortingMethods.OlderToNewer) {
				availableTransactions = availableTransactions.OrderBy(t => t.Timestamp).ToList();
				availableWebTransactions = availableWebTransactions.OrderBy(t => new TransactionId(t.TransactionId).Timestamp).ToList();
			} else {
				availableTransactions = availableTransactions.Shuffle().ToList();
				availableWebTransactions = availableWebTransactions.Shuffle().ToList();
			}

			return this.SelectSelection(availableTransactions, availableWebTransactions);
		}

		protected override List<TransactionId> SelectSelection(List<TransactionId> transactionIds, List<WebTransactionPoolResult> availableWebTransactions) {
			return transactionIds.OrderByDescending(t => t.Timestamp).Take(this.MaximumTransactionCount).ToList();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}