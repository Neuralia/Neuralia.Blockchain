using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {

	public interface ISphincsCryptographicKey : ICryptographicKey {
	}

	public class SphincsCryptographicKey : CryptographicKey, ISphincsCryptographicKey {
		protected override void SetType() {
			this.Type = Enums.KeyTypes.SPHINCS;
		}
	}
}