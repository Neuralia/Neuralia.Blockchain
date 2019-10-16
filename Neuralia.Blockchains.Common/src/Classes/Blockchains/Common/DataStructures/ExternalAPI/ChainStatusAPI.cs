
namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	
	public class ChainStatusAPI {
		public bool WalletExists { get; set; }
		public bool IsWalletLoaded { get; set; }
		public bool WalletEncrypted { get; set; }
		public string WalletPath { get; set; }
		public int MinRequiredPeerCount { get; set; }
	}
}