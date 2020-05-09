using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet {

	public class WalletAccountAPI {
		public Guid AccountUuid { get; set; }
		public string AccountId { get; set; }
		public string FriendlyName { get; set; }
		public bool IsActive { get; set; }
		public int Status { get; set; }
	}

	public struct WalletAccountDetailsAPI {
		public Guid AccountUuid { get; set; }
		public string AccountId { get; set; }
		public string AccountHash { get; set; }
		public string FriendlyName { get; set; }
		public bool IsActive { get; set; }
		public int Status { get; set; }
		public long DeclarationBlockId { get; set; }
		public bool KeysEncrypted { get; set; }
		public int AccountType { get; set; }
		public int TrustLevel { get; set; }
		public bool Correlated { get; set; }
	}
}