using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {
	public class SizeTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly SizeTransactionSelectionStrategySettings sizeTransactionSelectionStrategySettings;

		public SizeTransactionSelectionMethod(long blockId, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext, SizeTransactionSelectionStrategySettings sizeTransactionSelectionStrategySettings, NodeShareType nodeShareType) : base(blockId, chainStateProvider, walletProvider, electionContext, nodeShareType) {
			this.sizeTransactionSelectionStrategySettings = sizeTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.Size, 1, 0);
		}

		public override List<TransactionId> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {
			var poolTransactions = chainEventPoolProvider.GetTransactionIds();

			// exclude the transactions that should not be selected
			var availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();

			//TODO: implement this

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