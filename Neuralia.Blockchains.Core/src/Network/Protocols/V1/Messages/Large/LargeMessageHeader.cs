using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large {
	public class LargeMessageHeader : MessageHeader {

		public const int MAXIMUM_SIZE = 1000000; // 1 megabyte

		public LargeMessageHeader() {

		}

		public LargeMessageHeader(int messageLength, IByteArray message) : base(messageLength, message) {
		}

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		protected override void Initialize() {
			this.MessageType = MessageTypes.Large;
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

		}

		protected override IMessageHash CreateHash(IByteArray message = null) {
			return new MessageHash64(message);
		}

		public override int GetMaximumHeaderSize() {
			return MAXIMUM_HEADER_SIZE + PREFIX_SIZE + PREFIX_HEADER_LENGTH_SIZE;
		}
	}
}