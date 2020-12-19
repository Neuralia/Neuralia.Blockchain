using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies {
	public class TransactionTypeTransactionSelectionStrategySettings : TransactionSelectionStrategySettings {
		public enum TransactionTypes {
			Presentation
		}

		public List<TransactionTypes> TransactionPriorities { get; set; } = new[] {TransactionTypes.Presentation}.ToList();
	}
}