using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {
	public class EnvelopeType : SimpleUShort<EnvelopeType> {

		public EnvelopeType() {
		}

		public EnvelopeType(ushort value) : base(value) {
		}

		public static implicit operator EnvelopeType(ushort d) {
			return new EnvelopeType(d);
		}

		public static implicit operator EnvelopeType(byte d) {
			return new EnvelopeType(d);
		}

		public static bool operator ==(EnvelopeType a, EnvelopeType b) {
			return a.Value == b.Value;
		}

		public static bool operator !=(EnvelopeType a, EnvelopeType b) {
			return a.Value != b.Value;
		}
	}

	public sealed class EnvelopeTypes : UShortConstantSet<EnvelopeType> {

		public readonly EnvelopeType Block;
		public readonly EnvelopeType SignedMessage;
		public readonly EnvelopeType SignedTransaction;
		public readonly EnvelopeType PresentationTransaction;
		public readonly EnvelopeType InitiationAppointment;
		public readonly EnvelopeType ModeratorSignedMessage;

		static EnvelopeTypes() {
		}

		private EnvelopeTypes() : base(50) {
			this.SignedTransaction = this.CreateBaseConstant();
			this.SignedMessage = this.CreateBaseConstant();
			this.Block = this.CreateBaseConstant();
			this.PresentationTransaction = this.CreateBaseConstant();
			this.InitiationAppointment = this.CreateBaseConstant();
			this.ModeratorSignedMessage = this.CreateBaseConstant();
		}

		public static EnvelopeTypes Instance { get; } = new EnvelopeTypes();
	}
}