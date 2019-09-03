using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {

	public interface IPresentationEnvelopeSignature : ISingleEnvelopeSignature<PresentationAccountSignature> {
	}

	public class PresentationEnvelopeSignature : SingleEnvelopeSignature<PresentationAccountSignature>, IPresentationEnvelopeSignature {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

		}

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.Presentation, 1, 0);
		}
	}

}