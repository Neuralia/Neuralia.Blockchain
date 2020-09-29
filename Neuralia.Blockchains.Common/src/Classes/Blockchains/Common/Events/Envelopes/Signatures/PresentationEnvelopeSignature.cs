using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	
	
	
	public class PresentationEnvelopeSignature : SingleEnvelopeSignature<PresentationAccountSignature> {

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.Presentation, 1, 0);
		}
	}
}