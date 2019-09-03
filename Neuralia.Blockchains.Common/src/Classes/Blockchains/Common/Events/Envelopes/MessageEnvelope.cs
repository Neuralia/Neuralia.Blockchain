using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IMessageEnvelope : ISignedEnvelope<IDehydratedBlockchainMessage, IPublishedEnvelopeSignature> {
		Guid ID { get; }
	}

	public abstract class MessageEnvelope : SignedEnvelope<IDehydratedBlockchainMessage, IPublishedEnvelopeSignature>, IMessageEnvelope {

		public MessageEnvelope() {
			this.Signature = new PublishedEnvelopeSignature();
		}

		public Guid ID { get; } = Guid.NewGuid();

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());
			nodeList.Add(this.Signature.GetStructuresArray());

			return nodeList;
		}

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.Message, 1, 0);
		}

		protected override IDehydratedBlockchainMessage RehydrateContents(IDataRehydrator rh) {

			IDehydratedBlockchainMessage dehydratedMessage = new DehydratedBlockchainMessage();
			dehydratedMessage.Rehydrate(rh);

			return dehydratedMessage;
		}
	}
}