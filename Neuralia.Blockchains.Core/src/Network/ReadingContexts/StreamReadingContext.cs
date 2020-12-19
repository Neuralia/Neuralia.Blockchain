using System;

namespace Neuralia.Blockchains.Core.Network.ReadingContexts {
	public struct StreamReadingContext : ITcpReadingContext {

		private int read;
		public readonly Memory<byte> buffer;

		public StreamReadingContext(Memory<byte> buffer) {
			this.buffer = buffer;
			this.read = 0;
		}

		public bool IsCanceled => false;
		public bool IsCompleted => false;
		public bool IsEmpty => this.buffer.Length == 0;
		public long Length => this.buffer.Length - this.read;

		public void DataRead(int amount) {
			this.read += amount;
		}

		public void CopyTo(in Span<byte> dest, int srcOffset, int destOffset, int length) {
			if(length > this.Length) {
				throw new ApplicationException("Reading to much data");
			}

			this.buffer.Slice(this.read + srcOffset, length).Span.CopyTo(dest.Slice(destOffset, length));
		}

		public byte this[int i] => this.buffer.Span[i + this.read];

		public bool AllDataRead => this.buffer.Length == this.read;
	}
}