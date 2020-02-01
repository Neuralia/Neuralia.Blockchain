namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {
	public class ChainOptionsSnapshot : IChainOptionsSnapshot {

		public int Id { get; set; }
		public string MaximumVersionAllowed { get; set; }
		public string MinimumWarningVersionAllowed { get; set; }
		public string MinimumVersionAllowed { get; set; }
		public int MaxBlockInterval { get; set; }
		public bool AllowGossipPresentations { get; set; }
	}
}