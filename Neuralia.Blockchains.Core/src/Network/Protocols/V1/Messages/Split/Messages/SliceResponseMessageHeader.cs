using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages {
	public class SliceResponseMessageHeader : MessageHeader {

		public const int MAXIMUM_SIZE = Slice.MAXIMUM_SIZE;
		public long SliceHash;
		public int SliceIndex;

		public SliceResponseMessageHeader() {

		}

		public SliceResponseMessageHeader(int messageLength, IMessageHash hash) : base(messageLength, hash) {
		}

		public SliceResponseMessageHeader(int messageLength, IMessageHash messageHash, int sliceindex, long sliceHash) : base(messageLength, messageHash) {

			this.SliceIndex = sliceindex;
			this.SliceHash = sliceHash;
		}

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		protected override IMessageHash CreateHash(IByteArray message = null) {
			return new MessageHash64();
		}

		protected override void Initialize() {
			this.MessageType = MessageTypes.SplitSliceResponse;
		}

		protected override void DehydrateHeader(IDataDehydrator dh) {
			// in this case, we serialize to a minimal size
			this.Hash.WriteHash(dh);

			this.DehydrateComponents(dh);
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

			dh.Write(this.SliceIndex);
			dh.Write(this.SliceHash);
		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

			this.SliceIndex = rh.ReadInt();
			this.SliceHash = rh.ReadLong();
		}

		public override int GetMaximumHeaderSize() {
			return sizeof(int) + sizeof(long) + sizeof(long) + PREFIX_SIZE;
		}

		protected override void RehydrateHeader(IDataRehydrator rh) {

			// now we know what to expect, lets set the expected max size
			rh.UpdateMaximumReadSize(this.GetMaximumHeaderSize());

			int startOffset = rh.Offset;

			this.Hash.ReadHash(rh);

			this.RehydrateComponents(rh);

			// lets check again, to ensure that we really did read less than maximum size
			if(rh.Offset != this.GetMaximumHeaderSize()) {
				throw new ApplicationException("We read more data than was advertised in the header length. Corrupt data.");
			}
		}
	}
}