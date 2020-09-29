using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IModeratorSignedMessageEnvelope : ISignedMessageEnvelope {
		
	}
	public class ModeratorSignedMessageEnvelope : SignedMessageEnvelope, IModeratorSignedMessageEnvelope{
		

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.ModeratorSignedMessage, 1, 0);
		}
	}
}