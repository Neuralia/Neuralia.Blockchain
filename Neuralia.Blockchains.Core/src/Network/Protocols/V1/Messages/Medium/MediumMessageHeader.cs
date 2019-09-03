using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Medium {
	public class MediumMessageHeader : MessageHeader {

		public const ushort MAXIMUM_SIZE = ushort.MaxValue;

		public MediumMessageHeader() {

		}

		public MediumMessageHeader(int messageLength, IByteArray message) : base(messageLength, message) {
		}

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		protected override void Initialize() {
			this.MessageType = MessageTypes.Medium;
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

		}

		protected override IMessageHash CreateHash(IByteArray message = null) {
			return new MessageHash32(message);
		}

		public override int GetMaximumHeaderSize() {
			return MAXIMUM_HEADER_SIZE + PREFIX_SIZE + PREFIX_HEADER_LENGTH_SIZE;
		}
	}
}