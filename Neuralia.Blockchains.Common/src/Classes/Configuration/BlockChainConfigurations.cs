using Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies;
using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Common.Classes.Configuration {
	public class BlockChainConfigurations : ChainConfigurations {
		public enum TransactionSelectionStrategies {
			Automatic = 1,
			CreationTime = 2,
			TransactionType = 3,
			Size = 4,
			Random = 5
		}

		public TransactionSelectionStrategies TransactionSelectionStrategy { get; set; } = TransactionSelectionStrategies.Automatic;

		public CreationTimeTransactionSelectionStrategySettings CreationTimeTransactionSelectionStrategySettings { get; } = new CreationTimeTransactionSelectionStrategySettings();
		public TransactionTypeTransactionSelectionStrategySettings TransactionTypeTransactionSelectionStrategySettings { get; } = new TransactionTypeTransactionSelectionStrategySettings();
		public RandomTransactionSelectionStrategySettings RandomTransactionSelectionStrategySettings { get; } = new RandomTransactionSelectionStrategySettings();
		public SizeTransactionSelectionStrategySettings SizeTransactionSelectionStrategySettings { get; } = new SizeTransactionSelectionStrategySettings();
	}
}