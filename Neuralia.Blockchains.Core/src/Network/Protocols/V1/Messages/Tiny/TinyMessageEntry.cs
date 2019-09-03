using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Tiny {
	public class TinyMessageEntry : MessageEntry<TinyMessageHeader> {

		public TinyMessageEntry(IByteArray message = null) : base(message) {

		}

		public override void RebuildHeader(IByteArray buffer) {
			this.HeaderT.Rehydrate(buffer);
		}

		protected override TinyMessageHeader CreateHeader() {
			return new TinyMessageHeader();
		}

		protected override TinyMessageHeader CreateHeader(int messageLength, IByteArray message) {
			return new TinyMessageHeader(messageLength, message);
		}
	}
}