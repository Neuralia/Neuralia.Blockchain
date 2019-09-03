using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public interface ISecretEnvelopeSignature : ISingleEnvelopeSignature<PromisedSecretPublishedAccountSignature> {
	}

	public class SecretEnvelopeSignature : SingleEnvelopeSignature<PromisedSecretPublishedAccountSignature>, ISecretEnvelopeSignature {

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.SingleSecret, 1, 0);
		}
	}
}