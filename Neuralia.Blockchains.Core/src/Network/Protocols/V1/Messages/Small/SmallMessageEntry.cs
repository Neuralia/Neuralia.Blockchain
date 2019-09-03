using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Small {
	public class SmallMessageEntry : MessageEntry<SmallMessageHeader> {

		public SmallMessageEntry(IByteArray message = null) : base(message) {

		}

		public override void RebuildHeader(IByteArray buffer) {
			this.Header.Rehydrate(buffer);
		}

		protected override SmallMessageHeader CreateHeader() {
			return new SmallMessageHeader();
		}

		protected override SmallMessageHeader CreateHeader(int messageLength, IByteArray message) {
			return new SmallMessageHeader(messageLength, message);
		}
	}
}