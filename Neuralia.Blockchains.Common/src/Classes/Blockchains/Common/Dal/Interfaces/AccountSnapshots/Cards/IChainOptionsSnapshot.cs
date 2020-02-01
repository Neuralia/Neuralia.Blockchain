namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {
	public interface IChainOptionsSnapshot : ISnapshot {
		int Id { get; set; }

		/// <summary>
		///     The maximum client version allowed on the network
		/// </summary>
		string MaximumVersionAllowed { get; set; }

		/// <summary>
		///     minimum version which is still accepted, but produces a warning.
		/// </summary>
		string MinimumWarningVersionAllowed { get; set; }

		/// <summary>
		///     The minimum client version allowed on the network
		/// </summary>
		string MinimumVersionAllowed { get; set; }

		/// <summary>
		///     The average amount of time in seconds between each block. Allows to help determine how often to check for syncing.
		/// </summary>
		int MaxBlockInterval { get; set; }
		
		bool AllowGossipPresentations  { get; set; }
	}
}