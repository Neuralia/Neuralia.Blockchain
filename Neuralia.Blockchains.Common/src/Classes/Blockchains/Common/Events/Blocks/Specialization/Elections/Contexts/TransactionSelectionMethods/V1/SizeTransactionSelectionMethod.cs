using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {
	public class SizeTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly SizeTransactionSelectionStrategySettings sizeTransactionSelectionStrategySettings;

		public SizeTransactionSelectionMethod(long blockId, IChainMiningStatusProvider chainMiningStatusProvider, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext, SizeTransactionSelectionStrategySettings sizeTransactionSelectionStrategySettings) : base(blockId, chainMiningStatusProvider, configuration, chainStateProvider, walletProvider, electionContext) {
			this.sizeTransactionSelectionStrategySettings = sizeTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.Size, 1, 0);
		}

		public override async Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions, List<WebTransactionPoolResult> webTransactions) {
			List<TransactionId> poolTransactions = await chainEventPoolProvider.GetTransactionIds().ConfigureAwait(false);

			// exclude the transactions that should not be selected
			List<TransactionId> availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();
			List<WebTransactionPoolResult> availableWebTransactions = webTransactions.Where(p => !existingTransactions.Contains(new TransactionId(p.TransactionId))).ToList();

			//TODO: implement this

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