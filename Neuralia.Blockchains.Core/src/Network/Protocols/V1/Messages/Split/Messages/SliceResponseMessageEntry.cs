using System;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages {
	public class SliceResponseMessageEntry : MessageEntry<SliceResponseMessageHeader>, ISliceResponseMessageEntry {

		public SliceResponseMessageEntry() {

		}

		public SliceResponseMessageEntry(IMessageHash messageHash, int sliceindex, long sliceHash, SafeArrayHandle message) : base(message) {
			this.HeaderT = new SliceResponseMessageHeader(message.Length, messageHash, sliceindex, sliceHash);
		}

		public long LargeMessageHash => ((MessageHash64) this.HeaderT.Hash).Hash;
		public int Index => this.HeaderT.SliceIndex;
		public long SliceHash => this.HeaderT.SliceHash;

		public override void RebuildHeader(SafeArrayHandle buffer) {
			this.Header.Rehydrate(buffer);
		}

		protected override SliceResponseMessageHeader CreateHeader() {
			return new SliceResponseMessageHeader();
		}

		protected override SliceResponseMessageHeader CreateHeader(int messageLength, SafeArrayHandle message) {
			// we wont be using this
			return null;
		}

		public override SafeArrayHandle Dehydrate() {

			return base.Dehydrate();

		}

		protected override void ValidateMessageHash(SafeArrayHandle message) {
			// first, lets compare the hashes
			// first, lets compare the hashes
			long hash = Slice.ComputeSliceHash(message);

			if(this.HeaderT.SliceHash != hash) {
				throw new ApplicationException("The expected hash of the message from the header is different than the actual content");
			}
		}
	}
}