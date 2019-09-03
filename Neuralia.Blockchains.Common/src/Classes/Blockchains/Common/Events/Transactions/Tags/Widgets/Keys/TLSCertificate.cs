using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ITLSCertificate : ICryptographicKey {
	}

	public class TLSCertificate : CryptographicKey, ITLSCertificate {
		protected override void SetType() {
			this.Type = Enums.KeyTypes.RSA;
		}
	}
}