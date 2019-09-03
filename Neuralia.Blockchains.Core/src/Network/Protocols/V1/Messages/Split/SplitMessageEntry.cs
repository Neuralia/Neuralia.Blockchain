using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split {
	public class SplitMessageEntry : MessageEntry<SplitMessageHeader>, ISplitMessageEntry<SplitMessageHeader> {

		private ByteArray assembledMessage;

		private IByteArray dehydratedHeaderBytes;

		public SplitMessageEntry(IByteArray message = null) : base(message) {

			if(message != null) {
				// now lets set our slice descriptions 
				this.HeaderT.SetBodyDescriptions(this.SliceMessage(message));
			}

			// in the case of a large message, we dont keep the original data. once sliced, we are done with it and can clear the memory.
			this.ClearMessage();
		}

		protected override bool AllowEmptyMessage => true;

		public override void RebuildHeader(IByteArray buffer) {
			this.Header.Rehydrate(buffer);
		}

		public override bool IsComplete => this.HeaderT.IsComplete;

		/// <summary>
		///     we should never call Message on a large message
		/// </summary>
		public override IByteArray Message => throw new NotImplementedException();

		public int CompleteMessageLength => this.HeaderT.CompleteMessageLength;

		/// <summary>
		///     Take all the slices and rebuild the complete message
		/// </summary>
		/// <returns></returns>
		public IByteArray AssembleCompleteMessage() {
			if(!this.IsComplete) {
				return null;
			}

			if(this.assembledMessage == null) {
				this.assembledMessage = new ByteArray(this.HeaderT.CompleteMessageLength);

				int offset = 0;

				foreach(Slice slice in this.HeaderT.Slices.Values.OrderBy(s => s.index)) {

					this.assembledMessage.CopyFrom(slice.bytes, offset);
					offset += slice.length;
				}

				if(!this.HeaderT.Hash.CompareHash(this.assembledMessage)) {
					throw new ApplicationException("The resulting assembled message hash is different than was advertized during protocol transfer. Data is corrupted.");
				}
			}

			return this.assembledMessage;
		}

		public long Hash => ((MessageHash64) this.HeaderT.Hash).Hash;

		public IByteArray CreateNextSliceRequestMessage() {
			if(this.IsComplete) {
				throw new ApplicationException("There are no more slices to request. the download is complete.");
			}

			// lets get the next slice to download
			Slice nextSlice = this.HeaderT.Slices.Values.Where(s => !s.IsLoaded).OrderBy(s => s.index).First();

			SliceRequestMessageEntry nextrequestEntry = new SliceRequestMessageEntry(this.HeaderT.Hash, nextSlice);

			return nextrequestEntry.Dehydrate();
		}

		public IByteArray CreateSliceResponseMessage(ISliceRequestMessageEntry requestSliceMessageEntry) {

			Slice slice = this.HeaderT.Slices[requestSliceMessageEntry.Index];

			SliceResponseMessageEntry nextrequestEntry = new SliceResponseMessageEntry(this.HeaderT.Hash, slice.index, slice.hash, slice.bytes);

			return nextrequestEntry.Dehydrate();
		}

		public void SetSliceData(ISliceResponseMessageEntry responseSliceMessageEntry) {
			IMessageEntry messageEntry = (IMessageEntry) responseSliceMessageEntry;

			Slice slice = this.HeaderT.Slices[responseSliceMessageEntry.Index];

			if(slice.hash != responseSliceMessageEntry.SliceHash) {
				throw new ApplicationException("Slice hash provided is not the same as the one we have locally");
			}

			slice.bytes = messageEntry.Message;

			//now we recompute and confirm the total slice hashes if we are complete to confirm we match what was promissed in the header.
			if(this.IsComplete) {
				long finalHash = this.HeaderT.ComputeSliceHash();

				if(finalHash != this.HeaderT.SlicesHash) {
					throw new ApplicationException("The computed hash of the slices does not match expected value. Data is corrupted.");
				}
			}
		}

		public override IByteArray Dehydrate() {
			// since we cache the split messages, we store the bytes, in case we need to reuse them
			if(this.dehydratedHeaderBytes != null) {
				return this.dehydratedHeaderBytes;
			}

			this.dehydratedHeaderBytes = base.Dehydrate();

			return this.dehydratedHeaderBytes;
		}

		protected override void WriteMessage(IDataDehydrator dh) {
			// we have no message here
		}

		protected override SplitMessageHeader CreateHeader() {
			return new SplitMessageHeader();
		}

		protected override SplitMessageHeader CreateHeader(int messageLength, IByteArray message) {
			return new SplitMessageHeader(message);
		}

		/// <summary>
		///     cut the message in multiple slices
		/// </summary>
		private Dictionary<int, Slice> SliceMessage(IByteArray bytes) {

			int startIndex = 0;
			int length = Slice.MAXIMUM_SIZE;
			int endIndex = length;

			int index = 1;
			var slices = new Dictionary<int, Slice>();

			do {
				// check if the remaining buffer is smaller than the maximum size
				if(endIndex > bytes.Length) {
					endIndex = bytes.Length;
					length = endIndex - startIndex;
				}

				ByteArray sliceBytes = new ByteArray(length);
				sliceBytes.CopyFrom(bytes, startIndex, length);

				slices.Add(index, new Slice(index, startIndex, length, sliceBytes));

				index++;
				startIndex += length;
				endIndex += length;

			} while(startIndex < bytes.Length);

			return slices;
		}

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					// dispose of the slices, so they can free their memory
					foreach(Slice slice in this.HeaderT.Slices.Values) {
						slice.Dispose();
					}
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~SplitMessageEntry() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}