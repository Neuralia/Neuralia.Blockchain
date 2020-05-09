namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public class ChainStatusAPI {
		public WalletInfoAPI WalletInfo { get; set; }
		public int MinRequiredPeerCount { get; set; }
		public int MiningTier { get; set; }
	}
}