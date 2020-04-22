using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {
	public class RandomTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly RandomTransactionSelectionStrategySettings randomTransactionSelectionStrategySettings;

		public RandomTransactionSelectionMethod(long blockId, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext, RandomTransactionSelectionStrategySettings randomTransactionSelectionStrategySettings) : base(blockId, configuration, chainStateProvider, walletProvider, electionContext) {
			
			this.randomTransactionSelectionStrategySettings = randomTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.Random, 1, 0);
		}

		public override async Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {
			var poolTransactions = await chainEventPoolProvider.GetTransactionIds().ConfigureAwait(false);

			// exclude the transactions that should not be selected
			var availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).Shuffle().ToList();

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