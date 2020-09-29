using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {

	public class EnvelopeSignatureType : SimpleUShort<EnvelopeSignatureType> {

		public EnvelopeSignatureType() {
		}

		public EnvelopeSignatureType(ushort value) : base(value) {
		}

		public static implicit operator EnvelopeSignatureType(ushort d) {
			return new EnvelopeSignatureType(d);
		}

		public static bool operator ==(EnvelopeSignatureType a, EnvelopeSignatureType b) {
			return a.Value == b.Value;
		}

		public static bool operator !=(EnvelopeSignatureType a, EnvelopeSignatureType b) {
			return a.Value != b.Value;
		}
	}

	public sealed class EnvelopeSignatureTypes : UShortConstantSet<EnvelopeSignatureType> {

		public readonly EnvelopeSignatureType Joint;
		public readonly EnvelopeSignatureType JointPublished;
		public readonly EnvelopeSignatureType Presentation;
		public readonly EnvelopeSignatureType Published;
		public readonly EnvelopeSignatureType SingleSecret;
		public readonly EnvelopeSignatureType SingleSecretCombo;
		public readonly EnvelopeSignatureType Initiation;
		public readonly EnvelopeSignatureType POW;
		
		
		static EnvelopeSignatureTypes() {
		}

		private EnvelopeSignatureTypes() : base(1000) {
			this.Published = this.CreateBaseConstant();
			this.Joint = this.CreateBaseConstant();
			this.JointPublished = this.CreateBaseConstant();
			this.SingleSecret = this.CreateBaseConstant();
			this.SingleSecretCombo = this.CreateBaseConstant();
			this.Presentation = this.CreateBaseConstant();
			this.Initiation = this.CreateBaseConstant();
			this.POW = this.CreateBaseConstant();
		}

		public static EnvelopeSignatureTypes Instance { get; } = new EnvelopeSignatureTypes();
	}
}