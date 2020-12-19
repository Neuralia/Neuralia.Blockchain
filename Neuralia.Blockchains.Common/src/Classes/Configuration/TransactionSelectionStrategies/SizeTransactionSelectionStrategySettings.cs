namespace Neuralia.Blockchains.Common.Classes.Configuration.TransactionSelectionStrategies {
	public class SizeTransactionSelectionStrategySettings : TransactionSelectionStrategySettings {
		public enum SortingMethods {
			LargerToSmaller,
			SmallerToLarger,
			Random
		}

		public SortingMethods SortingMethod { get; set; } = SortingMethods.LargerToSmaller;
	}
}