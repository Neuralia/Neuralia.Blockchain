using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {
	
	
	public interface ISignedMessageEnvelope : ISignedEnvelope<IDehydratedBlockchainMessage, IPublishedEnvelopeSignature>, IMessageEnvelope {
	}

	public abstract class SignedMessageEnvelope : SignedEnvelope<IDehydratedBlockchainMessage, IPublishedEnvelopeSignature>, ISignedMessageEnvelope {

		public SignedMessageEnvelope() {
			this.Signature = new PublishedEnvelopeSignature();
		}

		public Guid ID { get; private set; } = Guid.NewGuid();
		protected override void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.ID);
			base.Dehydrate(dehydrator);
			
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			this.ID = rehydrator.ReadGuid();
			base.Rehydrate(rehydrator);
		}

		public override string GetId() {
			return this.ID.ToString();
		}

		protected override HashNodeList GetContentStructuresArray() {
			return this.Contents.RehydratedEvent.GetStructuresArray();
		}
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());

			return nodeList;
		}

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.SignedMessage, 1, 0);
		}

		protected override IDehydratedBlockchainMessage RehydrateContents(IDataRehydrator rh) {

			IDehydratedBlockchainMessage dehydratedMessage = new DehydratedBlockchainMessage();
			dehydratedMessage.Rehydrate(rh);

			return dehydratedMessage;
		}
	}
}