using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages {
	public class SliceRequestMessageHeader : MessageHeader {

		public const int MAXIMUM_SIZE = Slice.MAXIMUM_SIZE;
		public Slice slice;

		public SliceRequestMessageHeader() {

		}

		public SliceRequestMessageHeader(IMessageHash hash, Slice slice) : base(0, hash) {
			this.slice = slice;
		}

		protected override int MaxiumMessageSize => 0;

		protected override IMessageHash CreateHash(SafeArrayHandle message = null) {
			return new MessageHash64();
		}

		protected override void Initialize() {
			this.MessageType = MessageTypes.SplitSliceRequest;
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

			dh.Write(this.slice.index);
			dh.Write(this.slice.hash);
			dh.Write(this.slice.length);
		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

			int index = rh.ReadInt();
			long sliceHash = rh.ReadLong();
			int length = rh.ReadInt();

			this.slice = new Slice(index, length, sliceHash);
		}

		public override int GetMaximumHeaderSize() {
			return sizeof(int) + sizeof(int) + sizeof(long) + sizeof(long) + PREFIX_SIZE + PREFIX_HEADER_LENGTH_SIZE;
		}
	}
}