using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Small {
	public class SmallMessageEntry : MessageEntry<SmallMessageHeader> {

		public SmallMessageEntry(SafeArrayHandle message = null) : base(message) {

		}

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.Header.Rehydrate(buffer);
		}

		protected override SmallMessageHeader CreateHeader() {
			return new SmallMessageHeader();
		}

		protected override SmallMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			return new SmallMessageHeader(messageLength, message);
		}
	}
}