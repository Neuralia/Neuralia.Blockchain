using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1 {
	public class RandomTransactionSelectionMethod : TransactionSelectionMethod {

		private readonly RandomTransactionSelectionStrategySettings randomTransactionSelectionStrategySettings;

		public RandomTransactionSelectionMethod(long blockId, IWalletProvider walletProvider, ushort maximumTransactionCount, RandomTransactionSelectionStrategySettings randomTransactionSelectionStrategySettings) : base(blockId, walletProvider, maximumTransactionCount) {
			this.randomTransactionSelectionStrategySettings = randomTransactionSelectionStrategySettings;
		}

		protected override ComponentVersion<TransactionSelectionMethodType> SetIdentity() {
			return (TransactionSelectionMethodTypes.Instance.Random, 1, 0);
		}

		public override List<TransactionId> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {
			var poolTransactions = chainEventPoolProvider.GetTransactionIds();

			// exclude the transactions that should not be selected
			var availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).Shuffle().ToList();

			return this.SelectSelection(availableTransactions);
		}

		protected override List<TransactionId> SelectSelection(List<TransactionId> transactionIds) {
			return transactionIds.OrderByDescending(t => t.Timestamp).Take(this.maximumTransactionCount).ToList();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}
	}
}