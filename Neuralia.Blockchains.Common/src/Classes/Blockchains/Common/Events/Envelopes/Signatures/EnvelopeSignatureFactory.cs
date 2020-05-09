using System;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public static class EnvelopeSignatureFactory {
		public static IEnvelopeSignature Rehydrate(IDataRehydrator rehydrator) {

			ComponentVersion<EnvelopeSignatureType> version = rehydrator.RehydrateRewind<ComponentVersion<EnvelopeSignatureType>>();

			IEnvelopeSignature envelopeSignature = null;

			if(version == EnvelopeSignatureTypes.Instance.Published) {
				envelopeSignature = new PublishedEnvelopeSignature();

			} else if(version == EnvelopeSignatureTypes.Instance.SingleSecret) {
				envelopeSignature = new SecretEnvelopeSignature();

			} else if(version == EnvelopeSignatureTypes.Instance.SingleSecretCombo) {
				envelopeSignature = new SecretComboEnvelopeSignature();

			} else if(version == EnvelopeSignatureTypes.Instance.Joint) {
				envelopeSignature = new JointEnvelopeSignature();
			} else if(version == EnvelopeSignatureTypes.Instance.JointPublished) {
				envelopeSignature = new JointPublishedEnvelopeSignature();
			} else if(version == EnvelopeSignatureTypes.Instance.Presentation) {
				envelopeSignature = new PresentationEnvelopeSignature();
			}

			if(envelopeSignature == null) {
				throw new ApplicationException("Invalid account signature type");
			}

			envelopeSignature.Rehydrate(rehydrator);

			return envelopeSignature;
		}
	}
}