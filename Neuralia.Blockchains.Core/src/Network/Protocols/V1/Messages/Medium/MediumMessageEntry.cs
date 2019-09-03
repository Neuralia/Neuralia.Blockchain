using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Medium {
	public class MediumMessageEntry : MessageEntry<MediumMessageHeader> {

		public MediumMessageEntry(IByteArray message = null) : base(message) {

		}

		public override void RebuildHeader(IByteArray buffer) {
			this.HeaderT.Rehydrate(buffer);
		}

		protected override MediumMessageHeader CreateHeader() {
			return new MediumMessageHeader();
		}

		protected override MediumMessageHeader CreateHeader(int messageLength, IByteArray message) {
			return new MediumMessageHeader(messageLength, message);
		}
	}
}