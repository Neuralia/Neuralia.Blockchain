using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface INtruCryptographicKey : ICryptographicKey {
	}

	public class NtruCryptographicKey : CryptographicKey, INtruCryptographicKey {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.NTRU;
		}
	}
}