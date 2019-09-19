using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split {
	public class SplitMessageHeader : MessageHeader, ISplitMessageHeader {

		public const int MINIMUM_MESSAGE_SIZE = LargeMessageHeader.MAXIMUM_SIZE; //about 1 megabyte
		public const int MAXIMUM_MESSAGE_SIZE = 1000000 * 10; //about 10 megabyte

		/// <summary>
		///     This is the actual maximum size of this very protocol message. this is independent of the total message when all
		///     slices are joined.
		/// </summary>
		public const int MAXIMUM_SIZE = 0;

		public SplitMessageHeader() {

		}

		public SplitMessageHeader(SafeArrayHandle message) : base(0, message) {

			this.CompleteMessageLength = message.Length;

			// the complete message length, once we get all the slices
			this.CheckCompleteMessageSize();
		}

		public Dictionary<int, Slice> Slices { get; private set; } = new Dictionary<int, Slice>();
		public List<Slice> SlicesValues => this.Slices.Values.OrderBy(s => s.index).ToList();

		public long SlicesHash { get; private set; }

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		public bool IsComplete => (this.Slices.Count != 0) && this.SlicesValues.All(s => s.IsLoaded);

		/// <summary>
		///     This is the length of the message once all the slices will have been reassembled.
		/// </summary>
		public int CompleteMessageLength { get; private set; }

		/// <summary>
		///     this verison will check the entire messsage length, the one we must split.
		/// </summary>
		private void CheckCompleteMessageSize() {
			if(this.CompleteMessageLength < MINIMUM_MESSAGE_SIZE) {
				throw new MessageTooSmallException($"Large message size cannot be smaller than {MINIMUM_MESSAGE_SIZE} bytes.");
			}

			if(this.CompleteMessageLength > MAXIMUM_MESSAGE_SIZE) {
				throw new MessageTooLargeException($"Large message size cannot be greater than {MAXIMUM_MESSAGE_SIZE} bytes.");
			}
		}

		public void SetBodyDescriptions(Dictionary<int, Slice> slices) {

			this.Slices.Clear();
			this.Slices = slices;
			this.SlicesHash = this.ComputeSliceHash();

		}

		public override int GetMaximumHeaderSize() {
			return MAXIMUM_HEADER_SIZE + PREFIX_SIZE + PREFIX_HEADER_LENGTH_SIZE;
		}

		protected override void Initialize() {
			this.MessageType = MessageTypes.Split;
		}

		public long ComputeSliceHash() {
			var hashes = this.SlicesValues.OrderBy(s => s.index).Select(s => s.hash).ToList();

			return ComputeSliceHash(hashes);
		}

		public static long ComputeSliceHash(List<long> hashes) {

			using(HashNodeList nodeList = new HashNodeList()) {

				foreach(long hash in hashes) {
					nodeList.Add(hash);
				}

				xxHashSakuraTree hasher = new xxHashSakuraTree();

				return hasher.HashLong(nodeList);
			}
		}

		protected override IMessageHash CreateHash(SafeArrayHandle message = null) {
			return new MessageHash64();
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

			dh.Write(this.CompleteMessageLength);
			dh.Write(this.SlicesHash);
			dh.Write(this.Slices.Count);

			foreach(Slice slice in this.SlicesValues) {
				dh.Write(slice.index);
				dh.Write(slice.hash);
				dh.Write(slice.length);
			}
		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

			this.CompleteMessageLength = rh.ReadInt();

			this.CheckCompleteMessageSize();

			this.SlicesHash = rh.ReadLong();
			int sliceCount = rh.ReadInt();

			// confirm that the slice count matches expectations
			int expectedSliceCount = (int) Math.Ceiling((double) this.CompleteMessageLength / Slice.MAXIMUM_SIZE);

			if(expectedSliceCount != sliceCount) {
				throw new ApplicationException("The promissed slice count is different than mathematically expected. data is corrupted");
			}

			this.Slices.Clear();

			for(int i = 0; i < sliceCount; i++) {

				int index = rh.ReadInt();

				if(index != (i + 1)) {
					throw new ApplicationException("Slice idnexes do not match expected order");
				}

				long hash = rh.ReadLong();

				int sliceLength = rh.ReadInt();

				if(sliceLength > Slice.MAXIMUM_SIZE) {
					throw new ApplicationException("Slice length is larger than possible maximum length. data is corrupted");
				}

				this.Slices.Add(index, new Slice(index, sliceLength, hash));
			}

			// ensure that the slice total matches expected total
			int sliceTotal = this.SlicesValues.Sum(s => s.length);

			if(sliceTotal != this.CompleteMessageLength) {
				throw new ApplicationException("Slice length does not match expected total message size. data is corrupted");
			}
		}
	}
}