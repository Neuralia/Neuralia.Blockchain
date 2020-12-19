using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	
	public interface IPresentationEnvelopeSignature : ISingleEnvelopeSignature<PresentationAccountSignature> {
	}

	
	public class PresentationEnvelopeSignature : SingleEnvelopeSignature<PresentationAccountSignature>, IPresentationEnvelopeSignature {

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.Presentation, 1, 0);
		}
	}
}