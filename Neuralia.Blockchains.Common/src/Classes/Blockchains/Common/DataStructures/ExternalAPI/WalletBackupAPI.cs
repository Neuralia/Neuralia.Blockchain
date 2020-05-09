namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public class WalletBackupAPI {
		public string Path { get; set; }
		public string Passphrase { get; set; }
		public string Salt { get; set; }
		public int Iterations { get; set; }
	}
}