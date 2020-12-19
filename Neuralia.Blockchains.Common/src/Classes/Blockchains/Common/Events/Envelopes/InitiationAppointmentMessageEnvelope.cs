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

	public interface IInitiationAppointmentMessageEnvelope: ITHSEnvelope<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature>, IMessageEnvelope,  IEnvelope<IDehydratedBlockchainMessage> {
		
	}
	
	/// <summary>
	/// this is the only blockchain message that can be sent without a signature.
	/// </summary>
	public abstract class InitiationAppointmentMessageEnvelope : Envelope<IDehydratedBlockchainMessage, EnvelopeType>, IInitiationAppointmentMessageEnvelope{

		private readonly THSEnvelopeImplementation<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature> thsEnvelopeImplementation;
		public InitiationAppointmentMessageEnvelope() {
			this.thsEnvelopeImplementation = new THSEnvelopeImplementation<IDehydratedBlockchainMessage, InitiationAppointmentEnvelopeSignature>();
		}
		
		public Guid ID { get; private set; } = Guid.NewGuid();
		protected override void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.ID);
			this.thsEnvelopeImplementation.Dehydrate(dehydrator);
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			this.ID = rehydrator.ReadGuid();
			this.thsEnvelopeImplementation.Rehydrate(rehydrator);
		}
		
		public override string GetId() {
			return this.ID.ToString();
		}
		
		protected override HashNodeList GetContentStructuresArray() {
			return this.Contents.RehydratedEvent.GetStructuresArray();
		}
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			nodeList.Add(this.thsEnvelopeImplementation.GetStructuresArray());
			
			return nodeList;
		}

		public HashNodeList GetTHSStructuresArray() {
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

		public ITHSEnvelopeSignature THSEnvelopeSignatureBase {
			get => this.thsEnvelopeImplementation.THSEnvelopeSignatureBase;
			set => this.thsEnvelopeImplementation.THSEnvelopeSignatureBase = value;
		}
		
		public InitiationAppointmentEnvelopeSignature THSEnvelopeSignature {
			get => this.thsEnvelopeImplementation.THSEnvelopeSignature;
			set => this.thsEnvelopeImplementation.THSEnvelopeSignature = value;
		}
		
		public string Key => this.ID.ToString();
	}
}