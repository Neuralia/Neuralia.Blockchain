using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface INTRUWalletKey : INTRUKey, IWalletKey {
	}

	public class NTRUWalletKey : WalletKey, INTRUWalletKey {
		
		public NTRUWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.NTRU, 1,0);
		}
	}
}