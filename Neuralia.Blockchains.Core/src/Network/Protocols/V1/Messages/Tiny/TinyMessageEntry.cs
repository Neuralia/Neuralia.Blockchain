using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Tiny {
	public class TinyMessageEntry : MessageEntry<TinyMessageHeader> {

		public TinyMessageEntry(SafeArrayHandle message = null) : base(message) {

		}

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.HeaderT.Rehydrate(buffer);
		}

		protected override TinyMessageHeader CreateHeader() {
			return new TinyMessageHeader();
		}

		protected override TinyMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			return new TinyMessageHeader(messageLength, message);
		}
	}
}