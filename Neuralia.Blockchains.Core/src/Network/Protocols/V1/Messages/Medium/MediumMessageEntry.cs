using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Medium {
	public class MediumMessageEntry : MessageEntry<MediumMessageHeader> {

		public MediumMessageEntry(SafeArrayHandle message = null) : base(message) {

		}

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.HeaderT.Rehydrate(buffer);
		}

		protected override MediumMessageHeader CreateHeader() {
			return new MediumMessageHeader();
		}

		protected override MediumMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			return new MediumMessageHeader(messageLength, message);
		}
	}
}