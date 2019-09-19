using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages {
	public class SliceRequestMessageEntry : MessageEntry<SliceRequestMessageHeader>, ISliceRequestMessageEntry {

		public SliceRequestMessageEntry() {

		}

		public SliceRequestMessageEntry(IMessageHash hash, Slice slice) {
			this.HeaderT = new SliceRequestMessageHeader(hash, slice);
		}

		protected override bool AllowEmptyMessage => true;
		public override bool IsComplete => true;
		public override SafeArrayHandle Message => null;
		public long LargeMessageHash => ((MessageHash64) this.HeaderT.Hash).Hash;
		public int Index => this.HeaderT.slice.index;
		public long SliceHash => this.HeaderT.slice.hash;

		protected override void WriteMessage(IDataDehydrator dh) {
			// do nothing, we have no body here
		}

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.Header.Rehydrate(buffer);

		}

		protected override SliceRequestMessageHeader CreateHeader() {
			return new SliceRequestMessageHeader();
		}

		protected override SliceRequestMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			// we wont be using this
			return null;
		}

		public override SafeArrayHandle Dehydrate() {

			IDataDehydrator dh = DataSerializationFactory.CreateDehydrator();

			this.Header.Dehydrate(dh);

			return dh.ToArray();
		}
	}
}