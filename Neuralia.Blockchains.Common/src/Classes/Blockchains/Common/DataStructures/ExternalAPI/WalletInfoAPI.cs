namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	public class WalletInfoAPI {
		public bool WalletExists { get; set; }
		public bool IsWalletLoaded { get; set; }
		public bool WalletEncrypted { get; set; }
		public string WalletPath { get; set; }
	}
}