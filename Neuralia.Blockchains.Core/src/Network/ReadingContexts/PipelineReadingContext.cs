using System;
using System.Buffers;
using System.IO.Pipelines;

namespace Neuralia.Blockchains.Core.Network.ReadingContexts {
	public struct PipelineReadingContext : ITcpReadingContext {

		public readonly ReadResult readResult;
		public readonly PipeReader reader;

		public PipelineReadingContext(ReadResult readResult, PipeReader reader) {
			this.readResult = readResult;
			this.reader = reader;
		}

		public bool IsCanceled => this.readResult.IsCanceled;
		public bool IsCompleted => this.readResult.IsCompleted;
		public bool IsEmpty => this.readResult.Buffer.IsEmpty;
		public long Length => this.readResult.Buffer.Length;

		public void DataRead(int amount) {
			this.reader.AdvanceTo(this.readResult.Buffer.GetPosition(amount));
		}

		public void CopyTo(in Span<byte> dest, int srcOffset, int destOffset, int length) {
			this.readResult.Buffer.Slice(srcOffset, length).CopyTo(dest.Slice(destOffset, length));
		}

		public byte this[int i] => this.readResult.Buffer.Slice(i, 1).First.Span[0];
	}
}