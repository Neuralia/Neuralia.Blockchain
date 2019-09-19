using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large {
	public class LargeMessageEntry : MessageEntry<LargeMessageHeader> {

		public LargeMessageEntry(SafeArrayHandle message = null) : base(message) {

		}

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.HeaderT.Rehydrate(buffer);
		}

		protected override LargeMessageHeader CreateHeader() {
			return new LargeMessageHeader();
		}

		protected override LargeMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			return new LargeMessageHeader(messageLength, message);
		}
	}
}