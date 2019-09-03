namespace Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies {
	public class CreationTimeTransactionSelectionStrategySettings : TransactionSelectionStrategySettings {
		public enum SortingMethods {
			OlderToNewer,
			NewerToOlder,
			Random
		}

		public SortingMethods SortingMethod { get; set; } = SortingMethods.NewerToOlder;
	}
}