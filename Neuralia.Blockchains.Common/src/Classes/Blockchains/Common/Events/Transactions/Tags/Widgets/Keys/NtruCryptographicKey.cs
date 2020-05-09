using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface INtruCryptographicKey : ICryptographicKey {
	}

	public class NtruCryptographicKey : CryptographicKey, INtruCryptographicKey {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
		}

		public override void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is INtruWalletKey ntruWalletKey) {

			}
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.NTRU;
		}
	}
}