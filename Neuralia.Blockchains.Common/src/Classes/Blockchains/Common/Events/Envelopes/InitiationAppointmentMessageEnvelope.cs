using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IInitiationAppointmentMessageEnvelope: IPOWEnvelope<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature>, IMessageEnvelope,  IEnvelope<IDehydratedBlockchainMessage> {
		
	}
	
	/// <summary>
	/// this is the only blockchain message that can be sent without a signature.
	/// </summary>
	public abstract class InitiationAppointmentMessageEnvelope : Envelope<IDehydratedBlockchainMessage, EnvelopeType>, IInitiationAppointmentMessageEnvelope{

		private readonly POWEnvelopeImplementation<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature> powEnvelopeImplementation;
		public InitiationAppointmentMessageEnvelope() {
			this.powEnvelopeImplementation = new POWEnvelopeImplementation<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature>();
		}
		
		public Guid ID { get; private set; } = Guid.NewGuid();
		protected override void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.ID);
			this.powEnvelopeImplementation.Dehydrate(dehydrator);
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			this.ID = rehydrator.ReadGuid();
			this.powEnvelopeImplementation.Rehydrate(rehydrator);
		}
		
		public override string GetId() {
			return this.ID.ToString();
		}
		
		protected override HashNodeList GetContentStructuresArray() {
			return this.Contents.RehydratedEvent.GetStructuresArray();
		}
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			nodeList.Add(this.powEnvelopeImplementation.GetStructuresArray());
			
			return nodeList;
		}

		public HashNodeList GetPOWStructuresArray() {
			return this.GetStructuresArray();
		}
		
		protected override IDehydratedBlockchainMessage RehydrateContents(IDataRehydrator rh) {

			IDehydratedBlockchainMessage dehydratedMessage = new DehydratedBlockchainMessage();
			dehydratedMessage.Rehydrate(rh);

			return dehydratedMessage;
		}
		
		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.InitiationAppointment, 1, 0);
		}

		public IPOWEnvelopeSignature PowEnvelopeSignatureBase {
			get => this.powEnvelopeImplementation.PowEnvelopeSignatureBase;
			set => this.powEnvelopeImplementation.PowEnvelopeSignatureBase = value;
		}
		
		public InitiationAppointmentEnvelopeSignature PowEnvelopeSignature {
			get => this.powEnvelopeImplementation.PowEnvelopeSignature;
			set => this.powEnvelopeImplementation.PowEnvelopeSignature = value;
		}
		
		public string Key => this.ID.ToString();
	}
}