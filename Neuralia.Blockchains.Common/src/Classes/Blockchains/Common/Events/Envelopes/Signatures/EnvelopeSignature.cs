using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {

	public interface IEnvelopeSignature : IVersionable<EnvelopeSignatureType>, IJsonSerializable {
	}

	public abstract class EnvelopeSignature : Versionable<EnvelopeSignatureType>, IEnvelopeSignature {
	}
}