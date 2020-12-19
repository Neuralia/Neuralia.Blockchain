using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public interface ISecretComboEnvelopeSignature : ISingleEnvelopeSignature<PromisedSecretComboPublishedAccountSignature> {
	}

	public class SecretComboEnvelopeSignature : SingleEnvelopeSignature<PromisedSecretComboPublishedAccountSignature>, ISecretComboEnvelopeSignature {

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.SingleSecretCombo, 1, 0);
		}
	}
}