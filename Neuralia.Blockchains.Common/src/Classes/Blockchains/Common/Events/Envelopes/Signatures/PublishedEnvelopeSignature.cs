using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {

	public interface IPublishedEnvelopeSignature : ISingleEnvelopeSignature<PublishedAccountSignature> {
	}

	public class PublishedEnvelopeSignature : SingleEnvelopeSignature<PublishedAccountSignature>, IPublishedEnvelopeSignature {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
		}

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.Published, 1, 0);
		}
	}
}